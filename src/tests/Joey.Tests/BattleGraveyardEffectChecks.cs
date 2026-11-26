using Joey.Core;

internal static class BattleGraveyardEffectChecks
{
    private static Card Monster(ushort id, int index, int atk = 1000, int def = 100,
        int level = 4, byte attribute = 2, int type = 3) =>
        new(index, id, $"Monster {id}", "", ((uint)type << 20) |
            ((uint)level << 25) | ((uint)attribute << 29) |
            ((uint)(atk / 10) << 9) | (uint)(def / 10));
    private static Card?[] Slots(Card? a = null, Card? b = null, Card? c = null,
        Card? d = null, Card? e = null) => [a, b, c, d, e];

    private sealed class RecruitmentGate : IPresentationGate
    {
        public Type? Hold { get; set; }
        public string? HoldName { get; set; }
        public bool CanAdvance => true;
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) =>
            command.Command.GetType() != Hold && command.Command.GetType().Name != HoldName;
    }

    private static void Drain(ActionQueue queue)
    {
        for (int tick = 0; queue.IsBusy && tick < 60; tick++) queue.Tick();
        if (queue.IsBusy) throw new InvalidOperationException("Battle recruitment did not reach a choice or finish.");
    }

    private static void RunQueuedBattleRecruitment(Action<bool, string> check)
    {
        var momonga = Monster(CardSelectionRules.NimbleMomonga, 453);
        var germ = Monster(CardSelectionRules.GiantGerm, 383);
        var normal = Monster(24, 24, 2200);
        foreach (int side in new[] { 0, 1 })
        {
            int defender = 1 - side;
            var duel = DemoDuel.FromPartialObservation(defender, [],
                side == 0 ? Slots(momonga) : Slots(germ),
                side == 1 ? Slots(momonga) : Slots(germ), 8000,
                side0Deck: side == 0 ? [momonga, momonga] : [germ, germ],
                side1Deck: side == 1 ? [momonga, momonga] : [germ, germ]);
            check(duel.EndTurn(out _) && duel.BeginBattle(out _), "P168 tied battle fixture");
            var gate = new RecruitmentGate { Hold = typeof(HealCommand) };
            var console = new DuelConsoleSession(duel, gate);
            var queue = console.Queue;
            check(console.Execute("attack 0 0").Enqueued, "P168 admits both recruiters as combatants");
            for (int tick = 0; queue.IsBusy && queue.Active?.Command is not HealCommand && tick < 30; tick++) queue.Tick();
            check(queue.Active?.Command is HealCommand && duel.Life[side] == 9000 &&
                duel.Graveyard(side).SequenceEqual([momonga]) && duel.Graveyard(defender).Count == 0 &&
                duel.PendingCardSelection is null, "Momonga heals after arriving and before the second removal");
            gate.Hold = null;
            Drain(queue);
            check(duel.Life[side] == 8500 && duel.Graveyard(defender).SequenceEqual([germ]) &&
                duel.PendingCardSelection?.EffectResourceId == momonga.Id && CpuDuelController.PendingOwner(duel) == side &&
                !duel.SelectBatchCard(0, out _) && !duel.FinishOptionalCardSelection(out _) &&
                console.Execute("select-recruit batch 0 1").Enqueued,
                "Germ's damage ends before either request opens; only the queue answers the first request");
            Drain(queue);
            check(duel.Monsters[side, 0] == momonga && duel.Monsters[side, 1] == momonga &&
                duel.MonsterFaceDown[side, 0] && duel.MonsterFaceDown[side, 1] &&
                duel.PendingCardSelection?.EffectResourceId == germ.Id && CpuDuelController.PendingOwner(duel) == defender &&
                console.Execute("select-recruit decline").Enqueued,
                "Momonga recruits two hidden copies and advances to the second owner");
            Drain(queue);
            check(duel.PendingCardSelection is null && duel.DeckCount(defender) == 2 &&
                queue.Trace.Count(entry => entry.Stage == ActionTraceStage.Executed &&
                    entry.Entry.Command is ShuffleDeckCommand) == 1,
                "Declining Germ does not summon or shuffle");

            foreach (bool lethal in new[] { false, true })
            {
                var single = DemoDuel.FromPartialObservation(defender, [],
                    side == 0 ? Slots(normal) : Slots(germ), side == 1 ? Slots(normal) : Slots(germ),
                    lethal ? 100 : 8000,
                    side0Deck: defender == 0 ? [germ, germ] : [], side1Deck: defender == 1 ? [germ, germ] : []);
                single.MonsterFaceDown[defender, 0] = single.DefensePosition[defender, 0] = !lethal;
                var singleConsole = new DuelConsoleSession(single);
                check(single.EndTurn(out _) && single.BeginBattle(out _) &&
                    singleConsole.Execute("attack 0 0").Enqueued, "P168 Germ battle fixture");
                Drain(singleConsole.Queue);
                check(single.Life[side] == 7500 && single.Graveyard(defender).SequenceEqual([germ]) &&
                    (lethal ? single.Winner == side && single.PendingCardSelection is null :
                        single.PendingCardSelection?.EffectResourceId == germ.Id &&
                        singleConsole.Execute("select-recruit batch 0 1").Enqueued),
                    "Germ burns for 500 even after lethal battle damage; only a live duel can recruit");
                Drain(singleConsole.Queue);
                if (!lethal)
                    check(single.Monsters[defender, 0] == germ && single.Monsters[defender, 1] == germ &&
                        !single.DefensePosition[defender, 0] && !single.DefensePosition[defender, 1],
                        "Germ recruits both copies face-up in attack");
            }
        }
    }

    private static void RunQueuedBattleRevival(Action<bool, string> check)
    {
        var spear = Monster(CardSelectionRules.SpearCretin, 516, 500, 500);
        var opponent = Monster(24, 24, 2200);
        foreach (int owner in new[] { 0, 1 })
        foreach ((bool set, bool previouslyFlipped) in new[] { (false, false), (true, false), (false, true) })
        {
            int attacker = 1 - owner;
            var duel = DemoDuel.FromPartialObservation(1, [],
                owner == 0 ? Slots(spear) : Slots(opponent), owner == 1 ? Slots(spear) : Slots(opponent), 8000,
                side0Graveyard: owner == 0 ? [opponent, spear] : [opponent],
                side1Graveyard: owner == 1 ? [opponent, spear] : [opponent]);
            duel.MonsterFaceDown[owner, 0] = duel.DefensePosition[owner, 0] = set || previouslyFlipped;
            if (previouslyFlipped)
                check(duel.RevealMonsterByEffect(owner, 0, out _) && duel.PendingCardSelection is null,
                    "P169 records an earlier reveal without reviving before the card leaves the field");
            if (attacker == 0) check(duel.EndTurn(out _), "Spear Cretin fixture: pass to the attacking side");
            check(duel.BeginBattle(out _), "Spear Cretin fixture: enter Battle Phase");
            var gate = new RecruitmentGate { HoldName = "FinishBattleRemovalCommand" };
            var console = new DuelConsoleSession(duel, gate);
            var queue = console.Queue;
            check(console.Execute("attack 0 0").Enqueued, "P169 battle uses the queued combat producer");
            for (int tick = 0; queue.IsBusy && queue.Active?.Command.GetType().Name != gate.HoldName && tick < 30; tick++)
                queue.Tick();
            check(queue.IsBusy && duel.Monsters[owner, 0] is null && duel.Graveyard(owner).Last().Id == spear.Id &&
                duel.PendingCardSelection is null, "P169 waits for the battle removal gate before offering revival");
            gate.HoldName = null;
            Drain(queue);
            if (!set && !previouslyFlipped)
            {
                check(duel.PendingCardSelection is null, "A never-flipped Spear Cretin does not queue revival");
                continue;
            }

            var request = duel.PendingCardSelection;
            check(request is { Source: CardSelectionSource.Graveyard } && request.Owner == owner &&
                request.EligibleCards.Select(c => c.SourceIndex).SequenceEqual([0, 1]) &&
                !duel.ResolveCardSelection(1, 4, true, out _) &&
                !console.Execute("select-revive 2 4 set").Success,
                "P169 offers the other copy after battle and rejects the triggering occurrence and legacy answer");
            check(duel.TryCaptureCardTarget(owner, DuelZone.Graveyard, 1, out var otherSpear) &&
                console.Execute("select-revive 1 4 set").Enqueued, "P169 admits the chosen copy from the owner's GY");
            gate.Hold = typeof(SpecialSummonCommand);
            for (int tick = 0; queue.IsBusy && queue.Active?.Command is not SpecialSummonCommand && tick < 20; tick++)
                queue.Tick();
            check(queue.IsBusy && duel.PendingCardSelection is null && duel.MonsterFaceDown[owner, 4] &&
                duel.TryCaptureCardTarget(owner, DuelZone.Monster, 4, out var placed) &&
                placed.Occurrence == otherSpear.Occurrence,
                "P169 keeps the next owner waiting for the first revival's gate");
            gate.Hold = null;
            Drain(queue);
            check(duel.PendingCardSelection?.Owner == attacker &&
                !duel.ResolveCardSelection(0, out _) &&
                new CpuDuelController(attacker).Step(duel) is { Status: CpuStepStatus.Acted, Action: CpuAction.Selection },
                "P169 second owner also answers through the queue");
            Drain(queue);
            check(duel.PendingCardSelection is null && duel.Monsters[attacker, 1]?.Id == opponent.Id &&
                queue.Trace.Count(entry => entry.Stage == ActionTraceStage.Executed &&
                    entry.Entry.Command is SpecialSummonCommand) == 2 &&
                !queue.Trace.Any(entry => entry.Entry.Command is ShuffleDeckCommand),
                "P169 completes both revivals without a deck shuffle");
        }
    }

    public static void Run(Action<bool, string> check)
    {
        RunQueuedBattleRecruitment(check);
        RunQueuedBattleRevival(check);
        var momonga = Monster(1115, 453, 1000, 100, level: 2, attribute: 5, type: 11);
        var duplicate1 = Monster(1115, 1453, 1000, 100, level: 2, attribute: 5, type: 11);
        var duplicate2 = Monster(1115, 2453, 1000, 100, level: 2, attribute: 5, type: 11);
        var germ = Monster(1114, 383, 1000, 100, level: 2);
        var germ1 = Monster(1114, 1383, 1000, 100, level: 2);
        var germ2 = Monster(1114, 2383, 1000, 100, level: 2);
        var opponent = Monster(800, 1300, 2200, 1800);
        var irrelevant = Monster(901, 1301, 2000, 1500);
        var tribute = Monster(907, 1302, 2300, 1500, level: 6);
        var spear = Monster(1116, 516, 500, 500, level: 2);
        var anotherSpear = Monster(1116, 1516, 500, 500, level: 2);
        var ally = Monster(909, 1303, 1600, 1000);
        var enemyGrave = Monster(910, 1304, 1400, 1300);
        var bug = Monster(500, 1305, 450, 600, level: 2, type: 10);

        check(CardSelectionRules.BattleSameNameRecruiter(1115) &&
              CardSelectionRules.BattleSameNameRecruiter(1114) &&
              !CardSelectionRules.BattleSameNameRecruiter(1116) &&
              CardSelectionRules.IsEligible(duplicate1, CardSelectionFilter.SameNameMonster,
                  requiredResourceId: 1115) &&
              !CardSelectionRules.IsEligible(germ, CardSelectionFilter.SameNameMonster,
                  requiredResourceId: 1115) &&
              !CardSelectionRules.IsEligible(duplicate1, CardSelectionFilter.SameNameMonster) &&
              CardSelectionRules.IsEligible(ally, CardSelectionFilter.AnyMonster),
              "P168/P169: catalogued IDs, same resource ID duplicates, and generic Graveyard monster filter");

        var momongaDuel = DemoDuel.FromPartialObservation(1, [], Slots(momonga),
            Slots(opponent), 8000,
            side0Deck: [duplicate1, irrelevant, duplicate2], effectShuffleSeed: 1984);
        momongaDuel.MonsterFaceDown[0, 0] = momongaDuel.DefensePosition[0, 0] = true;
        check(momongaDuel.BeginBattle(out _) && momongaDuel.Attack(0, 0, out _) &&
              momongaDuel.Life[0] == 9000 && momongaDuel.Life[1] == 8000 &&
              momongaDuel.Graveyard(0).Single() == momonga &&
              momongaDuel.PendingCardSelection is {
                  Source: CardSelectionSource.Deck, Destination: CardSelectionDestination.Field,
                  Filter: CardSelectionFilter.SameNameMonster, MinSelections: 0, MaxSelections: 2,
                  Owner: 0, RequiredResourceId: 1115, EffectResourceId: 1115,
                  SummonPosition: CardSelectionPosition.FaceDownDefense } &&
              momongaDuel.PendingCardSelection.EligibleCards
                  .Select(c => c.SourceIndex).SequenceEqual([0, 2]),
              "Nimble Momonga: battle sends to GY, heals 1000, then offers 0-2 same-name copies");

        check(momongaDuel.SelectBatchCard(0, out _) &&
              momongaDuel.SelectedBatchIndices.SequenceEqual([0]) &&
              momongaDuel.DeckCount(0) == 3 &&
              momongaDuel.Monsters[0, 0] is null &&
              !momongaDuel.SelectBatchCard(0, out _) &&
              !momongaDuel.SkipOptionalCardSelection(out _),
              "P168: one selection does not mutate deck/field and cannot be duplicated or skipped");
        check(momongaDuel.FinishOptionalCardSelection(out _) &&
              momongaDuel.PendingCardSelection is null &&
              momongaDuel.Monsters[0, 0] == duplicate1 &&
              momongaDuel.MonsterFaceDown[0, 0] && momongaDuel.DefensePosition[0, 0] &&
              momongaDuel.DeckSnapshot(0).Contains(duplicate2) &&
              momongaDuel.DeckCount(0) == 2 &&
              momongaDuel.SelectedBatchIndices.Count == 0,
              "Nimble Momonga: finish after one selected copy, summon set DEF and shuffle once");

        var bothMomonga = DemoDuel.FromPartialObservation(1, [], Slots(momonga),
            Slots(opponent), 8000, side0Deck: [duplicate1, irrelevant, duplicate2]);
        bothMomonga.DefensePosition[0, 0] = true;
        check(bothMomonga.BeginBattle(out _) && bothMomonga.Attack(0, 0, out _) &&
              bothMomonga.SelectBatchCard(0, out _) &&
              bothMomonga.SelectBatchCard(2, out _) &&
              bothMomonga.PendingCardSelection is null &&
              bothMomonga.DeckSnapshot(0).SequenceEqual([irrelevant]) &&
              bothMomonga.Monsters[0, 0] == duplicate1 &&
              bothMomonga.Monsters[0, 1] == duplicate2 &&
              bothMomonga.MonsterFaceDown[0, 0] &&
              bothMomonga.MonsterFaceDown[0, 1],
              "Nimble Momonga: selecting two copies commits together with face-down DEF");

        var declined = DemoDuel.FromPartialObservation(1, [], Slots(momonga),
            Slots(opponent), 8000, side0Deck: [duplicate1, duplicate2, irrelevant]);
        declined.DefensePosition[0, 0] = true;
        check(declined.BeginBattle(out _) && declined.Attack(0, 0, out _) &&
              declined.SkipOptionalCardSelection(out _) &&
              declined.PendingCardSelection is null &&
              declined.DeckSnapshot(0).SequenceEqual([duplicate1, duplicate2, irrelevant]) &&
              declined.Monsters[0, 0] is null && declined.Life[0] == 9000,
              "Nimble Momonga: skipping optional summons preserves mandatory heal and deck order");

        var germDuel = DemoDuel.FromPartialObservation(1, [], Slots(germ),
            Slots(opponent), 8000, side0Deck: [germ1, germ2]);
        germDuel.MonsterFaceDown[0, 0] = germDuel.DefensePosition[0, 0] = true;
        check(germDuel.BeginBattle(out _) && germDuel.Attack(0, 0, out _) &&
              germDuel.Life[1] == 7500 && germDuel.Life[0] == 8000 &&
              germDuel.PendingCardSelection is { Owner: 0, RequiredResourceId: 1114,
                  MaxSelections: 2, SummonPosition: CardSelectionPosition.FaceUpAttack } &&
              germDuel.SelectBatchCard(0, out _) && germDuel.SelectBatchCard(1, out _) &&
              germDuel.Monsters[0, 0] == germ1 && germDuel.Monsters[0, 1] == germ2 &&
              !germDuel.MonsterFaceDown[0, 0] && !germDuel.DefensePosition[0, 0] &&
              germDuel.DeckCount(0) == 0,
              "Giant Germ: opponent suffers mandatory 500 and 2 copies summon face-up attack");

        var empty = DemoDuel.FromPartialObservation(1, [], Slots(germ),
            Slots(opponent), 8000, side0Deck: [irrelevant]);
        empty.DefensePosition[0, 0] = true;
        check(empty.BeginBattle(out _) && empty.Attack(0, 0, out _) &&
              empty.PendingCardSelection is null && empty.Life[1] == 7500 &&
              empty.DeckSnapshot(0).Single() == irrelevant,
              "Giant Germ: mandatory damage applies even without extra copies");

        var oneSlot = DemoDuel.FromPartialObservation(1, [],
            Slots(momonga, irrelevant, irrelevant, irrelevant, irrelevant),
            Slots(opponent), 8000, side0Deck: [duplicate1, duplicate2]);
        oneSlot.DefensePosition[0, 0] = true;
        check(oneSlot.BeginBattle(out _) && oneSlot.Attack(0, 0, out _) &&
              oneSlot.PendingCardSelection is { MaxSelections: 1 } &&
              oneSlot.ResolveCardSelection(0, out _) &&
              oneSlot.PendingCardSelection is null &&
              oneSlot.Monsters[0, 0] == duplicate1 && oneSlot.MonsterFaceDown[0, 0] &&
              oneSlot.Monsters[0, 4] == irrelevant,
              "P168: available monster zones cap same-name summons to one");

        var tributeNoEffect = DemoDuel.FromPartialObservation(0, [tribute],
            Slots(germ), Slots(), 8000, side0Deck: [germ1]);
        check(tributeNoEffect.Summon(0, 0, 0, true, out _, [0]) &&
              tributeNoEffect.Graveyard(0).Single() == germ &&
              tributeNoEffect.PendingCardSelection is null &&
              tributeNoEffect.Life[1] == 8000 &&
              tributeNoEffect.DeckSnapshot(0).Single() == germ1,
              "Giant Germ: tribute is not battle destruction, no damage or summon");

        var flipThenRemoved = DemoDuel.FromPartialObservation(0, [],
            Slots(spear), Slots(bug), 8000,
            side0Graveyard: [ally, anotherSpear], side1Graveyard: [enemyGrave]);
        flipThenRemoved.MonsterFaceDown[0, 0] = flipThenRemoved.DefensePosition[0, 0] = true;
        flipThenRemoved.MonsterFaceDown[1, 0] = true;
        check(flipThenRemoved.RevealMonsterByEffect(0, 0, out _) &&
              flipThenRemoved.MonsterWasFlipped[0, 0] &&
              flipThenRemoved.PendingCardSelection is null &&
              flipThenRemoved.Graveyard(0).Count == 2,
              "Spear Cretin: FLIP marks history but never revives on reveal alone");
        check(flipThenRemoved.RevealMonsterByEffect(1, 0, out _) &&
              flipThenRemoved.ResolveFlipTarget(0, 0, out _) &&
              flipThenRemoved.Graveyard(0).Count == 3 &&
              flipThenRemoved.PendingCardSelection is { Owner: 0,
                  Source: CardSelectionSource.Graveyard,
                  Destination: CardSelectionDestination.Field,
                  Filter: CardSelectionFilter.AnyMonster,
                  MaySelectFaceDownDefense: true,
                  EffectResourceId: 1116, MinSelections: 1, MaxSelections: 1 } &&
              flipThenRemoved.PendingCardSelection.EligibleCards
                  .Select(x => x.SourceIndex).SequenceEqual([0, 1]) &&
              !flipThenRemoved.SkipOptionalCardSelection(out _) &&
              !flipThenRemoved.ResolveCardSelection(2, out _),
              "Spear Cretin: after later GY transition, owner selects one monster but not this source");
        check(flipThenRemoved.ResolveCardSelection(1, 4, true, out _) &&
              flipThenRemoved.Monsters[0, 4] == anotherSpear &&
              flipThenRemoved.MonsterFaceDown[0, 4] &&
              flipThenRemoved.DefensePosition[0, 4] &&
              !flipThenRemoved.MonsterWasFlipped[0, 4] &&
              flipThenRemoved.PendingCardSelection is { Owner: 1,
                  Destination: CardSelectionDestination.Field,
                  Source: CardSelectionSource.Graveyard } &&
              flipThenRemoved.ResolveCardSelection(0, null, false, out _) &&
              flipThenRemoved.Monsters[1, 1] == enemyGrave &&
              flipThenRemoved.PendingCardSelection is null &&
              flipThenRemoved.Graveyard(0).SequenceEqual([ally, spear]),
              "Spear Cretin: both owners choose separately; allow set DEF and attack; no self-revive");

        var unflipped = DemoDuel.FromPartialObservation(0, [],
            Slots(spear), Slots(bug), 8000,
            side0Graveyard: [ally], side1Graveyard: [enemyGrave]);
        unflipped.MonsterFaceDown[1, 0] = true;
        check(unflipped.RevealMonsterByEffect(1, 0, out _) &&
              unflipped.ResolveFlipTarget(0, 0, out _) &&
              unflipped.Graveyard(0).SequenceEqual([ally, spear]) &&
              unflipped.PendingCardSelection is null,
              "Spear Cretin: face-up without previous FLIP does not trigger on destruction");

        var battleSpear = DemoDuel.FromPartialObservation(1, [], Slots(spear),
            Slots(opponent), 8000, side0Graveyard: [ally], side1Graveyard: [enemyGrave]);
        battleSpear.MonsterFaceDown[0, 0] = battleSpear.DefensePosition[0, 0] = true;
        check(battleSpear.BeginBattle(out _) && battleSpear.Attack(0, 0, out _) &&
              battleSpear.PendingCardSelection is { Owner: 0, EffectResourceId: 1116 } &&
              battleSpear.Graveyard(0).SequenceEqual([ally, spear]) &&
              battleSpear.ResolveCardSelection(0, out _) &&
              battleSpear.PendingCardSelection is { Owner: 1, EffectResourceId: 1116 } &&
              battleSpear.ResolveCardSelection(0, out _) &&
              battleSpear.Monsters[0, 0] == ally && battleSpear.Monsters[1, 1] == enemyGrave,
              "Spear Cretin: revealing during battle triggers only after destruction and offers both sides");

        var nothingToRevive = DemoDuel.FromPartialObservation(1, [], Slots(spear),
            Slots(opponent), 8000);
        nothingToRevive.MonsterFaceDown[0, 0] = nothingToRevive.DefensePosition[0, 0] = true;
        check(nothingToRevive.BeginBattle(out _) && nothingToRevive.Attack(0, 0, out _) &&
              nothingToRevive.PendingCardSelection is null &&
              nothingToRevive.Graveyard(0).Single() == spear,
              "Spear Cretin: source alone in graveyard cannot self-special-summon");
    }
}
