namespace Joey.Core;

// These are logical operations, not native opcode numbers. Admission (player,
// CPU, effect, lab) is separate from execution; a queue accepts admitted commands.
public enum ActionOrigin { Player, Cpu, Effect, Console, Test }
public enum DuelZone { Deck, Hand, Monster, SpellTrap, FieldSpell, Graveyard, Banished }

// The state adapter owns allocation and resolution of occurrence IDs. Never use
// resource ID, catalogue object reference, or current slot as an occurrence ID.
public readonly record struct CardOccurrenceId(long Value);
public readonly record struct CardTarget(CardOccurrenceId Occurrence, int Side, DuelZone Zone);

public abstract record DuelCommand(int Side);
public sealed record DrawCommand(int Side) : DuelCommand(Side); // exactly one card
public sealed record SummonCommand(int Side, CardTarget Source, int Slot) : DuelCommand(Side);
public sealed record SetCommand(int Side, CardTarget Source, DuelZone Destination, int Slot) : DuelCommand(Side);
public sealed record SendToGraveyardCommand(int Side, CardTarget Target) : DuelCommand(Side);
public sealed record DiscardCommand(int Side, CardTarget Target) : DuelCommand(Side);
public sealed record DestroyCommand(int Side, CardTarget Target) : DuelCommand(Side);
public sealed record DamageCommand(int Side, int Amount) : DuelCommand(Side);
public sealed record HealCommand(int Side, int Amount) : DuelCommand(Side);
public sealed record FlipCommand(int Side, CardTarget Target) : DuelCommand(Side);
public sealed record ChangePositionCommand(int Side, CardTarget Target, bool Defense) : DuelCommand(Side);
public sealed record EquipCommand(int Side, CardTarget Source, CardTarget Target, int SpellSlot) : DuelCommand(Side);

// Pilot orchestration only; A08 moves card-specific producers out of Core.
// Neither of these stages claims a one-to-one native opcode mapping.
internal sealed record ActivateDrawSpellCommand(int Side, CardTarget Source) : DuelCommand(Side);
internal sealed record ActivateLifePointSpellCommand(int Side, CardTarget Source) : DuelCommand(Side);
internal sealed record ActivateSpellTrapWipeCommand(int Side, CardTarget Source) : DuelCommand(Side);
internal sealed record ActivateMonsterWipeCommand(int Side, CardTarget Source) : DuelCommand(Side);
internal sealed record ActivateRaigekiCommand(int Side, CardTarget Source) : DuelCommand(Side);
internal sealed record FinishFieldRemovalCommand(int Side) : DuelCommand(Side);
internal sealed record PrepareNormalPlacementCommand(int Side, CardTarget Source, int Slot, bool FaceDown,
    CardTarget? FirstTribute, CardTarget? SecondTribute) : DuelCommand(Side);
internal sealed record TributeCommand(int Side, CardTarget Target) : DuelCommand(Side);
internal sealed record FinishNormalPlacementCommand(int Side, CardTarget Target, bool FaceDown) : DuelCommand(Side);
internal sealed record ResolvePositionCommand(int Side, CardTarget Target, bool WasFaceDown, bool WasInDefense) : DuelCommand(Side);
internal sealed record FinishFlipBatchCommand(int Side, CardTarget Target, bool WasInDefense, bool DeckOut0, bool DeckOut1) : DuelCommand(Side);
internal sealed record RecordFlipDrawDecisionCommand(int Side, long DecisionId, EffectDecisionAnswer Answer) : DuelCommand(Side);

internal static class DuelCommandValidation
{
    public static void Validate(DuelCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Side is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(command), "Invalid side.");
        switch (command)
        {
            case RecordFlipDrawDecisionCommand record when record.DecisionId > 0 && Enum.IsDefined(record.Answer):
            case DrawCommand:
            case FinishFieldRemovalCommand:
                break;
            case EquipCommand equip when equip.SpellSlot is >= 0 and < 5:
                Target(equip.Source, equip.Side, DuelZone.Hand, DuelZone.SpellTrap);
                if (equip.Target.Side is not (0 or 1)) throw new ArgumentException("Invalid equipment target side.");
                Target(equip.Target, equip.Target.Side, DuelZone.Monster);
                break;
            case ActivateDrawSpellCommand drawSpell:
                Target(drawSpell.Source, drawSpell.Side, DuelZone.Hand, DuelZone.SpellTrap);
                break;
            case ActivateLifePointSpellCommand lp:
                Target(lp.Source, lp.Side, DuelZone.Hand, DuelZone.SpellTrap);
                break;
            case ActivateSpellTrapWipeCommand spellTrapWipe:
                Target(spellTrapWipe.Source, spellTrapWipe.Side, DuelZone.Hand, DuelZone.SpellTrap);
                break;
            case ActivateMonsterWipeCommand monsterWipe:
                Target(monsterWipe.Source, monsterWipe.Side, DuelZone.Hand, DuelZone.SpellTrap);
                break;
            case ActivateRaigekiCommand activate:
                Target(activate.Source, activate.Side, DuelZone.Hand);
                break;
            case PrepareNormalPlacementCommand prepare when prepare.Slot is >= 0 and < 5:
                Target(prepare.Source, prepare.Side, DuelZone.Hand);
                if (prepare.FirstTribute is { } first) Target(first, prepare.Side, DuelZone.Monster);
                if (prepare.SecondTribute is { } second) Target(second, prepare.Side, DuelZone.Monster);
                break;
            case TributeCommand tribute:
                Target(tribute.Target, tribute.Side, DuelZone.Monster);
                break;
            case FinishNormalPlacementCommand finish:
                Target(finish.Target, finish.Side, DuelZone.Monster);
                break;
            case ResolvePositionCommand position:
                Target(position.Target, position.Side, DuelZone.Monster);
                break;
            case FinishFlipBatchCommand jar:
                Target(jar.Target, jar.Side, DuelZone.Monster);
                break;
            case DamageCommand damage when damage.Amount > 0:
            case HealCommand { Amount: > 0 }:
                break;
            case SummonCommand summon when summon.Slot is >= 0 and < 5:
                Target(summon.Source, summon.Side, DuelZone.Hand);
                break;
            case SetCommand set when
                (set.Destination is DuelZone.Monster or DuelZone.SpellTrap && set.Slot is >= 0 and < 5)
                || (set.Destination == DuelZone.FieldSpell && set.Slot == 0):
                Target(set.Source, set.Side, DuelZone.Hand);
                break;
            case SendToGraveyardCommand send:
                Target(send.Target, send.Side, DuelZone.Deck, DuelZone.Hand, DuelZone.Monster,
                    DuelZone.SpellTrap, DuelZone.FieldSpell, DuelZone.Banished);
                break;
            case DiscardCommand discard:
                Target(discard.Target, discard.Side, DuelZone.Hand);
                break;
            case DestroyCommand destroy:
                Target(destroy.Target, destroy.Side, DuelZone.Monster, DuelZone.SpellTrap, DuelZone.FieldSpell);
                break;
            case FlipCommand flip:
                Target(flip.Target, flip.Side, DuelZone.Monster);
                break;
            case ChangePositionCommand position:
                Target(position.Target, position.Side, DuelZone.Monster);
                break;
            default:
                throw new ArgumentException("Unknown command or invalid arguments.", nameof(command));
        }
    }

    private static void Target(CardTarget target, int side, params DuelZone[] allowed)
    {
        if (target.Occurrence.Value <= 0 || target.Side != side || !allowed.Contains(target.Zone))
            throw new ArgumentException("Invalid occurrence, side or expected zone.", nameof(target));
    }
}
