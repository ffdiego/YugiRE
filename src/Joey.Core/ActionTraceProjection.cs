namespace Joey.Core;

/// <summary>Comparable four-word representation, not a native binary log.
/// Unmapped and unsuccessful commands remain visible, with their sequence.</summary>
public sealed record ProjectedAction(long Sequence, string Command, string Origin, string Outcome,
    int Side, string Mapping, ushort[]? Args, string Detail, PlacementObservation? Placement = null, DirectAttackObservation? DirectAttack = null,
    TargetedAttackObservation? TargetedAttack = null, BattleRemovalObservation? BattleRemoval = null);

public static class ActionTraceProjection
{
    public static IReadOnlyList<ProjectedAction> ReadExecuted(IEnumerable<ActionTraceEntry> trace)
    {
        var rows = new List<ProjectedAction>();
        long previous = 0;
        foreach (var entry in trace.Where(e => e.Stage == ActionTraceStage.Executed))
        {
            var e = entry.Entry;
            var result = entry.Result ?? throw new InvalidOperationException("Executed command has no result.");
            if (e.Sequence <= previous) throw new InvalidOperationException("Executed sequence must increase.");
            previous = e.Sequence;
            var command = e.Command;
            ushort[]? words = null;
            string mapping = result.Outcome == CommandOutcome.Applied ? "unmapped" : "non_applied";
            if (result.Outcome == CommandOutcome.Applied && command.Side is 0 or 1)
            {
                ushort side = (ushort)(command.Side << 15);
                // Ordinary successful transfer. Empty-deck result is a terminal
                // operation, not a successful draw. Do not infer a native word.
                words = command switch
                {
                    DrawCommand or TurnDrawCommand when result.Detail.Length == 0 => [(ushort)(side | 0x61), 1, 1, 0],
                    DamageCommand d when d.Amount is > 0 and <= ushort.MaxValue => [(ushort)(side | 0x43), (ushort)d.Amount, 0, 1],
                    HealCommand h when h.Amount is > 0 and <= ushort.MaxValue => [(ushort)(side | 0x42), (ushort)h.Amount, 0, 1],
                    SummonCommand or SetCommand => PlacementWords(command, result.Placement),
                    DeclareDirectAttackCommand attack => DirectAttackWords(attack, result.DirectAttack),
                    DeclareTargetedAttackCommand attack => TargetedAttackWords(attack,result.TargetedAttack),
                    SnapshotBattleRemovalCommand snapshot => RemovalWords(snapshot.Target,snapshot.Role,result.BattleRemoval,false),
                    FinishBattleRemovalCommand finish => RemovalWords(finish.Target,finish.Role,result.BattleRemoval,true),
                    _ => null
                };
                if (words is not null) mapping = "mapped";
            }
            rows.Add(new(e.Sequence, command.GetType().Name, e.Origin.ToString(), result.Outcome.ToString(),
                command.Side, mapping, words, result.Detail, result.Placement, result.DirectAttack, result.TargetedAttack, result.BattleRemoval));
        }
        return rows.AsReadOnly();
    }

    private static ushort[]? TargetedAttackWords(DeclareTargetedAttackCommand command,TargetedAttackObservation? a)
    {
        if(a is null || a.Attacker!=command.Attacker || a.Defender!=command.Defender ||
            a.Attacker.Side!=command.Side || a.Defender.Side!=1-command.Side ||
            a.Attacker.Zone!=DuelZone.Monster || a.Defender.Zone!=DuelZone.Monster ||
            a.AttackerSlot is <0 or >=5 || a.DefenderSlot is <0 or >=5) return null;
        return [(ushort)((command.Side<<15)|0x33),(ushort)a.AttackerSlot,(ushort)a.DefenderSlot,(ushort)(a.Defender.Side+1)];
    }

    private static ushort[]? RemovalWords(CardTarget target,int role,BattleRemovalObservation? a,bool finish)
    {
        if(a is null || a.Target!=target || a.Role!=role || target.Zone!=DuelZone.Monster ||
            a.ResourceId==0 || a.Slot is <0 or >=5 || role is not (0 or 1)) return null;
        // Only defender 7C has an observed ordinary variant in the fixture.
        // Keep attacker routing visible but unmapped pending dynamic evidence.
        // P164 has a callback at GY arrival; the ordinary 7C variant is
        // not established for its native routing yet.
        if(finish && (role!=1 || CardSelectionRules.BattleRecruitAttribute(a.ResourceId) is not null)) return null;
        return [(ushort)((target.Side<<15)|(finish?0x7c:0x3c)),(ushort)a.Slot,
            (ushort)(finish?0:role),(ushort)(finish?1:0)];
    }

    private static ushort[]? DirectAttackWords(DeclareDirectAttackCommand command, DirectAttackObservation? a)
    {
        if (a is null || a.Attacker != command.Attacker || a.Attacker.Side != command.Side ||
            a.Attacker.Zone != DuelZone.Monster || a.ResourceId == 0 || a.Slot is < 0 or >= 5 ||
            a.RequestedDamage is < 0 or > ushort.MaxValue) return null;
        return [(ushort)((command.Side << 15) | 0x34), (ushort)a.Slot, 1, 0];
    }

    private static ushort[]? PlacementWords(DuelCommand command, PlacementObservation? p)
    {
        // Limit CE to the normal no-tribute variants whose four words have
        // been verified; CF only common face-down S/T, not field/activation.
        if (p is null || p.Source.Side != command.Side || p.Source.Zone != DuelZone.Hand ||
            p.ResourceId is 0 or > 0xfff || p.HandIndex is < 0 or >= 80 ||
            p.Slot is < 0 or >= 5 || p.PaidTributes != 0) return null;
        ushort side = (ushort)(command.Side << 15);
        int nativeSlot = p.Slot;
        ushort opcode, flags;
        switch (command)
        {
            case SummonCommand summon when summon.Source == p.Source && summon.Slot == p.Slot &&
                p.Destination == DuelZone.Monster && !p.FaceDown && !p.Defense:
                opcode = 0xCE; flags = 1; break;
            case SetCommand set when set.Source == p.Source && set.Slot == p.Slot &&
                set.Destination == p.Destination && p.FaceDown:
                if (p.Destination == DuelZone.Monster && p.Defense)
                { opcode = 0xCE; flags = 0x100; }
                else if (p.Destination == DuelZone.SpellTrap && !p.Defense)
                { opcode = 0xCF; flags = 0; nativeSlot += 5; }
                else return null;
                break;
            default: return null;
        }
        return [(ushort)(side | opcode), p.ResourceId, (ushort)((nativeSlot << 8) | p.HandIndex), flags];
    }
}
