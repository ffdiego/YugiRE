using System.Text.Json;
using Joey.Core;

internal static class FusionChecks
{
    private static Card Monster(ushort id, bool fusion = false) =>
        new(id, id, $"Monster {id}", "", (1u << 20) | (4u << 25) | (fusion ? 2u << 18 : 0) | (50u << 9));

    public static void Run(Action<bool, string> check)
    {
        var spell = new Card(1003, 1003, "Polymerization", "", 22u << 20);
        Card baby = Monster(3), wizard = Monster(15), filler = Monster(25);
        Card thousand = Monster(68, fusion: true), dragon = Monster(424), twin = Monster(612, fusion: true);
        Card?[] empty = new Card?[5];
        var duel = DemoDuel.FromPartialObservation(0, [baby, filler, spell], [null, wizard, null, null, null],
            empty, 8000, side0FusionDeck: [thousand]);
        duel.MonsterFaceDown[0, 1] = true;
        var summons = new List<DemoSpecialSummon>();
        duel.MonsterSpecialSummoned += summons.Add;
        duel.MonsterDestroyed += _ => throw new Exception("Fusion material must not be treated as destroyed.");
        duel.MonsterTributed += _ => throw new Exception("Fusion material must not be treated as a Tribute.");

        check(duel.CanActivateFromHand(2) && duel.Play(2, out _) &&
              duel.PendingFusionSummon is { Options.Count: 1 } && !duel.EndTurn(out _),
            "Fusion: opening selection blocks other actions without committing Polymerization");
        check(duel.CancelFusionSummon(out _) && duel.Hand.SequenceEqual([baby, filler, spell]) &&
              duel.FusionDeck(0).Single() == thousand && duel.Monsters[0, 1] == wizard &&
              duel.Graveyard(0).Count == 0 && duel.Play(2, out _),
            "Fusion: cancel preserves hand, field, Fusion Deck and Graveyard");
        var choice = duel.PendingFusionSummon!.Options.Single();
        Card physicalWizard = duel.Monsters[0, 1]!;
        duel.Monsters[0, 1] = wizard with { };
        check(!duel.ResolveFusionSummon(0, choice.SuggestedMaterials, out _) && duel.Hand.Count == 3,
            "Fusion: confirmation rejects a material occurrence replaced after selection");
        duel.Monsters[0, 1] = physicalWizard;
        check(duel.ResolveFusionSummon(0, choice.SuggestedMaterials, out _) && duel.Hand.SequenceEqual([filler]) &&
              duel.Monsters[0, 0] == thousand && !duel.DefensePosition[0, 0] &&
              duel.Monsters[0, 1] is null && !duel.MonsterFaceDown[0, 1] &&
              duel.Graveyard(0).SequenceEqual([spell, baby, wizard]) && duel.FusionDeck(0).Count == 0 &&
              summons.Single().Card == thousand && duel.CanSummonFromHand(0),
            "Thousand Dragon: takes one hand and one face-down field material and preserves the Normal Summon");

        var repeat = DemoDuel.FromPartialObservation(0, [spell, dragon, dragon],
            [filler, filler, dragon, filler, filler], empty, 8000, side0FusionDeck: [twin, twin]);
        check(repeat.SetFromHand(0, out _) && repeat.ActivateSetSpell(0, out _) &&
              repeat.PendingFusionSummon is { FromSet: true, Options.Count: 2 },
            "Fusion: a same-turn Set can activate on a full board if a material frees a zone");
        var second = repeat.PendingFusionSummon!.Options[1];
        var field = second.Materials.Single(m => m.Zone == FusionMaterialZone.Field);
        var hand = second.Materials.Where(m => m.Zone == FusionMaterialZone.Hand).ToArray();
        check(!repeat.ResolveFusionSummon(1, [field, field], out _) &&
              !repeat.ResolveFusionSummon(1, hand, out _) &&
              repeat.FusionDeck(0).Count == 2 && repeat.Graveyard(0).Count == 0 && repeat.Spells[0, 0] == spell,
            "Twin-Headed Thunder Dragon: rejects reusing one occurrence or paying a full-board Fusion entirely from the hand");
        check(repeat.ResolveFusionSummon(1, [field, hand[1]], out _, defense: true) &&
              repeat.Hand.Single() == dragon && repeat.Monsters[0, 2] == twin && repeat.DefensePosition[0, 2] &&
              !repeat.MonsterFaceDown[0, 2] && repeat.Graveyard(0).SequenceEqual([spell, dragon, dragon]) &&
              repeat.FusionDeck(0).Single() == twin && repeat.Spells[0, 0] is null,
            "Twin-Headed Thunder Dragon: consumes two distinct copies sharing a catalog object, in face-up defense");

        var onlyOpposing = DemoDuel.FromPartialObservation(0, [spell, baby], empty,
            [wizard, null, null, null, null], 8000, side0FusionDeck: [thousand]);
        check(!onlyOpposing.CanActivateFromHand(0) && !onlyOpposing.Play(0, out _),
            "Fusion: the opponent's monsters do not supply materials");
        var missingResult = DemoDuel.FromPartialObservation(0, [spell, baby, wizard], empty, empty, 8000);
        check(!missingResult.CanActivateFromHand(0) && !missingResult.Play(0, out _),
            "Fusion: the result must be in the controller's Fusion Deck");

        Card witch = Monster(573), faith = Monster(611), king = Monster(494, fusion: true);
        var trigger = DemoDuel.FromPartialObservation(0, [spell, faith], [witch, null, null, null, null],
            empty, 8000, side0Deck: [baby], side0FusionDeck: [king]);
        check(trigger.Play(0, out _) &&
              trigger.ResolveFusionSummon(0, trigger.PendingFusionSummon!.Options[0].SuggestedMaterials, out _) &&
              trigger.Monsters[0, 0] == king && trigger.PendingCardSelection?.EffectResourceId == witch.Id,
            "Fusion: Witch of the Black Forest's field-to-Graveyard effect opens after Musician King is placed");

        var cpuDuel = DemoDuel.FromPartialObservation(1, [spell, baby, wizard], empty, empty, 8000,
            side1Deck: [filler], side1FusionDeck: [thousand]);
        var cpu = new CpuDuelController(1);
        check(cpuDuel.Draw(out _) && cpu.Step(cpuDuel) is { Action: CpuAction.Selection, Status: CpuStepStatus.Acted } &&
              cpu.Step(cpuDuel) is { Action: CpuAction.Spell, Status: CpuStepStatus.Acted,
                  HandIndex: 0, AdditionalSpentHandIndices.Count: 2 } &&
              cpuDuel.Monsters[1, 0] == thousand && cpuDuel.Hand.Single() == filler,
            "Fusion: the CPU commits both selections and reports all spent hand indices");

        Card penguin = Monster(FlipEffectRules.PenguinSoldier);
        var bounce = DemoDuel.FromPartialObservation(0, [], [penguin, thousand, null, null, null], empty, 8000);
        bounce.MonsterFaceDown[0, 0] = true;
        check(bounce.ChangePosition(0, out _) && bounce.ResolveFlipTarget(0, 1, out _) &&
              bounce.FinishFlipTargets(out _) && bounce.Hand.Count == 0 && bounce.FusionDeck(0).Single() == thousand,
            "Fusion: a Fusion Monster returned to the hand goes to the Fusion Deck");

        Card jar = Monster(FlipEffectRules.MorphingJar2);
        var shuffle = DemoDuel.FromPartialObservation(0, [], [jar, null, null, null, null],
            [thousand, null, null, null, null], 8000, side1Deck: [filler]);
        shuffle.MonsterFaceDown[0, 0] = true;
        check(shuffle.ChangePosition(0, out _) && shuffle.FusionDeck(1).Single() == thousand &&
              shuffle.DeckSnapshot(1).Single() == filler && shuffle.LastMorphingJar2[1].Excavated == 0,
            "Fusion: Morphing Jar #2 returns the Fusion Monster without adding to the Main Deck quota");

        CheckQueuedFusion(check, spell, baby, wizard, thousand, filler);
        CheckSubstitutes(check, spell, baby, wizard, thousand, filler);
        CheckFusionGate(check, baby, wizard, thousand, filler);
    }

