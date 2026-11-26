using Joey.Core;

internal static class SummonTrapChecks
{
    private static Card Monster(ushort id, int index, int atk = 1000, int def = 800,
        int level = 4, int kind = 3) =>
        new(index, id, $"Monster {id}", "", ((uint)kind << 20) |
            ((uint)level << 25) | ((uint)(atk / 10) << 9) | (uint)(def / 10));
    private static Card Trap(ushort id, int index) =>
        new(index, id, id == 1002 ? "Trap Hole" : "Torrential Tribute", "", 21u << 20);
    private static Card?[] Slots(Card? a = null, Card? b = null, Card? c = null) =>
        [a, b, c, null, null];

    private sealed class ResponseGate : IPresentationGate
    {
        public string? WaitingFor { get; set; }
        public bool CanAdvance => true;
        public bool CanComplete(QueuedDuelCommand entry, CommandResult result) =>
            entry.Command.GetType().Name != WaitingFor;
    }

    private static void StepUntil(ActionQueue queue, Func<bool> expected)
    {
        for (int i = 0; i < 60 && queue.IsBusy && !expected(); i++) queue.Tick();
        if (!expected()) throw new InvalidOperationException("Summon response did not reach expected state.");
    }

    private static void RunQueued(Action<bool, string> check)
    {
        var trapHole = Trap(1002, 120);
        var senju = Monster(1109, 485, 1400, 1000);
        var ritual = new Card(777, 730, "Relinquished", "", (18u << 20) | (1u << 25));
        foreach (int summoner in new[] { 0, 1 })
        foreach (bool pass in new[] { false, true })
        {
            int owner = 1 - summoner;
            var duel = DemoDuel.FromPartialObservation(owner, [trapHole], Slots(), Slots(), 8000,
                side0Deck: summoner == 0 ? [senju, ritual] : [], side1Deck: summoner == 1 ? [senju, ritual] : []);
            check(duel.SetFromHand(0, out _) && duel.EndTurn(out _) && duel.Draw(out _), "Queued Trap Hole fixture");
            var gate = new ResponseGate { WaitingFor = nameof(SummonCommand) };
            var console = new DuelConsoleSession(duel, gate);
            var queue = console.Queue;
            check(console.Execute("summon 0 2").Enqueued, "Queued Normal Summon admitted");
            StepUntil(queue, () => queue.Active?.Command is SummonCommand);
            check(duel.PendingSummonResponse is null && duel.PendingCardSelection is null &&
                duel.Monsters[summoner, 2] == senju, "Response and search wait for the summon gate");
            gate.WaitingFor = null;
            StepUntil(queue, () => !queue.IsBusy);
            var response = duel.PendingSummonResponse!;
            var option = response.EligibleTraps.Single();
            check(option.Owner == owner && duel.UsesQueuedSummonResponse(response) && console.HasPendingChoice &&
                console.Execute("choices").Message.Contains($"{owner} 0: 1002") &&
                duel.PendingCardSelection is null && !console.Execute("draw 1").Success &&
                !duel.PassSummonResponse(out _) && !duel.ActivateSummonTrap(owner, 0, out _),
                "Queued response blocks other actions and legacy answers");

            if (pass)
                check(console.Execute("respond-summon pass").Enqueued, "Console admits pass for current response");
            else
                check(new CpuDuelController(owner).Step(duel, queueTurnActions: true) is
                    { Status: CpuStepStatus.Acted, Action: CpuAction.Response, Card: null },
                    "CPU admits Trap Hole without duplicating activation presentation");
            var admitted = queue.Trace.Last().Entry;
            check(ReferenceEquals(response, duel.PendingSummonResponse) && duel.Spells[owner, 0] == trapHole &&
                duel.PendingCardSelection is null && admitted.Origin == (pass ? ActionOrigin.Console : ActionOrigin.Cpu) &&
                !duel.TryQueueSummonResponse(response, null, out _), "Response admission does not spend or clear the choice");
            if (!pass)
            {
                var presentation = duel.DescribeActionPresentation(admitted.Command);
                check(presentation is { Kind: ActionPresentationKind.Activate, SourceIndex: 0 } &&
                    presentation.Source.Side == owner && presentation.Card == trapHole, "A03 projects the actual Trap before spending");

                gate.WaitingFor = "ResolveSummonResponseCommand";
                StepUntil(queue, () => queue.Active?.Command.GetType().Name == gate.WaitingFor);
                queue.Tick();
                check(duel.Spells[owner, 0] == trapHole && duel.Monsters[summoner, 2] == senju &&
                    duel.PendingCardSelection is null, "Activation gate precedes cost and destruction");
                gate.WaitingFor = nameof(SendToGraveyardCommand);
                StepUntil(queue, () => queue.Active?.Command is SendToGraveyardCommand);
                queue.Tick();
                check(duel.Graveyard(owner).Single() == trapHole && duel.Monsters[summoner, 2] == senju &&
                    duel.PendingCardSelection is null, "Cost gate precedes destruction and trigger");
            }
            gate.WaitingFor = null;
            StepUntil(queue, () => !queue.IsBusy);
            check(duel.PendingSummonResponse is null && !duel.TryQueueSummonResponse(response, null, out _) &&
                duel.PendingCardSelection is { EffectResourceId: 1109 } search && search.Owner == summoner &&
                (pass ? duel.Spells[owner, 0] == trapHole && duel.Monsters[summoner, 2] == senju && duel.LastSummonTrap is null
                    : duel.Monsters[summoner, 2] is null && duel.Graveyard(summoner).Single() == senju &&
                      duel.LastMonsterWipe is null && duel.LastSummonTrap is { TrapResourceId: 1002, DestroyedMonsters: 1 }),
                "Pass or Trap Hole preserves the summon trigger and accounts only applied moves");
        }

        var torrential = Trap(1425, 272);
        var sangan = Monster(47, 248);
        var witch = Monster(573, 261, 1100, 1200);
        var strong = Monster(12, 103, 1600);
        var low = Monster(11, 102);
        var wipe = DemoDuel.FromPartialObservation(0, [torrential], Slots(sangan), Slots(witch), 8000,
            side0Deck: [low], side1Deck: [strong, low]);
        check(wipe.SetFromHand(0, out _) && wipe.EndTurn(out _) && wipe.Draw(out _), "Queued Torrential fixture");
        var wipeGate = new ResponseGate();
        var wipeConsole = new DuelConsoleSession(wipe, wipeGate);
        check(wipeConsole.Execute("summon 0 1").Enqueued, "Queued Torrential fixture summons");
        StepUntil(wipeConsole.Queue, () => !wipeConsole.Queue.IsBusy);
        var pending = wipe.PendingSummonResponse!;
        check(!wipeConsole.Execute("respond-summon 1 0").Success &&
            wipeConsole.Execute("respond-summon 0 0").Enqueued, "Console admits only an offered Trap");
        var originalTrap = wipe.Spells[0, 0];
        wipe.Spells[0, 0] = torrential with { };
        StepUntil(wipeConsole.Queue, () => !wipeConsole.Queue.IsBusy);
        check(ReferenceEquals(pending, wipe.PendingSummonResponse) && wipe.Graveyard(0).Count == 0 &&
            wipe.PendingCardSelection is null && wipeConsole.Queue.Trace.Last().Result?.Outcome == CommandOutcome.Rejected,
            "Replacing the chosen Trap before execution rejects without consuming the response");
        wipe.Spells[0, 0] = originalTrap;
        check(wipeConsole.Execute("respond-summon 0 0").Enqueued, "A still current response can be answered again");
        wipeGate.WaitingFor = nameof(DestroyCommand);
        StepUntil(wipeConsole.Queue, () => wipeConsole.Queue.Active?.Command is DestroyCommand);
        wipeConsole.Queue.Tick();
        check(wipe.Graveyard(0).SequenceEqual([torrential, sangan]) && wipe.Graveyard(1).Count == 0 &&
            wipe.PendingCardSelection is null && wipe.LastMonsterWipe is null,
            "Torrential applies exactly one destruction before the gate, with no premature search");
        wipeGate.WaitingFor = null;
        StepUntil(wipeConsole.Queue, () => !wipeConsole.Queue.IsBusy);
        check(wipe.Graveyard(1).SequenceEqual([witch, strong]) &&
            wipe.LastMonsterWipe is { EffectResourceId: 1425, DestroyedPlayer0: 1, DestroyedPlayer1: 2 } &&
            wipe.LastSummonTrap is { TrapResourceId: 1425, DestroyedMonsters: 3 } &&
            wipe.PendingCardSelection is { Owner: 0, EffectResourceId: 47 },
            "Torrential finalizes the batch in field order before opening Sangan's search");
    }

