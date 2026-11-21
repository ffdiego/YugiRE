using Joey.Core;

internal static class MonsterWipeChecks
{
    private static Card Monster(ushort id, int index, int atk = 1000, int def = 800) =>
        new(index, id, $"Monster {id}", "", (3u << 20) | (4u << 25) |
            ((uint)(atk / 10) << 9) | (uint)(def / 10));
    private static Card Spell(ushort id, int index, int classCode = 22) =>
        new(index, id, $"Spell {id}", "", (uint)classCode << 20);
    private static Card?[] Slots(Card? a = null, Card? b = null, Card? c = null) =>
        [a, b, c, null, null];

    public static void Run(Action<bool, string> check)
    {
        var darkHole = Spell(335, 850);
        var raigeki = Spell(336, 851);
        var invalid = Spell(336, 852, classCode: 21);
        var own = Monster(24, 50);
        var attacker = Monster(25, 51, 1900, 1900);
        var worm = Monster(561, 674);
        var tomato = Monster(1123, 940, 1400, 1100);
        var sangan = Monster(47, 248, 1000, 600);
        var witch = Monster(573, 261, 1100, 1200);
        var lowAtk = Monster(100, 200, 1500, 1900);
        var lowDef = Monster(101, 201, 1900, 1500);
        var spear = Monster(1116, 516, 500, 500);

        check(MonsterWipeRules.Scope(335) == MonsterWipeScope.BothPlayers &&
              MonsterWipeRules.Scope(336) == MonsterWipeScope.OpponentOnly &&
              MonsterWipeRules.Scope(1001) is null &&
              MonsterWipeRules.Scope(345) is null &&
              MonsterWipeRules.Targets(335, 0,
                  new Card?[2, 5], new bool[2, 5]).Count == 0,
              "P037/P039: exact resource IDs and separate all/opponent scopes");

        var raigekiDuel = DemoDuel.FromPartialObservation(0, [raigeki],
            Slots(own), Slots(attacker, worm, tomato), 8000, side1Deck: [own]);
        List<DemoMonsterDestruction> destructions = [];
        raigekiDuel.MonsterDestroyed += destructions.Add;
        raigekiDuel.MonsterFaceDown[1, 1] = true;
        check(raigekiDuel.CanActivateFromHand(0) &&
              raigekiDuel.Play(0, out _) &&
              raigekiDuel.LastMonsterWipe is { EffectResourceId: 336,
                  Caster: 0, DestroyedPlayer0: 0, DestroyedPlayer1: 3,
                  DestroyedFaceDown: 1, TotalDestroyed: 3 } &&
              raigekiDuel.Monsters[0, 0] == own &&
              Enumerable.Range(0, 5).All(z => raigekiDuel.Monsters[1, z] is null) &&
              raigekiDuel.Graveyard(0).Single() == raigeki &&
              raigekiDuel.Graveyard(1).SequenceEqual([attacker, worm, tomato]) &&
              raigekiDuel.LastDeckFlow is null && raigekiDuel.PendingCardSelection is null &&
              raigekiDuel.DeckSnapshot(1).Single() == own &&
              raigekiDuel.Life.SequenceEqual([8000, 8000]),
              "Raigeki: kills all opposing monsters including facedown, preserves own field, no FLIP or battle triggers");
        check(!raigekiDuel.Play(0, out _) && !raigekiDuel.CanActivateFromHand(0),
              "Raigeki: spell is spent exactly once");
        check(destructions.SequenceEqual([
                  new(1, 0, attacker, false, false),
                  new(1, 1, worm, true, false),
                  new(1, 2, tomato, false, false)]),
              "Raigeki: emits one ordered destruction snapshot per monster");

        var noEnemy = DemoDuel.FromPartialObservation(0, [raigeki], Slots(own),
            Slots(), 8000);
        check(!noEnemy.CanActivateFromHand(0) && !noEnemy.Play(0, out _) &&
              noEnemy.Hand.Single() == raigeki && noEnemy.Graveyard(0).Count == 0 &&
              noEnemy.Monsters[0, 0] == own && noEnemy.LastMonsterWipe is null,
              "Raigeki: activation with no opponent monsters is rejected atomically");

        var hole = DemoDuel.FromPartialObservation(0, [darkHole],
            Slots(sangan, own), Slots(witch, tomato), 8000,
            side0Deck: [lowAtk, lowDef], side1Deck: [lowDef, lowAtk]);
        hole.MonsterFaceDown[0, 1] = hole.MonsterFaceDown[1, 1] = true;
        check(hole.CanActivateFromHand(0) && hole.Play(0, out _) &&
              hole.LastMonsterWipe is { EffectResourceId: 335,
                  DestroyedPlayer0: 2, DestroyedPlayer1: 2, DestroyedFaceDown: 2 } &&
              Enumerable.Range(0, 5).All(z =>
                  hole.Monsters[0, z] is null && hole.Monsters[1, z] is null) &&
              hole.Graveyard(0).SequenceEqual([darkHole, sangan, own]) &&
              hole.Graveyard(1).SequenceEqual([witch, tomato]) &&
              hole.PendingCardSelection is { Owner: 0, EffectResourceId: 47,
                  Source: CardSelectionSource.Deck, Destination: CardSelectionDestination.Hand,
                  Filter: CardSelectionFilter.MonsterAttackAtMost1500 } &&
              hole.PendingCardSelection.EligibleCards.Single().Card == lowAtk &&
              hole.HandFor(0).Count == 0 && hole.HandFor(1).Count == 0,
              "Dark Hole: simultaneous field destruction finishes before opening Sangan's search");
        var firstIndex = hole.PendingCardSelection!.EligibleCards.Single().SourceIndex;
        check(!hole.EndTurn(out _) &&
              hole.ResolveCardSelection(firstIndex, out _) &&
              hole.HandFor(0).Single() == lowAtk &&
              hole.PendingCardSelection is { Owner: 1, EffectResourceId: 573,
                  Filter: CardSelectionFilter.MonsterDefenseAtMost1500 } &&
              hole.PendingCardSelection.EligibleCards.Single().Card == lowDef,
              "Dark Hole: queue Witch after Sangan, preserving independent player decks");
        int secondIndex = hole.PendingCardSelection!.EligibleCards.Single().SourceIndex;
        check(hole.ResolveCardSelection(secondIndex, out _) &&
              hole.PendingCardSelection is null &&
              hole.HandFor(1).Single() == lowDef &&
              hole.DeckCount(0) == 1 && hole.DeckCount(1) == 1 &&
              hole.Life.SequenceEqual([8000, 8000]),
              "Dark Hole: all graveyard searches drain once, with no LP or battle effects");

        var setSpell = DemoDuel.FromPartialObservation(0, [darkHole],
            Slots(own), Slots(attacker), 8000);
        check(setSpell.SetFromHand(0, out _) && setSpell.Spells[0, 0] == darkHole &&
              setSpell.SpellFaceDown[0, 0] &&
              setSpell.LastMonsterWipe is null && setSpell.Monsters[0, 0] == own,
              "Dark Hole: setting a normal Spell never destroys monsters");
        check(setSpell.ActivateSetSpell(0, out _) &&
              setSpell.Spells[0, 0] is null && !setSpell.SpellFaceDown[0, 0] &&
              setSpell.Graveyard(0).SequenceEqual([darkHole, own]) &&
              setSpell.Graveyard(1).Single() == attacker &&
              setSpell.LastMonsterWipe is { TotalDestroyed: 2 },
              "Dark Hole: activation from a set card spends card once and clears both fields");
        check(!setSpell.ActivateSetSpell(0, out _) && setSpell.Graveyard(0).Count == 2,
              "Dark Hole: set activation is not repeatable");

        var emptyBoard = DemoDuel.FromPartialObservation(0, [darkHole],
            Slots(), Slots(), 8000);
        check(!emptyBoard.CanActivateFromHand(0) && !emptyBoard.Play(0, out _) &&
              emptyBoard.SetFromHand(0, out _) &&
              !emptyBoard.ActivateSetSpell(0, out _) &&
              emptyBoard.Spells[0, 0] == darkHole &&
              emptyBoard.SpellFaceDown[0, 0] &&
              emptyBoard.Graveyard(0).Count == 0,
              "Dark Hole: no monsters means no activation, even from set card");

        var wrongClass = DemoDuel.FromPartialObservation(0, [invalid],
            Slots(), Slots(attacker), 8000);
        check(!wrongClass.CanActivateFromHand(0) &&
              !wrongClass.Play(0, out _) &&
              wrongClass.Hand.Single() == invalid &&
              wrongClass.Monsters[1, 0] == attacker,
              "P039: resource ID without DAT Spell class cannot invoke wipe");

        var space = DemoDuel.FromPartialObservation(0, [raigeki],
            Slots(), Slots(attacker), 8000);
        for (int z = 0; z < 5; z++)
        {
            space.Spells[0, z] = Spell((ushort)(400 + z), 400 + z);
            space.SpellFaceDown[0, z] = true;
        }
        check(!space.CanActivateFromHand(0) && !space.Play(0, out _) &&
              space.Hand.Single() == raigeki &&
              space.Monsters[1, 0] == attacker &&
              space.LastMonsterWipe is null,
              "Raigeki: no free Spell/Trap zone prevents hand activation");

        var spearDuel = DemoDuel.FromPartialObservation(0, [darkHole],
            Slots(spear), Slots(attacker), 8000,
            side0Graveyard: [own], side1Graveyard: [lowDef]);
        spearDuel.MonsterWasFlipped[0, 0] = true;
        check(spearDuel.Play(0, out _) &&
              spearDuel.PendingCardSelection is { Owner: 0, EffectResourceId: 1116,
                  Source: CardSelectionSource.Graveyard,
                  Destination: CardSelectionDestination.Field } &&
              spearDuel.Graveyard(0).SequenceEqual([own, darkHole, spear]) &&
              spearDuel.Graveyard(1).SequenceEqual([lowDef, attacker]) &&
              spearDuel.PendingCardSelection.EligibleCards.Single().Card == own &&
              spearDuel.ResolveCardSelection(0, out _) &&
              spearDuel.PendingCardSelection is { Owner: 1, EffectResourceId: 1116 } &&
              spearDuel.ResolveCardSelection(0, out _) &&
              spearDuel.Monsters[0, 0] == own && spearDuel.Monsters[1, 0] == lowDef,
              "Dark Hole: previously flipped Spear Cretin starts own and opponent graveyard selections after wipe");
    }
}