    private sealed class FusionGateProbe(DemoDuel duel) : ICommandPresentationGate
    {
        public bool Release { get; set; }
        public bool CanAdvance => true;
        public List<DuelActionPresentation> Cues { get; } = [];
        public void OnStarting(QueuedDuelCommand command)
        {
            if (duel.DescribeActionPresentation(command.Command) is { Kind: ActionPresentationKind.Activate } cue)
                Cues.Add(cue);
        }
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) => Release;
    }

    private static void CheckQueuedFusion(Action<bool, string> check, Card spell, Card baby, Card wizard,
        Card thousand, Card filler)
    {
        Card?[] empty = new Card?[5];
        Card gateCard = new(1547, 1547, "Fusion Gate", "", 22u << 20);
        foreach (int side in new[] { 0, 1 })
        foreach (int sourceKind in new[] { 0, 1, 2 })
        foreach (bool defense in new[] { false, true })
        {
            bool gate = sourceKind == 2, set = sourceKind == 1;
            var d = DemoDuel.FromPartialObservation(side, [gate ? gateCard : spell, baby, filler],
                side == 0 ? [wizard, null, null, null, null] : empty,
                side == 1 ? [wizard, null, null, null, null] : empty, 8000,
                side0FusionDeck: side == 0 ? [thousand, thousand] : null,
                side1FusionDeck: side == 1 ? [thousand, thousand] : null);
            d.MonsterFaceDown[side, 0] = true;
            if (gate) check(d.Play(0, out _) && d.BeginFusionGate(side, out _), "Queued Fusion Gate fixture");
            else if (set) check(d.SetFromHand(0, out _) && d.ActivateSetSpell(0, out _), "Queued Fusion Set fixture");
            else check(d.Play(0, out _), "Queued Fusion hand fixture");
            var request = d.PendingFusionSummon!;
            var choice = request.Options[1];
            Card physicalResult = d.FusionDeck(side)[1];
            Card unselectedResult = d.FusionDeck(side)[0];
            var probe = new FusionGateProbe(d);
            var q = d.CreatePrimitiveActionQueue(probe);
            var originalHand = d.Hand.ToArray();
            Card physicalWizard = d.Monsters[side, 0]!;
            check(d.TryCaptureCardTarget(side, DuelZone.FusionDeck, 1, out var resultTarget) &&
                d.TryCaptureCardTarget(side, DuelZone.FusionDeck, 0, out var otherTarget) &&
                resultTarget != otherTarget, "Fusion Deck tracks separate physical copies");
            check(!d.TryQueueFusionSummon(request, 1, [choice.SuggestedMaterials[0], choice.SuggestedMaterials[0]], out _)
                && q.Trace.Count == 0, "Queued Fusion rejects duplicate material");
            d.Monsters[side, 0] = wizard with { };
            check(!d.TryQueueFusionSummon(request, 1, choice.SuggestedMaterials, out _) &&
                q.Trace.Count == 0 && d.Hand.SequenceEqual(originalHand), "Queued Fusion rejects stale same-ID material");
            d.Monsters[side, 0] = physicalWizard;
            int destructions = 0, tributes = 0, summons = 0;
            d.MonsterDestroyed += _ => destructions++;
            d.MonsterTributed += _ => tributes++;
            d.MonsterSpecialSummoned += _ => summons++;
            check(d.TryQueueFusionSummon(request, 1, choice.SuggestedMaterials, out _, defense) &&
                q.IsBusy && d.PendingFusionSummon == request && d.Hand.SequenceEqual(originalHand) &&
                d.Graveyard(side).Count == 0 && d.Banished(side).Count == 0 &&
                !d.CancelFusionSummon(out _) && !d.TryQueueFusionSummon(request, 1, choice.SuggestedMaterials, out _),
                "Queued Fusion admission is read-only and blocks competing input");
            q.Tick(); q.Tick();
            check(q.IsBusy && d.PendingFusionSummon is null && d.Hand.SequenceEqual(originalHand) &&
                d.Monsters[side, 0] == wizard && d.FusionDeck(side).Count == 2 &&
                d.Graveyard(side).Count == 0 && d.Banished(side).Count == 0 && probe.Cues.Count == 1 &&
                probe.Cues[0].Destination == (gate ? DuelZone.FieldSpell : DuelZone.SpellTrap),
                "Queued Fusion activation gate precedes spending and all material moves");
            probe.Release = true;
            for (int tick = 0; tick < 100 && q.IsBusy; tick++) q.Tick();
            check(!q.IsBusy && q.Fault is null && d.Hand.SequenceEqual([filler]) &&
                ReferenceEquals(d.Monsters[side, 0], physicalResult) &&
                ReferenceEquals(d.FusionDeck(side).Single(), unselectedResult) &&
                d.DefensePosition[side, 0] == defense && !d.MonsterFaceDown[side, 0] &&
                destructions == 0 && tributes == 0 && summons == 1 &&
                (gate ? d.FieldSpells[side] == gateCard && d.Graveyard(side).Count == 0 &&
                    d.Banished(side).Count == 2 : d.Graveyard(side).Count == 3 &&
                    d.Banished(side).Count == 0 && d.Spells[side, 0] is null),
                "Queued Fusion preserves committed state, result occurrence and material semantics");
            var executed = q.Trace.Where(t => t.Stage == ActionTraceStage.Executed).ToArray();
            check(executed.All(t => t.Result?.Outcome == CommandOutcome.Applied) &&
                executed.Count(t => t.Entry.Command is SendToGraveyardCommand) == (gate ? 0 : 3) &&
                executed.Count(t => t.Entry.Command is BanishCommand) == (gate ? 2 : 0) &&
                executed.Count(t => t.Entry.Command is SpecialSummonCommand) == 1 &&
                executed[^2].Entry.Command is SpecialSummonCommand { Source: var summoned } && summoned == resultTarget &&
                executed[^1].Entry.Command.GetType().Name == "FinishFusionSummonCommand",
                "Queued Fusion moves one occurrence per command and finalizes after placement");
        }

        // A source/target replacement between admission and first execution
        // must reject the reservation without spending a single card.
        var stale = DemoDuel.FromPartialObservation(0, [spell, baby], [wizard, null, null, null, null],
            empty, 8000, side0FusionDeck: [thousand]);
        check(stale.Play(0, out _), "Queued Fusion stale fixture");
        var staleRequest = stale.PendingFusionSummon!;
        var staleChoice = staleRequest.Options.Single();
        var sq = stale.CreatePrimitiveActionQueue();
        check(stale.TryQueueFusionSummon(staleRequest, 0, staleChoice.SuggestedMaterials, out _),
            "Queued Fusion stale admission");
        stale.Monsters[0, 0] = wizard with { };
        for (int tick = 0; tick < 10 && sq.IsBusy; tick++) sq.Tick();
        check(!sq.IsBusy && sq.Fault is null && stale.PendingFusionSummon == staleRequest &&
            stale.Hand.Count == 2 && stale.Graveyard(0).Count == 0 && stale.FusionDeck(0).Count == 1,
            "Queued Fusion stale reservation fails before spending");

        foreach (int side in new[] { 0, 1 })
        {
            var d = DemoDuel.FromPartialObservation(side, [spell, baby, wizard], empty, empty, 8000,
                side0Deck: side == 0 ? [filler] : null, side1Deck: side == 1 ? [filler] : null,
                side0FusionDeck: side == 0 ? [thousand] : null, side1FusionDeck: side == 1 ? [thousand] : null);
            var q = d.CreatePrimitiveActionQueue();
            var cpu = new CpuDuelController(side);
            check(cpu.Step(d, queueTurnActions: true).Action == CpuAction.Draw, "Queued CPU Fusion draw");
            for (int tick = 0; tick < 100 && q.IsBusy; tick++) q.Tick();
            check(cpu.Step(d, queueTurnActions: true).Action == CpuAction.Selection &&
                d.PendingFusionSummon is not null, "Queued CPU Fusion opens existing selection");
            check(cpu.Step(d, queueTurnActions: true) is { Status: CpuStepStatus.Acted, Action: CpuAction.Spell, Card: null } &&
                q.IsBusy && d.Hand.Count == 4 && d.Monsters[side, 0] is null &&
                q.Trace.Last(t => t.Stage == ActionTraceStage.Admitted).Entry.Origin == ActionOrigin.Cpu,
                "Queued CPU Fusion uses identical producer without legacy presentation metadata");
            for (int tick = 0; tick < 100 && q.IsBusy; tick++) q.Tick();
            check(!q.IsBusy && q.Fault is null && d.Monsters[side, 0] == thousand && d.Hand.Single() == filler,
                "Queued CPU Fusion resolves on the scheduler");
        }

        Card witch = Monster(573), faith = Monster(611), king = Monster(494, fusion: true);
        var trigger = DemoDuel.FromPartialObservation(0, [spell, faith], [witch, null, null, null, null],
            empty, 8000, side0Deck: [baby], side0FusionDeck: [king]);
        check(trigger.Play(0, out _), "Queued Fusion Witch fixture");
        var tq = trigger.CreatePrimitiveActionQueue();
        var pending = trigger.PendingFusionSummon!;
        check(trigger.TryQueueFusionSummon(pending, 0, pending.Options[0].SuggestedMaterials, out _),
            "Queued Fusion Witch admitted");
        for (int tick = 0; tick < 100 && tq.IsBusy; tick++)
        {
            tq.Tick();
            check(trigger.PendingCardSelection is null || trigger.Monsters[0, 0] == king,
                "Queued Fusion field trigger opens only after placement");
        }
        check(!tq.IsBusy && trigger.PendingCardSelection?.EffectResourceId == witch.Id &&
            trigger.Monsters[0, 0] == king, "Queued Fusion preserves deferred Witch search");

        // An opposing Gate remains its owner's physical card. Only the
        // active player's materials and Fusion Deck are paid.
        foreach (int side in new[] { 0, 1 })
        {
            int enemy = 1 - side;
            Card first = Monster(159) with { Properties = 15u << 20 | 4u << 25 };
            Card second = Monster(160), result = Monster(42, fusion: true);
            Card sword = new(300, 300, "Legendary Sword", "", 22u << 20);
            var d = DemoDuel.FromPartialObservation(enemy, [gateCard],
                side == 0 ? [first, null, null, null, null] : empty,
                side == 1 ? [first, null, null, null, null] : empty, 8000,
                equipmentContextResolved: true,
                side0Deck: side == 0 ? [sword, second] : null,
                side1Deck: side == 1 ? [sword, second] : null,
                side0FusionDeck: side == 0 ? [result] : null,
                side1FusionDeck: side == 1 ? [result] : null);
            check(d.Play(0, out _) && d.EndTurn(out _) && d.Draw(out _),
                "Queued opposing Gate linked fixture: activate and draw");
            check(d.Equip(0, side, 0, out _), "Queued opposing Gate linked fixture: equip");
            check(d.EndTurn(out _) && d.EndTurn(out _) && d.Draw(out _) && d.BeginFusionGate(enemy, out _),
                "Queued opposing Gate linked fixture: second material and selection");
            var request = d.PendingFusionSummon!;
            var q = d.CreatePrimitiveActionQueue();
            check(d.TryQueueFusionSummon(request, 0, request.Options.Single().SuggestedMaterials, out _),
                "Queued opposing Gate admission");
            for (int tick = 0; tick < 100 && q.IsBusy; tick++) q.Tick();
            var commands = q.Trace.Where(t => t.Stage == ActionTraceStage.Executed).Select(t => t.Entry.Command).ToArray();
            check(!q.IsBusy && q.Fault is null && d.FieldSpells[enemy] == gateCard &&
                d.Monsters[side, 0] == result && d.Banished(side).Count == 2 &&
                d.Banished(enemy).Count == 0 && d.Graveyard(side).Single() == sword &&
                d.EquipmentCards.Count == 0 && d.Spells[side, 0] is null &&
                commands.Count(c => c is DestroyCommand) == 1 &&
                Array.FindIndex(commands, c => c is DestroyCommand) < Array.FindIndex(commands, c => c is SpecialSummonCommand),
                "Queued Fusion Gate removes linked equipment separately and preserves the opposing Gate");
        }

        // No general permission to move a Fusion result or banish a field
        // monster follows from extending occurrence zones/command validation.
        var bare = DemoDuel.FromPartialObservation(0, [], [wizard, null, null, null, null],
            empty, 8000, side0FusionDeck: [thousand]);
        var bq = bare.CreatePrimitiveActionQueue();
        check(bare.TryCaptureCardTarget(0, DuelZone.FusionDeck, 0, out var bareResult) &&
            bare.TryCaptureCardTarget(0, DuelZone.Monster, 0, out var bareField),
            "Queued Fusion unauthorized fixture");
        bare.TryCaptureCardTarget(0, DuelZone.Monster, 0, out var fieldTarget);
        bq.Enqueue(new SpecialSummonCommand(0, bareResult, 1));
        bq.Enqueue(new BanishCommand(0, fieldTarget));
        for (int tick = 0; tick < 10 && bq.IsBusy; tick++) bq.Tick();
        check(!bq.IsBusy && bare.FusionDeck(0).Count == 1 && bare.Monsters[0, 0] == wizard &&
            bare.Monsters[0, 1] is null && bare.Banished(0).Count == 0 &&
            bq.Trace.Where(t => t.Stage == ActionTraceStage.Executed).All(t => t.Result?.Outcome == CommandOutcome.Rejected),
            "Queued Fusion rejects bare Fusion Deck summon or field banishment");
    }

    private static void CheckFusionGate(Action<bool, string> check, Card baby, Card wizard, Card thousand, Card filler)
    {
        var gate = new Card(1547, 1547, "Fusion Gate", "", 22u << 20);
        foreach (int side in new[] { 0, 1 })
        {
            var queued = DemoDuel.FromPartialObservation(side, [gate, baby, wizard],
                new Card?[5], new Card?[5], 8000,
                side0Deck: side == 0 ? [filler] : null, side1Deck: side == 1 ? [filler] : null,
                side0FusionDeck: side == 0 ? [thousand] : null, side1FusionDeck: side == 1 ? [thousand] : null);
            var q = queued.CreatePrimitiveActionQueue();
            var queuedCpu = new CpuDuelController(side);
            void Drain()
            {
                for (int i = 0; i < 100 && q.IsBusy; i++) q.Tick();
                if (q.IsBusy) throw new InvalidOperationException("Fusion Gate queue did not drain.");
            }
            check(queuedCpu.Step(queued, queueTurnActions: true).Action == CpuAction.Draw,
                "C12 CPU Fusion Gate: turn draw");
            Drain();
            int before = q.Trace.Count;
            check(queuedCpu.Step(queued, queueTurnActions: true) is { Status: CpuStepStatus.Acted,
                Action: CpuAction.Spell, HandIndex: 0 } && queued.FieldSpells[side] is null &&
                queued.Hand.Count == 4 && q.IsBusy, "C12 CPU Fusion Gate: admission only");
            var command = q.Trace.Skip(before).First(x => x.Stage == ActionTraceStage.Admitted).Entry;
            check(command.Origin == ActionOrigin.Cpu &&
                command.Command.GetType().Name == "PrepareFieldSpellActivationCommand",
                "C12 CPU Fusion Gate: same producer as player");
            Drain();
            check(queued.FieldSpells[side]?.Id == gate.Id && queued.Hand.Count == 3 &&
                queued.CanUseFusionGate(side) && queued.PendingFusionSummon is null,
                "C12 CPU Fusion Gate: activation only, fusion remains separate");
            check(queuedCpu.Step(queued, queueTurnActions: true).Action == CpuAction.Selection &&
                queued.PendingFusionSummon is not null, "C12 CPU Fusion Gate: existing fusion strategy preserved");
        }
        Card witch = Monster(573), faith = Monster(611), king = Monster(494, fusion: true);
        Card?[] empty = new Card?[5];
        var duel = DemoDuel.FromPartialObservation(0, [gate, faith, baby, wizard],
            [witch, null, null, null, null], empty, 8000, side0Deck: [filler], side0FusionDeck: [king, thousand]);
        check(duel.SetFromHand(0, out _) && !duel.CanUseFusionGate(0) &&
              !duel.BeginFusionGate(0, out _) && duel.ActivateSetFieldSpell(out _) &&
              duel.PendingFusionSummon is null && duel.CanUseFusionGate(0) && duel.BeginFusionGate(0, out _),
            "Fusion Gate: a set Gate cannot fuse until activated in its Field Zone");
        check(duel.CancelFusionSummon(out _) && duel.FieldSpells[0] == gate && duel.Banished(0).Count == 0 &&
              duel.BeginFusionGate(0, out _),
            "Fusion Gate: cancel preserves Gate and its materials");
        var first = duel.PendingFusionSummon!.Options.Single(option => option.Result == king);
        Card physicalGate = duel.FieldSpells[0]!;
        duel.FieldSpells[0] = gate with { };
        check(!duel.ResolveFusionSummon(first.DeckIndex, first.SuggestedMaterials, out _) &&
              duel.Banished(0).Count == 0 && duel.Hand.Count == 3,
            "Fusion Gate: a replacement in the same Field Zone invalidates the pending source");
        duel.FieldSpells[0] = physicalGate;
        check(duel.ResolveFusionSummon(first.DeckIndex, first.SuggestedMaterials, out _) &&
              duel.Monsters[0, 0] == king && duel.FieldSpells[0] == gate &&
              duel.Banished(0).SequenceEqual([witch, faith]) && duel.Graveyard(0).Count == 0 &&
              duel.PendingCardSelection is null && duel.Hand.SequenceEqual([baby, wizard]),
            "Fusion Gate: field and hand materials are banished without Witch's Graveyard trigger");
        check(duel.BeginFusionGate(0, out _) &&
              duel.ResolveFusionSummon(0, duel.PendingFusionSummon!.Options[0].SuggestedMaterials, out _) &&
              duel.Monsters[0, 1] == thousand && duel.Banished(0).Count == 4 &&
              duel.FieldSpells[0] == gate && !duel.CanUseFusionGate(0),
            "Fusion Gate: the spell remains available for a second Fusion in the same Main Phase");

        var opposing = DemoDuel.FromPartialObservation(1, [baby, wizard], empty, empty, 8000,
            side1Deck: [filler], side1FusionDeck: [thousand]);
        opposing.FieldSpells[0] = gate;
        var cpu = new CpuDuelController(1);
        check(opposing.Draw(out _) && cpu.Step(opposing) is { Action: CpuAction.Selection } &&
              cpu.Step(opposing) is { Action: CpuAction.Spell, Status: CpuStepStatus.Acted,
                  FieldOwner: 0, HandIndex: null, AdditionalSpentHandIndices.Count: 2 } &&
              opposing.FieldSpells[0] == gate && opposing.Monsters[1, 0] == thousand &&
              opposing.Banished(0).Count == 0 && opposing.Banished(1).SequenceEqual([baby, wizard]),
            "Fusion Gate: a CPU can use the opponent's Gate with its own cards");

        var handGate = DemoDuel.FromPartialObservation(0, [gate], empty, empty, 8000);
        check(handGate.CanActivateFromHand(0) && handGate.Play(0, out _) && handGate.FieldSpells[0] == gate &&
              !handGate.CanUseFusionGate(0),
            "Fusion Gate: the Field Spell can activate before a recipe is available");

        Card firstWarrior = Monster(159) with { Properties = 15u << 20 | 4u << 25 };
        Card secondWarrior = Monster(160), karbonala = Monster(42, fusion: true);
        var sword = new Card(300, 300, "Legendary Sword", "", 22u << 20);
        var equipped = DemoDuel.FromPartialObservation(0, [sword, secondWarrior],
            [firstWarrior, null, null, null, null], empty, 8000, equipmentContextResolved: true,
            side0FusionDeck: [karbonala]);
        equipped.FieldSpells[0] = gate;
        check(equipped.Equip(0, 0, 0, out _) && equipped.BeginFusionGate(0, out _) &&
              equipped.ResolveFusionSummon(0, equipped.PendingFusionSummon!.Options[0].SuggestedMaterials, out _) &&
              equipped.Banished(0).Contains(firstWarrior) && equipped.Graveyard(0).SequenceEqual([sword]) &&
              equipped.EquipmentCards.Count == 0 && equipped.Spells[0, 0] is null,
            "Fusion Gate: banishing a field material clears the relationship and sends its equipped Spell to the Graveyard");
    }

    private static void CheckSubstitutes(Action<bool, string> check, Card spell, Card baby, Card wizard,
        Card thousand, Card filler)
    {
        Card?[] empty = new Card?[5];
        Card[] substitutes = [Monster(108), Monster(257), Monster(268), Monster(641)];
        foreach (Card substitute in substitutes)
        {
            var duel = DemoDuel.FromPartialObservation(0, [spell, baby, substitute], empty, empty, 8000,
                side0FusionDeck: [thousand]);
            check(duel.CanActivateFromHand(0) && duel.Play(0, out _) &&
                  duel.ResolveFusionSummon(0, duel.PendingFusionSummon!.Options.Single().SuggestedMaterials, out _) &&
                  duel.Graveyard(0).SequenceEqual([spell, baby, substitute]) && duel.Monsters[0, 0] == thousand,
                $"Fusion substitute {substitute.Id}: fills one missing material from the hand");
        }

        var full = DemoDuel.FromPartialObservation(0, [spell, baby, wizard],
            [filler, filler, substitutes[0], filler, filler], empty, 8000, side0FusionDeck: [thousand]);
        check(full.CanActivateFromHand(0) && full.Play(0, out _),
            "Fusion substitute: a field substitute can free the only zone even with exact materials in the hand");
        var option = full.PendingFusionSummon!.Options.Single();
        var hand = option.Materials.Where(material => material.Zone == FusionMaterialZone.Hand).ToArray();
        check(full.CanCompleteFusionSelection(option, [hand[0]]) &&
              !full.CanCompleteFusionSelection(option, hand) &&
              !full.ResolveFusionSummon(0, hand, out _) && full.Graveyard(0).Count == 0,
            "Fusion substitute: the choice filter prevents a dead end and the resolver preserves cards");
        check(full.ResolveFusionSummon(0, option.SuggestedMaterials, out _) &&
              full.Monsters[0, 2] == thousand && full.Hand.Count == 1 &&
              full.Graveyard(0).Contains(substitutes[0]),
            "Fusion substitute: the suggested combination consumes the field substitute and uses its zone");

        var twoSubstitutes = DemoDuel.FromPartialObservation(0, [spell, substitutes[0], substitutes[1]],
            empty, empty, 8000, side0FusionDeck: [thousand]);
        check(!twoSubstitutes.CanActivateFromHand(0) && !twoSubstitutes.Play(0, out _),
            "Fusion substitute: two substitutes cannot satisfy a two-material recipe");

        Card blue = Monster(0), ultimate = Monster(379, fusion: true);
        var triple = DemoDuel.FromPartialObservation(0, [spell, blue, blue, substitutes[0], substitutes[1]],
            empty, empty, 8000, side0FusionDeck: [ultimate]);
        check(triple.Play(0, out _), "Fusion substitute: can open a three-material recipe with two Blue-Eyes in hand");
        var tripleOption = triple.PendingFusionSummon!.Options.Single();
        var wildcards = tripleOption.Materials.Where(material => FusionRules.IsSubstitute(material.Card)).ToArray();
        var copies = tripleOption.Materials.Where(material => material.Card == blue).ToArray();
        check(!triple.CanCompleteFusionSelection(tripleOption, wildcards) &&
              !triple.ResolveFusionSummon(0, [copies[0], .. wildcards], out _) &&
              !triple.ResolveFusionSummon(0, [copies[0], copies[0], wildcards[0]], out _) &&
              triple.Hand.Count == 5,
            "Fusion substitute: a triple requires two exact, distinct occurrences and at most one wildcard");
        check(triple.CanCompleteFusionSelection(tripleOption, [wildcards[1]]) &&
              triple.ResolveFusionSummon(0, [wildcards[1], .. copies], out _) &&
              triple.Monsters[0, 0] == ultimate && triple.Hand.Single() == substitutes[0],
            "Fusion substitute: can select the wildcard first and choose which copy to use");
    }

    public static void RunCatalog(IReadOnlyList<Card> catalog, Action<bool, string> check)
    {
        var cards = catalog.ToDictionary(card => card.Id);
        using var reader = File.OpenText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "12-receitas-de-fusao.jsonl"));
        using var family = JsonDocument.Parse(reader.ReadLine()!);
        var recipes = family.RootElement.GetProperty("existing_card_recipes").EnumerateArray().Select(x => x.Clone()).ToList();
        while (reader.ReadLine() is string line)
        {
            using var document = JsonDocument.Parse(line);
            recipes.Add(document.RootElement.Clone());
        }
        var ids = recipes.Select(recipe => recipe.GetProperty("resource_id").GetUInt16()).ToHashSet();
        check(ids.SetEquals(catalog.Where(FusionRules.IsFusionMonster).Select(card => card.Id)) && ids.Count == 57,
            "Fusion: the static recipe evidence covers every category 2 monster in the DAT");
        foreach (var recipe in recipes)
        {
            var fusion = cards[recipe.GetProperty("resource_id").GetUInt16()];
            var materials = recipe.GetProperty("material_resource_ids").EnumerateArray()
                .Select(id => cards[id.GetUInt16()]).ToArray();
            var polymer = cards[materials.Length == 3 ? FusionRules.AlternatePolymerization : FusionRules.Polymerization];
            Card?[] empty = new Card?[5];
            var duel = DemoDuel.FromPartialObservation(0, [polymer, .. materials], empty, empty, 8000,
                side0FusionDeck: [fusion]);
            check(duel.Play(0, out _) &&
                  duel.ResolveFusionSummon(0, duel.PendingFusionSummon!.Options[0].SuggestedMaterials, out _) &&
                  duel.Monsters[0, 0] == fusion && duel.Hand.Count == 0 &&
                  duel.Graveyard(0).SequenceEqual([polymer, .. materials]) && duel.FusionDeck(0).Count == 0,
                $"Fusion DAT: {fusion.Id} {fusion.Name} is playable with its exact native recipe");
        }

        var alligator = cards[812];
        var alligatorDuel = DemoDuel.FromPartialObservation(0, [cards[1003], cards[3], cards[760]],
            new Card?[5], [cards[439], null, null, null, null], 8000, side0FusionDeck: [alligator]);
        check(alligatorDuel.Play(0, out _) &&
              alligatorDuel.ResolveFusionSummon(0, alligatorDuel.PendingFusionSummon!.Options[0].SuggestedMaterials, out _) &&
              alligatorDuel.EndTurn(out _) && alligatorDuel.EndTurn(out _) && alligatorDuel.BeginBattle(out _) &&
              alligatorDuel.CanAttackDirectly(0),
            "Alligator's Sword Dragon: direct attack works after a real Fusion Summon against WATER");
    }
}