    private static void RunQueuedFlipSummon(Action<bool, string> check)
    {
        var drawCard = Monster(24, 240);
        foreach (int summoner in new[] { 0, 1 })
        foreach (bool skelengel in new[] { false, true })
        foreach (bool pass in new[] { false, true })
        {
            int owner = 1 - summoner;
            var monster = skelengel ? Monster(FlipEffectRules.Skelengel, 230, 900) : Monster(24, 231, 1400);
            var trap = skelengel ? Trap(SummonTrapRules.TorrentialTribute, 272) : Trap(SummonTrapRules.TrapHole, 120);
            var duel = DemoDuel.FromPartialObservation(owner, [trap],
                summoner == 0 ? Slots(null, null, monster) : Slots(),
                summoner == 1 ? Slots(null, null, monster) : Slots(), 8000,
                side0Deck: summoner == 0 ? [drawCard] : [], side1Deck: summoner == 1 ? [drawCard] : []);
            duel.MonsterFaceDown[summoner, 2] = duel.DefensePosition[summoner, 2] = true;
            check(duel.SetFromHand(0, out _) && duel.EndTurn(out _), "Queued Flip Summon fixture");
            var gate = new ResponseGate { WaitingFor = nameof(FlipCommand) };
            var console = new DuelConsoleSession(duel, gate);
            var queue = console.Queue;
            check(console.Execute("flip 2").Enqueued, "Flip Summon admitted");
            StepUntil(queue, () => queue.Active?.Command is FlipCommand);
            check(!duel.MonsterFaceDown[summoner, 2] && duel.PendingSummonResponse is null &&
                queue.PendingDecision is null, "Flip gate precedes the response and optional draw");
            gate.WaitingFor = null;
            StepUntil(queue, () => !queue.IsBusy);
            check(duel.PendingSummonResponse is { Summon.Kind: DemoSummonKind.Flip } response &&
                duel.UsesQueuedSummonResponse(response) && console.HasPendingChoice &&
                queue.PendingDecision is null && duel.PendingEffectDecision is null &&
                !duel.PassSummonResponse(out _) && !duel.ActivateSummonTrap(owner, 0, out _),
                "Queued Flip Summon opens only the queued response window");

            bool admitted = pass ? console.Execute("respond-summon pass").Enqueued :
                new CpuDuelController(owner).Step(duel, queueTurnActions: true) is
                    { Status: CpuStepStatus.Acted, Action: CpuAction.Response };
            check(admitted && duel.Spells[owner, 0] == trap, "Flip Summon response admitted without cost movement");
            if (!pass)
            {
                gate.WaitingFor = nameof(SendToGraveyardCommand);
                StepUntil(queue, () => queue.Active?.Command is SendToGraveyardCommand);
                check(duel.Graveyard(owner).SequenceEqual([trap]) &&
                    duel.Monsters[summoner, 2] == monster && queue.PendingDecision is null,
                    "Trap spend gate precedes destruction and optional draw");
                gate.WaitingFor = null;
            }
            StepUntil(queue, () => skelengel ? queue.PendingDecision is not null : !queue.IsBusy);
            check(duel.PendingSummonResponse is null && duel.PendingEffectDecision is null &&
                (pass ? duel.Spells[owner, 0] == trap && duel.Monsters[summoner, 2] == monster &&
                    duel.LastSummonTrap is null
                    : duel.Spells[owner, 0] is null && duel.Monsters[summoner, 2] is null &&
                    duel.Graveyard(summoner).SequenceEqual([monster]) &&
                    duel.LastSummonTrap is { SummonKind: DemoSummonKind.Flip, DestroyedMonsters: 1 }),
                "Flip Summon response completes before the following effect");
            if (skelengel)
            {
                var decision = queue.PendingDecision!;
                check(decision.Source.Id == FlipEffectRules.Skelengel && decision.Owner == summoner &&
                    duel.Hand.Count == 0 && console.Execute($"decision {decision.Id} accept").Success,
                    "Skelengel draw remains queued even after the Trap destroys its source");
                StepUntil(queue, () => !queue.IsBusy);
                check(duel.Hand.SequenceEqual([drawCard]) && duel.EffectDecisionHistory.Count == 1 &&
                    queue.Trace.Count(entry => entry.Stage == ActionTraceStage.Completed &&
                        entry.Entry.Command is DrawCommand) == 1,
                    "Skelengel draws exactly once through the shared command");
            }
        }
    }

