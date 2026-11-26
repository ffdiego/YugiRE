namespace Joey.Core;

public sealed partial class DemoDuel
{
    private sealed record AttackResponseReservation(DemoAttackResponse Request,
        CardTarget Attacker, CardTarget? Defender, ActionOrigin DeclarationOrigin,
        IReadOnlyDictionary<int, CardTarget> Traps);

    private AttackResponseReservation? queuedAttackResponse;
    private bool admittingQueuedAttackResponse;

    public bool UsesQueuedAttackResponse(DemoAttackResponse response) =>
        ReferenceEquals(response, PendingAttackResponse) &&
        ReferenceEquals(response, queuedAttackResponse?.Request);

    private bool CanAdmitQueuedAttackResponse() =>
        !GameOver && Phase == DemoPhase.Battle && PendingAttackResponse is { } response &&
        UsesQueuedAttackResponse(response) && response.Attack.AttackingPlayer == ActivePlayer;

    // AttackValue/DefenseValue already include the active field contribution.
    private bool IsSupportedQueuedBattleFieldSpell(int side) =>
        FieldSpells[side] is null ||
        FieldSpells[side] is { ClassCode: 22 } field && FieldSpellRules.Supports(field.Id) &&
        TryCaptureCardTarget(side, DuelZone.FieldSpell, 0, out _);

    private bool IsSupportedQueuedBattleBackRow(int side, int slot) =>
        Spells[side, slot] is null ||
        Spells[side, slot] is { ClassCode: 21 or 22 } &&
        TryCaptureCardTarget(side, DuelZone.SpellTrap, slot, out _) &&
        (SpellFaceDown[side, slot] || equipment.Any(e => e.Owner == side && e.SourceZone == slot));

    private bool IsTrackedBattleEquipment(Equipment link) =>
        link.Card.ClassCode == 22 && EquipmentRules.SupportsQueued(link.Card.Id) &&
        ReferenceEquals(Spells[link.Owner, link.SourceZone], link.Card) &&
        !SpellFaceDown[link.Owner, link.SourceZone] &&
        TryCaptureCardTarget(link.Owner, DuelZone.SpellTrap, link.SourceZone, out _) &&
        ReferenceEquals(Monsters[link.TargetPlayer, link.TargetZone], link.TargetCard) &&
        TryCaptureCardTarget(link.TargetPlayer, DuelZone.Monster, link.TargetZone, out _);

    private bool IsQueuedAttackResponseTrap(int side, int slot) =>
        Spells[side, slot] is { ClassCode: 21 } trap && AttackTrapRules.Supports(trap.Id) &&
        SpellFaceDown[side, slot] && spellSetTurn[side, slot] < Turn;

    private bool HasQueuedAttackResponseTrap(int attacker) =>
        Enumerable.Range(0, 5).Any(slot => IsQueuedAttackResponseTrap(1 - attacker, slot));

    private CommandResult OpenAttackResponse(OpenAttackResponseCommand command)
    {
        var declaration = DescribeQueuedAttack(command.Attacker, command.Defender);
        if (queuedAttackResponse is not null || PendingAttackResponse is not null || declaration is null)
            return new(CommandOutcome.Rejected, "Attack response is no longer available.");

        var options = new List<DemoAttackTrapOption>();
        var traps = new Dictionary<int, CardTarget>();
        int responder = 1 - command.Side;
        for (int zone = 0; zone < 5; zone++)
        {
            if (!IsQueuedAttackResponseTrap(responder, zone) ||
                !TryCaptureCardTarget(responder, DuelZone.SpellTrap, zone, out var trap)) continue;
            traps.Add(zone, trap);
            options.Add(new(responder, zone, Spells[responder, zone]!));
        }
        if (options.Count == 0) return new(CommandOutcome.Skipped, "No tracked response remains.");

        var request = new DemoAttackResponse(declaration, options.AsReadOnly());
        queuedAttackResponse = new(request, command.Attacker, command.Defender, command.DeclarationOrigin, traps);
        PendingAttackResponse = request;
        return new(CommandOutcome.Applied);
    }

    private DemoAttackDeclaration? DescribeQueuedAttack(CardTarget attacker, CardTarget? defender)
    {
        if (defender is { } target)
            return TryPlanTargetedAttack(attacker, target, out var a, out var b, declared: true)
                ? new(attacker.Side, a.Slot, a.Card, b.Slot, b.Card) : null;
        return TryPlanQueuedDirectAttack(attacker, out int slot, out var card, out _, declared: true)
            ? new(attacker.Side, slot, card, -1, null) : null;
    }

