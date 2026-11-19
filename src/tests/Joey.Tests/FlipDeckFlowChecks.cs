using Joey.Core;

internal static class FlipDeckFlowChecks
{
    private static Card Monster(ushort id, int index, int atk = 800, int def = 500) =>
        new(index, id, $"Monster {id}", "", (3u << 20) | (4u << 25) |
            ((uint)(atk / 10) << 9) | (uint)(def / 10));
    private static Card Spell(int index) =>
        new(index, (ushort)(400 + index), $"Spell {index}", "", 22u << 20);
    private static Card Trap(int index) =>
        new(index, (ushort)(800 + index), $"Trap {index}", "", 21u << 20);
    private static Card?[] Slots(Card? first = null) =>
        [first, null, null, null, null];

    public static void Run(Action<bool, string> check)
    {
        var worm = Monster(561, 674, 750, 600);
        var scout = Monster(610, 18, 650, 500);
        var attacker = Monster(100, 250, 2200, 2000);
        var monsterA = Monster(200, 401);
        var monsterB = Monster(201, 402);
        var spellA = Spell(10);
        var spellB = Spell(11);
        var trap = Trap(12);
        var next = Monster(202, 403);

        check(FlipEffectRules.Effect(561) == DemoFlipEffect.MillOpponentDeck &&
              FlipEffectRules.Effect(610) == DemoFlipEffect.DrawAndDiscardOpponentSpells &&
              FlipEffectRules.Supports(561) && FlipEffectRules.Supports(610) &&
              FlipEffectRules.Effect(674) == DemoFlipEffect.None &&
              FlipEffectRules.Effect(18) == DemoFlipEffect.None,
              "P082/P088: dispatch original resource IDs, not internal card indexes");

        var summon = DemoDuel.FromPartialObservation(0, [worm], Slots(), Slots(), 8000,
            side0Deck: [monsterB], side1Deck: [monsterA, spellA, trap, monsterB, spellB, next]);
        check(summon.SetFromHand(0, out _) && summon.MonsterFaceDown[0, 0] &&
              summon.LastDeckFlow is null,
              "Needle Worm: set does not prematurely mill");
        summon.NextTurn(); summon.NextTurn();
        check(summon.ChangePosition(0, out _) &&
              summon.PendingFlipTarget is null && summon.PendingCardSelection is null &&
              summon.LastDeckFlow is { EffectResourceId: 561, Controller: 0,
                  AffectedPlayer: 1, Requested: 5, MovedFromDeck: 5,
                  KeptInHand: 0, SentToGraveyard: 5,
                  NewlyDrawnSpellsDiscarded: 0, DrawFailed: false } &&
              summon.Graveyard(1).SequenceEqual([monsterA, spellA, trap, monsterB, spellB]) &&
              summon.DeckSnapshot(1).SequenceEqual([next]) &&
              summon.DeckSnapshot(0).SequenceEqual([monsterB]) &&
              summon.HandFor(1).Count == 0 &&
              summon.Life.SequenceEqual([8000, 8000]) && !summon.ChangePosition(0, out _),
              "Needle Worm: FLIP summon mills five ordered cards, not draws; no hand/LP change");

        var shortDeck = DemoDuel.FromPartialObservation(1, [], Slots(worm),
            Slots(attacker), 8000, side0Deck: [next], side1Deck: [monsterA, spellA]);
        shortDeck.MonsterFaceDown[0, 0] = shortDeck.DefensePosition[0, 0] = true;
        check(shortDeck.BeginBattle(out _) && shortDeck.Attack(0, 0, out _) &&
              shortDeck.LastDeckFlow is { Controller: 0, AffectedPlayer: 1,
                  MovedFromDeck: 2, SentToGraveyard: 2, DrawFailed: false } &&
              shortDeck.Graveyard(1).SequenceEqual([monsterA, spellA]) &&
              shortDeck.DeckCount(1) == 0 &&
              shortDeck.Monsters[0, 0] is null &&
              shortDeck.Graveyard(0).Single() == worm &&
              !shortDeck.GameOver && shortDeck.Life.SequenceEqual([8000, 8000]),
              "Needle Worm: destroyed in battle but still mills available cards; empty deck alone is not defeat");
        check(shortDeck.EndTurn(out _) && shortDeck.EndTurn(out _) &&
              shortDeck.ActivePlayer == 1 &&
              shortDeck.Draw(out _) && shortDeck.Winner == 0 && shortDeck.GameOver,
              "Needle Worm: after milling to zero, mandatory later draw causes deck-out");

        var reveal = DemoDuel.FromPartialObservation(0, [], Slots(worm), Slots(), 8000,
            side1Deck: [monsterA, spellA, trap]);
        reveal.MonsterFaceDown[0, 0] = true;
        check(reveal.RevealMonsterByEffect(0, 0, out _) &&
              reveal.LastDeckFlow is { MovedFromDeck: 3 } &&
              reveal.Graveyard(1).SequenceEqual([monsterA, spellA, trap]) &&
              !reveal.RevealMonsterByEffect(0, 0, out _) &&
              reveal.Graveyard(1).Count == 3,
              "Needle Worm: external reveal mills only once and cannot retrigger face-up monster");

        var hiro = DemoDuel.FromPartialObservation(1, [spellA, monsterB],
            Slots(scout), Slots(attacker), 8000,
            side1Deck: [spellA, trap, spellB, next]);
        hiro.MonsterFaceDown[0, 0] = hiro.DefensePosition[0, 0] = true;
        check(hiro.BeginBattle(out _) && hiro.Attack(0, 0, out _) &&
              hiro.LastDeckFlow is { EffectResourceId: 610, Controller: 0,
                  AffectedPlayer: 1, Requested: 3, MovedFromDeck: 3,
                  KeptInHand: 1, SentToGraveyard: 2,
                  NewlyDrawnSpellsDiscarded: 2, DrawFailed: false } &&
              hiro.HandFor(1).SequenceEqual([spellA, monsterB, trap]) &&
              hiro.Graveyard(1).SequenceEqual([spellA, spellB]) &&
              hiro.DeckSnapshot(1).SequenceEqual([next]) &&
              hiro.Graveyard(0).Single() == scout &&
              hiro.Monsters[0, 0] is null &&
              hiro.Life.SequenceEqual([8000, 8000]),
              "Hiro's Shadow Scout: enemy draws 3, discards only newly drawn Spells, preserves existing identical Spell");

        var noSpells = DemoDuel.FromPartialObservation(0, [], Slots(), Slots(scout), 8000,
            side0Deck: [monsterA, trap, monsterB, next]);
        noSpells.MonsterFaceDown[1, 0] = true;
        check(noSpells.RevealMonsterByEffect(1, 0, out _) &&
              noSpells.LastDeckFlow is { Controller: 1, AffectedPlayer: 0,
                  MovedFromDeck: 3, KeptInHand: 3, SentToGraveyard: 0 } &&
              noSpells.HandFor(0).SequenceEqual([monsterA, trap, monsterB]) &&
              noSpells.DeckSnapshot(0).SequenceEqual([next]) &&
              noSpells.Graveyard(0).Count == 0 &&
              !noSpells.RevealMonsterByEffect(1, 0, out _),
              "Hiro's Shadow Scout: non-Spells stay in affected player's hand, works in opposite direction");

        var lowDeck = DemoDuel.FromPartialObservation(1, [monsterB],
            Slots(scout), Slots(attacker), 8000, side1Deck: [spellA, trap]);
        lowDeck.MonsterFaceDown[0, 0] = lowDeck.DefensePosition[0, 0] = true;
        check(lowDeck.BeginBattle(out _) && lowDeck.Attack(0, 0, out _) &&
              lowDeck.LastDeckFlow is { Requested: 3, MovedFromDeck: 2,
                  KeptInHand: 1, SentToGraveyard: 1,
                  NewlyDrawnSpellsDiscarded: 1, DrawFailed: true } &&
              lowDeck.DeckCount(1) == 0 &&
              lowDeck.HandFor(1).SequenceEqual([monsterB, trap]) &&
              lowDeck.Graveyard(1).Single() == spellA &&
              lowDeck.Winner == 0 && lowDeck.GameOver &&
              !lowDeck.Draw(out _) && !lowDeck.EndTurn(out _),
              "Hiro's Shadow Scout: insufficient forced draw loses for drawer (explicit sandbox policy)");

        var emptyHiro = DemoDuel.FromPartialObservation(0, [],
            Slots(), Slots(scout), 8000);
        emptyHiro.MonsterFaceDown[1, 0] = true;
        check(emptyHiro.RevealMonsterByEffect(1, 0, out _) &&
              emptyHiro.LastDeckFlow is { MovedFromDeck: 0,
                  SentToGraveyard: 0, DrawFailed: true } &&
              emptyHiro.Winner == 1 && emptyHiro.GameOver,
              "Hiro's Shadow Scout: forced draw from fully empty deck loses; no fabricated cards");

        // Regression: moved cards become part of each owner's physical GY and
        // remain eligible to existing graveyard-recovery effects.
        var faith = Monster(427, 471);
        var composition = DemoDuel.FromPartialObservation(0, [faith], Slots(),
            Slots(scout), 8000, side0Deck: [spellA, monsterA, trap]);
        composition.MonsterFaceDown[1, 0] = true;
        check(composition.RevealMonsterByEffect(1, 0, out _) &&
              composition.Graveyard(0).Single() == spellA &&
              composition.SetFromHand(0, out _),
              "Hiro's Shadow Scout: discarded Spells populate graveyard for later Magician of Faith");
    }
}