    private static void RunQueuedFlipBatchResponse(Action<bool, string> check)
    {
        var immortal = Monster(FlipEffectRules.ImmortalOfThunder, 232, 1500);
        var torrential = Trap(SummonTrapRules.TorrentialTribute, 272);
        var duel = DemoDuel.FromPartialObservation(0, [torrential], Slots(), Slots(immortal), 8000);
        duel.MonsterFaceDown[1, 0] = duel.DefensePosition[1, 0] = true;
        check(duel.SetFromHand(0, out _) && duel.EndTurn(out _), "Queued Flip batch response fixture");
        var gate = new ResponseGate { WaitingFor = nameof(HealCommand) };
        var console = new DuelConsoleSession(duel, gate);
        var queue = console.Queue;
        check(console.Execute("flip 0").Enqueued, "Immortal Flip Summon is admitted");
        StepUntil(queue, () => queue.Active?.Command is HealCommand);
        check(duel.Life[1] == 11000 && duel.PendingSummonResponse is null,
            "The immediate FLIP batch completes its gate before the response opens");
        gate.WaitingFor = null;
        StepUntil(queue, () => !queue.IsBusy);
        check(duel.PendingSummonResponse is { Summon.Kind: DemoSummonKind.Flip } response &&
            duel.UsesQueuedSummonResponse(response) && console.Execute("respond-summon 0 0").Enqueued,
            "Torrential can answer after the immediate FLIP batch");
        StepUntil(queue, () => !queue.IsBusy);
        check(duel.Life.SequenceEqual([8000, 6000]) && duel.Graveyard(1).SequenceEqual([immortal]) &&
            queue.Trace.Count(entry => entry.Stage == ActionTraceStage.Completed &&
                entry.Entry.Command is DamageCommand { Side: 1, Amount: 5000 }) == 1,
            "Torrential preserves the linked graveyard consequence of Immortal of Thunder");
    }