    public bool TryQueueAttackResponse(DemoAttackResponse response, DemoAttackTrapOption? option,
        out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose an eligible Set Trap or pass the current queued attack response.";
        if (primitiveActionQueue is not { IsBusy: false } queue || !CanAdmitQueuedAttackResponse() ||
            queuedAttackResponse is not { } pending || !UsesQueuedAttackResponse(response)) return false;

        int responder = 1 - pending.Attacker.Side;
        CardTarget? source = null;
        if (option is not null)
        {
            if (option.Owner != responder || !response.EligibleTraps.Contains(option) ||
                !TryCaptureCardTarget(responder, DuelZone.SpellTrap, option.Zone, out var current) ||
                !pending.Traps.TryGetValue(option.Zone, out var original) || original != current ||
                !IsQueuedAttackResponseTrap(responder, option.Zone))
                return false;
            source = current;
        }

        admittingQueuedAttackResponse = true;
        try { queue.Enqueue(new ResolveAttackResponseCommand(responder, response, source), origin); }
        finally { admittingQueuedAttackResponse = false; }
        message = source is null ? "Attack response pass admitted." : "Attack Trap activation admitted.";
        return true;
    }

    private sealed partial class PrimitiveActionExecutor
    {
        private sealed record AttackTrapPayment(CardTarget Source, ushort ResourceId, int Attacker, int Damage);

        private AttackTrapPayment? attackTrapPayment;
        private bool attackTrapSpent;

        private CommandResult ResolveAttackResponse(ResolveAttackResponseCommand command,
            IActionCommandSink followups)
        {
            if (!duel.CanAdmitQueuedAttackResponse() || !duel.UsesQueuedAttackResponse(command.Request) ||
                duel.queuedAttackResponse is not { } pending || command.Side != 1 - pending.Attacker.Side ||
                attackTrapPayment is not null || wipe is not null || finishScheduled || followups is not IActionBatchSink { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected, "Attack response changed before execution.");

            BattleReservation? attacker = null;
            BattleReservation? defender = null;
            int damage = 0;
            bool attackValid = pending.Defender is { } target
                ? duel.TryPlanTargetedAttack(pending.Attacker, target, out attacker, out defender, declared: true) &&
                    attacker.Slot == pending.Request.Attack.AttackerZone && defender.Slot == pending.Request.Attack.TargetZone
                : duel.TryPlanQueuedDirectAttack(pending.Attacker, out int sourceSlot, out _, out damage, declared: true) &&
                    sourceSlot == pending.Request.Attack.AttackerZone;
            if (attackValid && attacker is not null)
                damage = Math.Clamp(duel.AttackValue(attacker.Target.Side, attacker.Slot), 0, ushort.MaxValue);
            if (command.Source is { } source)
            {
                if (!attackValid || !duel.TryResolveCardTarget(source, out int slot, out var trap) ||
                    !pending.Traps.TryGetValue(slot, out var original) || original != source ||
                    !duel.IsQueuedAttackResponseTrap(command.Side, slot))
                    return new(CommandOutcome.Rejected, "Attack Trap or attacker occurrence is no longer eligible.");
                if (trap.Id == AttackTrapRules.MirrorForce)
                {
                    var attack = pending.Request.Attack;
                    var targets = MonsterWipeRules.AttackPositionTargets(attack.AttackingPlayer,
                        duel.Monsters, duel.MonsterFaceDown, duel.DefensePosition);
                    if (targets.Count == 0 || !TryReserveMonsterWipe(source, trap.Id, targets, queue, attack: attack))
                        return new(CommandOutcome.Rejected, "Mirror Force targets or links are no longer tracked.");
                }
                else
                {
                    attackTrapPayment = new(source, trap.Id, pending.Attacker.Side,
                        trap.Id == AttackTrapRules.MagicCylinder ? damage : 0);
                    attackTrapSpent = false;
                    queue.EnqueueBatch([new SendToGraveyardCommand(command.Side, source),
                        new ResolveSpentAttackTrapCommand(command.Side, source)]);
                }
            }
            else if (attackValid)
            {
                if (attacker is not null && defender is not null)
                    duel.EnqueueTargetedBattleResolution(attacker, defender, queue);
                else if (damage > 0)
                    queue.Enqueue(new DamageCommand(command.Side, damage));
            }

            duel.queuedAttackResponse = null;
            duel.PendingAttackResponse = null;
            return attackValid ? new(CommandOutcome.Applied) :
                new(CommandOutcome.Skipped, "Declared combatant is no longer available.");
        }

        private CommandResult ResolveSpentAttackTrap(ResolveSpentAttackTrapCommand command, IActionCommandSink followups)
        {
            if (attackTrapPayment is not { } payment || payment.Source != command.Source)
                return new(CommandOutcome.Rejected, "No matching reserved attack Trap.");
            attackTrapPayment = null;
            if (!attackTrapSpent) return new(CommandOutcome.Skipped, "Attack Trap was not spent.");
            attackTrapSpent = false;
            bool endsBattle = payment.ResourceId == AttackTrapRules.NegateAttack;
            if (endsBattle)
                followups.Enqueue(new ChangeBattlePhaseCommand(payment.Attacker, duel.Turn, EnterBattle: false));
            else if (payment.Damage > 0)
                followups.Enqueue(new DamageCommand(payment.Attacker, payment.Damage));
            duel.LastAttackTrap = new(payment.ResourceId, command.Side, payment.Attacker, 0,
                AttackContinued: false, ReflectedDamage: payment.Damage, BattlePhaseEnded: endsBattle);
            return new(CommandOutcome.Applied);
        }
    }
}
