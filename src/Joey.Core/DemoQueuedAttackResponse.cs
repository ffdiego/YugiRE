namespace Joey.Core;

public sealed partial class DemoDuel
{
    private sealed record DirectAttackResponseReservation(DemoAttackResponse Request,
        CardTarget Attacker, IReadOnlyDictionary<int, CardTarget> Traps);

    private DirectAttackResponseReservation? queuedDirectResponse;
    private bool admittingQueuedAttackResponse;

    public bool UsesQueuedAttackResponse(DemoAttackResponse response) =>
        ReferenceEquals(response, PendingAttackResponse) &&
        ReferenceEquals(response, queuedDirectResponse?.Request);

    private bool CanAdmitQueuedAttackResponse() =>
        !GameOver && Phase == DemoPhase.Battle && PendingAttackResponse is { } response &&
        UsesQueuedAttackResponse(response) && response.Attack.AttackingPlayer == ActivePlayer;

    private bool IsQueuedDirectResponseTrap(int side, int slot) =>
        Spells[side, slot] is { ClassCode: 21 } trap && AttackTrapRules.Supports(trap.Id) &&
        SpellFaceDown[side, slot] && spellSetTurn[side, slot] < Turn;

    private bool HasQueuedDirectResponse(int attacker) =>
        Enumerable.Range(0, 5).Any(slot => IsQueuedDirectResponseTrap(1 - attacker, slot));

    private CommandResult OpenDirectAttackResponse(OpenDirectAttackResponseCommand command)
    {
        if (queuedDirectResponse is not null || PendingAttackResponse is not null ||
            !TryPlanQueuedDirectAttack(command.Attacker, out int slot, out var attacker, out _, declared: true))
            return new(CommandOutcome.Rejected, "Direct attack response is no longer available.");

        var options = new List<DemoAttackTrapOption>();
        var traps = new Dictionary<int, CardTarget>();
        int responder = 1 - command.Side;
        for (int zone = 0; zone < 5; zone++)
        {
            if (!IsQueuedDirectResponseTrap(responder, zone) ||
                !TryCaptureCardTarget(responder, DuelZone.SpellTrap, zone, out var trap)) continue;
            traps.Add(zone, trap);
            options.Add(new(responder, zone, Spells[responder, zone]!));
        }
        if (options.Count == 0) return new(CommandOutcome.Skipped, "No tracked response remains.");

        var declaration = new DemoAttackDeclaration(command.Side, slot, attacker, -1, null);
        var request = new DemoAttackResponse(declaration, options.AsReadOnly());
        queuedDirectResponse = new(request, command.Attacker, traps);
        PendingAttackResponse = request;
        return new(CommandOutcome.Applied);
    }

    public bool TryQueueAttackResponse(DemoAttackResponse response, DemoAttackTrapOption? option,
        out string message, ActionOrigin origin = ActionOrigin.Player)
    {
        message = "Choose an eligible Set Trap or pass the current queued attack response.";
        if (primitiveActionQueue is not { IsBusy: false } queue || !CanAdmitQueuedAttackResponse() ||
            queuedDirectResponse is not { } pending || !UsesQueuedAttackResponse(response)) return false;

        int responder = 1 - pending.Attacker.Side;
        CardTarget? source = null;
        if (option is not null)
        {
            if (option.Owner != responder || !response.EligibleTraps.Contains(option) ||
                !TryCaptureCardTarget(responder, DuelZone.SpellTrap, option.Zone, out var current) ||
                !pending.Traps.TryGetValue(option.Zone, out var original) || original != current ||
                !IsQueuedDirectResponseTrap(responder, option.Zone))
                return false;
            source = current;
        }

        admittingQueuedAttackResponse = true;
        try { queue.Enqueue(new ResolveDirectAttackResponseCommand(responder, response, source), origin); }
        finally { admittingQueuedAttackResponse = false; }
        message = source is null ? "Attack response pass admitted." : "Attack Trap activation admitted.";
        return true;
    }

    private sealed partial class PrimitiveActionExecutor
    {
        private sealed record AttackTrapPayment(CardTarget Source, ushort ResourceId, int Attacker, int Damage);

        private AttackTrapPayment? attackTrapPayment;
        private bool attackTrapSpent;

        private CommandResult ResolveDirectAttackResponse(ResolveDirectAttackResponseCommand command,
            IActionCommandSink followups)
        {
            if (!duel.CanAdmitQueuedAttackResponse() || !duel.UsesQueuedAttackResponse(command.Request) ||
                duel.queuedDirectResponse is not { } pending || command.Side != 1 - pending.Attacker.Side ||
                attackTrapPayment is not null || wipe is not null || finishScheduled || followups is not IActionBatchSink { PendingCount: 0 } queue)
                return new(CommandOutcome.Rejected, "Attack response changed before execution.");

            bool attackerValid = duel.TryPlanQueuedDirectAttack(pending.Attacker, out _, out _, out int damage,
                declared: true);
            if (command.Source is { } source)
            {
                if (!attackerValid || !duel.TryResolveCardTarget(source, out int slot, out var trap) ||
                    !pending.Traps.TryGetValue(slot, out var original) || original != source ||
                    !duel.IsQueuedDirectResponseTrap(command.Side, slot))
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
            else if (attackerValid && damage > 0)
                queue.Enqueue(new DamageCommand(command.Side, damage));

            duel.queuedDirectResponse = null;
            duel.PendingAttackResponse = null;
            return attackerValid ? new(CommandOutcome.Applied) :
                new(CommandOutcome.Skipped, "Declared attacker is no longer available.");
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