    private static void RunQueuedRecoveryAfterSummonResponse(Action<bool, string> check)
    {
        var torrential = Trap(SummonTrapRules.TorrentialTribute, 272);
        var searcher = Monster(47, 248);
        var searchResult = Monster(24, 240);
        foreach (int side in new[] { 0, 1 })
        foreach (ushort resourceId in new ushort[] { 427, 101 })
        foreach (bool pass in new[] { false, true })
        {
            int responder = 1 - side;
            var flip = Monster(resourceId, 241);
            var desired = resourceId == 427
                ? new Card(342, 342, "Recovery Spell", "", 22u << 20)
                : Trap(AttackTrapRules.MirrorForce, 467);
            Card[] graveyard = [desired, flip, desired];
            var duel = DemoDuel.FromPartialObservation(responder, [torrential],
                side == 0 ? Slots(flip, searcher) : Slots(),
                side == 1 ? Slots(flip, searcher) : Slots(), 8000,
                side0Deck: side == 0 ? [searchResult] : [], side1Deck: side == 1 ? [searchResult] : [],
                side0Graveyard: side == 0 ? graveyard : [], side1Graveyard: side == 1 ? graveyard : []);
            duel.MonsterFaceDown[side, 0] = duel.DefensePosition[side, 0] = true;
            check(duel.SetFromHand(0, out _) && duel.EndTurn(out _) &&
                duel.TryCaptureCardTarget(side, DuelZone.Graveyard, 2, out _), "FLIP recovery response fixture");
            duel.TryCaptureCardTarget(side, DuelZone.Graveyard, 2, out var secondCopy);

            var gate = new ResponseGate();
            var console = new DuelConsoleSession(duel, gate);
            var queue = console.Queue;
            check(console.Execute("flip 0").Enqueued, "P018 Flip Summon admitted");
            StepUntil(queue, () => !queue.IsBusy);
            check(duel.PendingSummonResponse is { } response &&
                duel.UsesQueuedSummonResponse(response) && duel.PendingCardSelection is null &&
                CpuDuelController.PendingOwner(duel) == responder &&
                !console.Execute("select-hand 2").Enqueued &&
                console.Execute(pass ? "respond-summon pass" : $"respond-summon {responder} 0").Enqueued,
                "Recovery waits while the responder owns the Trap window");

            if (!pass)
            {
                gate.WaitingFor = nameof(DestroyCommand);
                StepUntil(queue, () => queue.Active?.Command is DestroyCommand);
                check(duel.Monsters[side, 0] is null && duel.Monsters[side, 1] == searcher &&
                    duel.PendingCardSelection is null, "Recovery and search wait for all Torrential destructions");
                gate.WaitingFor = null;
            }
            StepUntil(queue, () => !queue.IsBusy);
            check(duel.PendingSummonResponse is null && duel.PendingCardSelection is { } recovery &&
                recovery.EffectResourceId == resourceId && duel.UsesQueuedHandSelection(recovery) &&
                duel.QueuedGraveyardRecoveryCandidates(recovery).Select(candidate => candidate.SourceIndex)
                    .SequenceEqual([0, 2]) && duel.Hand.Count == 0 &&
                console.Execute("select-hand 2").Enqueued,
                "P018 resumes the original candidates and admits the selected occurrence");
            StepUntil(queue, () => !queue.IsBusy);
            check(duel.TryCaptureCardTarget(side, DuelZone.Hand, 0, out var recovered) &&
                recovered.Occurrence == secondCopy.Occurrence &&
                (pass ? duel.PendingCardSelection is null && duel.Monsters[side, 0] == flip :
                    duel.PendingCardSelection is { EffectResourceId: 47 } && duel.Monsters[side, 0] is null),
                "P018 completes with the selected copy before Torrential's Sangan search");
        }
    }

    private static void RunQueuedRecruitmentAfterSummonResponse(Action<bool, string> check)
    {
        var torrential = Trap(SummonTrapRules.TorrentialTribute, 272);
        var vermin = Monster(FlipEffectRules.BubonicVermin, 241);
        foreach (int side in new[] { 0, 1 })
        foreach (bool pass in new[] { false, true })
        {
            int responder = 1 - side;
            Card[] deck = [vermin, vermin];
            var duel = DemoDuel.FromPartialObservation(responder, [torrential],
                side == 0 ? Slots(vermin) : Slots(), side == 1 ? Slots(vermin) : Slots(), 8000,
                side0Deck: side == 0 ? deck : [], side1Deck: side == 1 ? deck : []);
            duel.MonsterFaceDown[side, 0] = duel.DefensePosition[side, 0] = true;
            check(duel.SetFromHand(0, out _) && duel.EndTurn(out _), "Bubonic response fixture");
            check(duel.TryCaptureCardTarget(side, DuelZone.Deck, 1, out var secondCopy),
                "Bubonic fixture has two deck occurrences");

            var gate = new ResponseGate();
            var console = new DuelConsoleSession(duel, gate);
            var queue = console.Queue;
            check(console.Execute("flip 0").Enqueued, "Bubonic Flip Summon admitted");
            StepUntil(queue, () => !queue.IsBusy);
            check(duel.PendingSummonResponse is { } response && duel.UsesQueuedSummonResponse(response) &&
                duel.PendingCardSelection is null && CpuDuelController.PendingOwner(duel) == responder &&
                !console.Execute("select-recruit 1 2").Enqueued &&
                console.Execute(pass ? "respond-summon pass" : $"respond-summon {responder} 0").Enqueued,
                "Recruitment waits while the responder owns the Trap window");

            if (!pass)
            {
                gate.WaitingFor = nameof(DestroyCommand);
                StepUntil(queue, () => queue.Active?.Command is DestroyCommand);
                check(duel.Monsters[side, 0] is null && duel.PendingCardSelection is null,
                    "Recruitment waits for the destruction gate after the source leaves");
                gate.WaitingFor = null;
            }
            StepUntil(queue, () => !queue.IsBusy);
            check(duel.PendingSummonResponse is null && duel.PendingCardSelection is { } recruitment &&
                duel.UsesQueuedFieldSelection(recruitment) &&
                console.Execute(pass ? "select-recruit decline" : "select-recruit 1 2").Enqueued,
                "Bubonic recruitment resumes by the original deck occurrences");
            StepUntil(queue, () => !queue.IsBusy);

            check(duel.PendingCardSelection is null && duel.DeckCount(side) == (pass ? 2 : 1) &&
                queue.Trace.Count(t => t.Stage == ActionTraceStage.Executed &&
                    t.Entry.Command is ShuffleDeckCommand) == (pass ? 0 : 1),
                "Only accepted recruitment moves a deck card and shuffles");
            check(pass ? duel.Monsters[side, 0] == vermin && duel.Monsters[side, 2] is null :
                duel.TryCaptureCardTarget(side, DuelZone.Monster, 2, out var recruit) &&
                recruit.Occurrence == secondCopy.Occurrence && duel.MonsterFaceDown[side, 2] &&
                duel.DefensePosition[side, 2], "Bubonic accepts the exact copy in face-down defense after Torrential");
        }
    }

    private static void RunQueuedSpellTrapPairAfterSummonResponse(Action<bool, string> check)
    {
        var torrential = Trap(SummonTrapRules.TorrentialTribute, 272);
        var first = Trap(SummonTrapRules.TrapHole, 273);
        var second = Trap(AttackTrapRules.MirrorForce, 274);
        foreach (int side in new[] { 0, 1 })
        foreach (ushort id in new[] { FlipEffectRules.Greenkappa, FlipEffectRules.TornadoBird })
        foreach (var (pass, enoughAfterTrap) in new[] { (true, false), (false, false), (false, true) })
        {
            int responder = 1 - side;
            var flip = Monster(id, 241);
            Card[] hand = enoughAfterTrap ? [torrential, first, second] : [torrential, first];
            var duel = DemoDuel.FromPartialObservation(responder, hand,
                side == 0 ? Slots(flip) : Slots(), side == 1 ? Slots(flip) : Slots(), 8000);
            duel.MonsterFaceDown[side, 0] = duel.DefensePosition[side, 0] = true;
            for (int i = 0; i < hand.Length; i++)
                check(duel.SetFromHand(0, out _), "Spell/Trap FLIP response fixture sets a Trap");
            check(duel.EndTurn(out _), "Spell/Trap FLIP response fixture ends setup turn");

            var gate = new ResponseGate();
            var console = new DuelConsoleSession(duel, gate);
            var queue = console.Queue;
            check(console.Execute("flip 0").Enqueued, "Spell/Trap FLIP admitted");
            StepUntil(queue, () => !queue.IsBusy);
            check(duel.PendingSummonResponse is { } response && duel.UsesQueuedSummonResponse(response) &&
                duel.PendingFlipSpellTrapTargets is null && CpuDuelController.PendingOwner(duel) == responder &&
                !console.Execute($"select-st-pair {responder} 0 {responder} 1").Enqueued &&
                console.Execute(pass ? "respond-summon pass" : $"respond-summon {responder} 0").Enqueued,
                "Only the Trap response is available while the Spell/Trap choice is suspended");
            if (!pass)
            {
                gate.WaitingFor = nameof(DestroyCommand);
                StepUntil(queue, () => queue.Active?.Command is DestroyCommand);
                check(duel.Monsters[side, 0] is null && duel.PendingFlipSpellTrapTargets is null,
                    "The pair waits until the gate closes after the FLIP source leaves");
                gate.WaitingFor = null;
            }
            StepUntil(queue, () => !queue.IsBusy);
            if (!pass && !enoughAfterTrap)
            {
                check(duel.PendingFlipSpellTrapTargets is null && duel.Spells[responder, 1] == first &&
                    console.Execute("turn end").Enqueued,
                    "A mandatory pair with only one surviving candidate leaves no pending choice");
                StepUntil(queue, () => !queue.IsBusy);
                continue;
            }

            int firstSlot = pass ? 0 : 1;
            int secondSlot = firstSlot + 1;
            check(duel.PendingSummonResponse is null && duel.PendingFlipSpellTrapTargets is { } pair &&
                duel.UsesQueuedFlipSpellTrapSelection(pair) &&
                duel.QueuedFlipSpellTrapCandidates(pair).Select(c => c.Zone).SequenceEqual([firstSlot, secondSlot]) &&
                console.Execute($"select-st-pair {responder} {firstSlot} {responder} {secondSlot}").Enqueued,
                "The original surviving pair can be selected after pass or source destruction");
            StepUntil(queue, () => !queue.IsBusy);

            bool destroys = id == FlipEffectRules.Greenkappa;
            Card firstSelected = pass ? torrential : first;
            Card secondSelected = pass ? first : second;
            var destination = destroys ? duel.Graveyard(responder) : duel.HandFor(responder);
            check(duel.PendingFlipSpellTrapTargets is null && destination.Contains(firstSelected) &&
                destination.Contains(secondSelected) && duel.Spells[responder, firstSlot] is null &&
                duel.Spells[responder, secondSlot] is null,
                "Greenkappa destroys both cards and Tornado Bird returns both to their owner");
        }
    }

    private static void RunQueuedMonsterFlipAfterSummonResponse(Action<bool, string> check)
    {
        var torrential = Trap(SummonTrapRules.TorrentialTribute, 272);
        var trapHole = Trap(SummonTrapRules.TrapHole, 273);
        var target = Monster(24, 240);
        foreach (int side in new[] { 0, 1 })
        foreach (ushort id in new[] { FlipEffectRules.ManEaterBug, FlipEffectRules.HaneHane, FlipEffectRules.PenguinSoldier })
        foreach (var (trap, pass) in new[] { (torrential, true), (torrential, false), (trapHole, false) })
        {
            int responder = 1 - side;
            bool wiped = !pass && trap == torrential;
            bool optional = id == FlipEffectRules.PenguinSoldier;
            // Raised fixture ATK permits testing Trap Hole separately from a full wipe.
            var flip = Monster(id, 241, atk: 1100);
            var duel = DemoDuel.FromPartialObservation(responder, [trap],
                Slots(side == 0 ? flip : target), Slots(side == 1 ? flip : target), 8000);
            duel.MonsterFaceDown[side, 0] = duel.DefensePosition[side, 0] = true;
            check(duel.SetFromHand(0, out _) && duel.EndTurn(out _) &&
                duel.TryCaptureCardTarget(responder, DuelZone.Monster, 0, out _), "Monster FLIP response fixture");
            duel.TryCaptureCardTarget(responder, DuelZone.Monster, 0, out var targetOccurrence);

            var gate = new ResponseGate();
            var console = new DuelConsoleSession(duel, gate);
            var queue = console.Queue;
            check(console.Execute("flip 0").Enqueued, "Monster FLIP admitted");
            StepUntil(queue, () => !queue.IsBusy);
            check(duel.PendingSummonResponse is { } response && duel.UsesQueuedSummonResponse(response) &&
                duel.PendingFlipTarget is null && CpuDuelController.PendingOwner(duel) == responder &&
                !console.Execute($"select-flip {responder} 0").Enqueued &&
                console.Execute(pass ? "respond-summon pass" : $"respond-summon {responder} 0").Enqueued,
                "Only the Trap responder can act while the monster choice is suspended");
            if (!pass)
            {
                gate.WaitingFor = nameof(DestroyCommand);
                StepUntil(queue, () => queue.Active?.Command is DestroyCommand);
                check(duel.PendingFlipTarget is null, "Monster target choice waits for the destruction gate");
                gate.WaitingFor = null;
            }
            StepUntil(queue, () => !queue.IsBusy);
            check(duel.PendingSummonResponse is null && (duel.Monsters[side, 0] is not null) == pass,
                "Trap response finishes with the expected source state");

            if (wiped && !optional)
            {
                check(duel.PendingFlipTarget is null && console.Execute("turn end").Enqueued,
                    "A mandatory monster choice with no legal target leaves the turn playable");
                StepUntil(queue, () => !queue.IsBusy);
                continue;
            }

            check(duel.PendingFlipTarget is { } pending && duel.UsesQueuedMonsterFlipSelection(pending),
                "The queued monster choice resumes with its original source occurrence");
            if (wiped)
            {
                check(duel.QueuedMonsterFlipCandidates().Count == 0 &&
                    new CpuDuelController(side).Step(duel, queueTurnActions: true) is
                        { Action: CpuAction.Selection, Status: CpuStepStatus.Acted },
                    "CPU can decline Penguin Soldier on an empty field after Torrential");
            }
            else
            {
                check(console.Execute($"select-flip {responder} 0").Enqueued,
                    "The console selects the surviving monster after pass or Trap Hole");
            }
            StepUntil(queue, () => !queue.IsBusy);

            DuelZone destination = id == FlipEffectRules.ManEaterBug ? DuelZone.Graveyard : DuelZone.Hand;
            int destinationIndex = destination == DuelZone.Graveyard && !pass ? 1 : 0;
            check(duel.PendingFlipTarget is null && duel.Monsters[responder, 0] is null &&
                (wiped || duel.TryCaptureCardTarget(responder, destination, destinationIndex, out var moved) &&
                    moved.Occurrence == targetOccurrence.Occurrence),
                "Answer closes the choice and moves the surviving target by occurrence");
        }
    }

    public static void Run(Action<bool, string> check)
    {
        RunQueued(check);
        RunQueuedFlipSummon(check);
        RunQueuedFlipBatchResponse(check);
        RunQueuedRecoveryAfterSummonResponse(check);
        RunQueuedRecruitmentAfterSummonResponse(check);
        RunQueuedSpellTrapPairAfterSummonResponse(check);
        RunQueuedMonsterFlipAfterSummonResponse(check);
        var trapHole = Trap(1002, 120);
        var torrential = Trap(1425, 272);
        var small = Monster(10, 101, 900);
        var exact = Monster(11, 102, 1000);
        var strong = Monster(12, 103, 1600);
        var sangan = Monster(47, 248);
        var witch = Monster(573, 261, 1100, 1200);
        var senju = Monster(1109, 485, 1400, 1000);
        var ritual = new Card(777, 730, "Relinquished", "", (18u << 20) | (1u << 25));
        var eligibleSangan = Monster(14, 104, 1200, 1900);
        var eligibleWitch = Monster(15, 105, 1900, 1200);

        check(SummonTrapRules.Supports(1002) && SummonTrapRules.Supports(1425) &&
              !SummonTrapRules.Supports(335) && !SummonTrapRules.Supports(1000) &&
              SummonTrapRules.IsEligible(trapHole, 0,
                  new(1, 0, exact, DemoSummonKind.Normal), 1000) &&
              !SummonTrapRules.IsEligible(trapHole, 1,
                  new(1, 0, exact, DemoSummonKind.Normal), 1000) &&
              !SummonTrapRules.IsEligible(trapHole, 0,
                  new(1, 0, small, DemoSummonKind.Flip), 999) &&
              SummonTrapRules.IsEligible(torrential, 1,
                  new(1, 0, small, DemoSummonKind.Normal), 900),
              "P196/P038: exact Trap IDs, opponent-only Trap Hole and inclusive 1000 current ATK");

        // The first player sets; the second player successfully Summons.
        var normal = DemoDuel.FromPartialObservation(0, [trapHole], Slots(), Slots(), 8000,
            side1Deck: [exact]);
        check(normal.SetFromHand(0, out _) &&
              normal.SpellFaceDown[0, 0] &&
              normal.PendingSummonResponse is null &&
              !normal.CanActivateFromHand(0),
              "Trap Hole: setting a Trap never activates it");
        normal.NextTurn();
        check(normal.Draw(out _) && normal.SummonLeftmost(0, false, out _) &&
              normal.PendingSummonResponse is
                  { Summon: { Player: 1, Zone: 0, Kind: DemoSummonKind.Normal } } &&
              normal.PendingSummonResponse.EligibleTraps.Single().Owner == 0 &&
              normal.PendingSummonResponse.EligibleTraps.Single().Trap == trapHole &&
              !normal.EndTurn(out _) && !normal.BeginBattle(out _) &&
              !normal.ActivateSummonTrap(1, 0, out _) &&
              !normal.ActivateSummonTrap(0, 1, out _),
              "Trap Hole: opponent Normal Summon opens exactly one response window and blocks actions");
        check(normal.ActivateSummonTrap(0, 0, out _) &&
              normal.PendingSummonResponse is null &&
              normal.LastSummonTrap is { TrapResourceId: 1002, Responder: 0,
                  SummoningPlayer: 1, SummonKind: DemoSummonKind.Normal, DestroyedMonsters: 1 } &&
              normal.Spells[0, 0] is null &&
              normal.Monsters[1, 0] is null &&
              normal.Graveyard(1).Single() == exact &&
              normal.Graveyard(0).Single() == trapHole &&
              !normal.ActivateSummonTrap(0, 0, out _),
              "Trap Hole: opponent's monster destroyed once, Trap sent to its owner's graveyard");

        // A Trap cannot be activated on the same turn it was Set, even for
        // its controller's own successful Summon (Torrential).
        var fresh = DemoDuel.FromPartialObservation(0, [torrential, strong],
            Slots(), Slots(), 8000);
        check(fresh.SetFromHand(0, out _) &&
              fresh.SummonLeftmost(0, false, out _) &&
              fresh.PendingSummonResponse is null &&
              fresh.Spells[0, 0] == torrential &&
              fresh.Monsters[0, 0] == strong &&
              !fresh.ActivateSummonTrap(0, 0, out _),
              "Torrential: cannot respond on the turn it was Set");

        // Current ATK, not base ATK, controls Trap Hole. Sogen boosts Warrior.
        var warrior = Monster(50, 106, 900, 900, kind: 15);
        var field = new Card(490, 332, "Sogen", "", 22u << 20);
        var buffed = DemoDuel.FromPartialObservation(0, [trapHole, field], Slots(),
            Slots(), 8000, side1Deck: [warrior], equipmentContextResolved: true);
        check(buffed.SetFromHand(0, out _) && buffed.Play(0, out _) &&
              buffed.AttackValue(1, 0) == 0,
              "Trap Hole: prior set and field buff are legal setup");
        buffed.NextTurn();
        check(buffed.Draw(out _) && buffed.SummonLeftmost(0, false, out _) &&
              buffed.AttackValue(1, 0) == 1100 &&
              buffed.PendingSummonResponse is not null &&
              buffed.ActivateSummonTrap(0, 0, out _) &&
              buffed.Graveyard(1).Single() == warrior,
              "Trap Hole: Sogen's +200 ATK makes a 900 ATK Warrior eligible");

        var low = DemoDuel.FromPartialObservation(0, [trapHole], Slots(), Slots(), 8000,
            side1Deck: [small]);
        low.SetFromHand(0, out _); low.NextTurn(); low.Draw(out _);
        check(low.SummonLeftmost(0, false, out _) &&
              low.PendingSummonResponse is null &&
              low.Spells[0, 0] == trapHole && low.Monsters[1, 0] == small,
              "Trap Hole: a Normal Summon below 1000 ATK opens no response");

        var flip = DemoDuel.FromPartialObservation(0, [trapHole], Slots(),
            Slots(exact), 8000);
        flip.SetFromHand(0, out _); flip.NextTurn();
        flip.MonsterFaceDown[1, 0] = flip.DefensePosition[1, 0] = true;
        check(flip.ChangePosition(0, out _) &&
              flip.PendingSummonResponse is
                  { Summon: { Kind: DemoSummonKind.Flip, Player: 1 } } &&
              flip.ActivateSummonTrap(0, 0, out _) &&
              flip.Monsters[1, 0] is null &&
              flip.Graveyard(1).Single() == exact &&
              flip.LastSummonTrap?.SummonKind == DemoSummonKind.Flip,
              "Trap Hole: voluntary Flip Summon qualifies, with no battle required");

        var external = DemoDuel.FromPartialObservation(0, [trapHole], Slots(),
            Slots(exact), 8000);
        external.SetFromHand(0, out _); external.NextTurn();
        external.MonsterFaceDown[1, 0] = true;
        check(external.RevealMonsterByEffect(1, 0, out _) &&
              external.PendingSummonResponse is null && external.Spells[0, 0] == trapHole,
              "Trap Hole: external face-up reveal does not create a Summon response");

        var faceDownSet = DemoDuel.FromPartialObservation(0, [trapHole], Slots(),
            Slots(), 8000, side1Deck: [strong]);
        faceDownSet.SetFromHand(0, out _); faceDownSet.NextTurn(); faceDownSet.Draw(out _);
        check(faceDownSet.SetFromHand(0, out _) &&
              faceDownSet.PendingSummonResponse is null &&
              faceDownSet.Spells[0, 0] == trapHole,
              "Trap Hole: opponent setting a monster face-down does not activate a response");

        // Both players are affected by Torrential. No monster is removed before
        // destruction finishes, and queued Sangan/Witch searches remain available.
        var wipe = DemoDuel.FromPartialObservation(0, [torrential],
            Slots(sangan), Slots(witch), 8000,
            side0Deck: [eligibleSangan], side1Deck: [strong, eligibleWitch]);
        check(wipe.SetFromHand(0, out _), "Torrential: set card before opponent turn");
        wipe.NextTurn();
        check(wipe.Draw(out _) && wipe.SummonLeftmost(0, false, out _) &&
              wipe.PendingSummonResponse is { Summon: { Player: 1 } } &&
              wipe.PendingSummonResponse.EligibleTraps.Single().Trap == torrential &&
              wipe.ActivateSummonTrap(0, 0, out _) &&
              wipe.LastMonsterWipe is { EffectResourceId: 1425, TotalDestroyed: 3,
                  DestroyedPlayer0: 1, DestroyedPlayer1: 2 } &&
              Enumerable.Range(0, 5).All(z => wipe.Monsters[0, z] is null &&
                wipe.Monsters[1, z] is null) &&
              wipe.Graveyard(0).SequenceEqual([torrential, sangan]) &&
              wipe.Graveyard(1).SequenceEqual([witch, strong]) &&
              wipe.PendingCardSelection is { Owner: 0, EffectResourceId: 47 } &&
              wipe.PendingCardSelection.EligibleCards.Single().Card == eligibleSangan,
              "Torrential: destroy both fields after Summon, then open queued Sangan search");
        check(wipe.ResolveCardSelection(
                  wipe.PendingCardSelection!.EligibleCards.Single().SourceIndex, out _) &&
              wipe.PendingCardSelection is { Owner: 1, EffectResourceId: 573 } &&
              wipe.PendingCardSelection.EligibleCards.Single().Card == eligibleWitch &&
              wipe.ResolveCardSelection(
                  wipe.PendingCardSelection.EligibleCards.Single().SourceIndex, out _) &&
              wipe.PendingCardSelection is null &&
              wipe.HandFor(0).Single() == eligibleSangan &&
              wipe.HandFor(1).Single() == eligibleWitch,
              "Torrential: sequential graveyard searches still resolve on each player's deck");

        var self = DemoDuel.FromPartialObservation(0, [torrential, strong],
            Slots(small), Slots(exact), 8000);
        self.SetFromHand(0, out _); self.NextTurn(); self.NextTurn();
        check(self.SummonLeftmost(0, false, out _) &&
              self.PendingSummonResponse is { Summon: { Player: 0 } } &&
              self.PendingSummonResponse.EligibleTraps.Single().Owner == 0 &&
              self.ActivateSummonTrap(0, 0, out _) &&
              self.LastMonsterWipe is { DestroyedPlayer0: 2, DestroyedPlayer1: 1 },
              "Torrential: controller can react to own successful Normal Summon on later turn");

        var passed = DemoDuel.FromPartialObservation(0, [trapHole],
            Slots(), Slots(), 8000, side1Deck: [senju, ritual]);
        passed.SetFromHand(0, out _); passed.NextTurn(); passed.Draw(out _);
        check(passed.SummonLeftmost(0, false, out _) &&
              passed.PendingSummonResponse is not null &&
              passed.PendingCardSelection is null &&
              passed.PassSummonResponse(out _) &&
              passed.PendingSummonResponse is null &&
              passed.PendingCardSelection is { Owner: 1, EffectResourceId: 1109,
                  Source: CardSelectionSource.Deck } &&
              passed.Spells[0, 0] == trapHole &&
              passed.ResolveCardSelection(0, out _) &&
              passed.HandFor(1).Single() == ritual,
              "P165: passing response preserves optional Ritual search, without spending Trap");

        var triggered = DemoDuel.FromPartialObservation(0, [trapHole],
            Slots(), Slots(), 8000, side1Deck: [senju, ritual]);
        triggered.SetFromHand(0, out _); triggered.NextTurn(); triggered.Draw(out _);
        check(triggered.SummonLeftmost(0, false, out _) &&
              triggered.PendingSummonResponse is not null &&
              triggered.ActivateSummonTrap(0, 0, out _) &&
              triggered.Monsters[1, 0] is null &&
              triggered.PendingCardSelection is { Owner: 1, EffectResourceId: 1109 } &&
              triggered.ResolveCardSelection(0, out _) &&
              triggered.HandFor(1).Single() == ritual,
              "P165: once successfully summoned, Senju's trigger survives Trap Hole destruction");

        // A special summon during battle is deliberately NOT given a Main
        // Phase Normal/Flip response; that would create an invalid trap window.
        var tomato = Monster(1123, 300, 1400, 1100);
        var dark = Monster(90, 301, 1000, 900) with
        {
            Properties = Monster(90, 301, 1000, 900).Properties | (2u << 29)
        };
        var battle = DemoDuel.FromPartialObservation(1, [], Slots(tomato),
            Slots(strong), 8000, side0Deck: [dark]);
        battle.Spells[1, 0] = torrential;
        battle.SpellFaceDown[1, 0] = true; // preexisting fixture, not set now
        check(battle.BeginBattle(out _) && battle.Attack(0, 0, out _) &&
              battle.PendingSummonResponse is null &&
              battle.PendingCardSelection is { Destination: CardSelectionDestination.Field } &&
              battle.ResolveCardSelection(0, out _) &&
              battle.Monsters[0, 0] == dark &&
              battle.PendingSummonResponse is null &&
              battle.Spells[1, 0] == torrential,
              "P038: damage-step recruitment is not offered a bogus Normal/Flip response");
    }
}
