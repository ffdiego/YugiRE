using Joey.Core;

internal static class FlipBanishBurnChecks
{
    private static Card Monster(ushort id, int index, int atk = 600, int def = 700) =>
        new(index, id, id switch {
            1435 => "Lady Assailant of Flames", 1436 => "Fire Sorcerer", _ => $"Monster {id}" },
            "", (10u << 20) | (4u << 25) | ((uint)(atk / 10) << 9) | (uint)(def / 10));
    private static Card Spell(int i) => new(2000+i, (ushort)(400+i), $"Spell {i}", "", 22u << 20);
    private static Card?[] Slots(Card? a = null) => [a, null, null, null, null];
    private static void Hide(DemoDuel d, int side) =>
        (d.MonsterFaceDown[side, 0], d.DefensePosition[side, 0]) = (true, true);

    public static void Run(Action<bool,string> check)
    {
        var lady = Monster(1435, 855);
        var fire = Monster(1436, 767);
        var other = Monster(200, 900);
        var a = Spell(1); var b = Spell(2); var c = Spell(3); var d = Spell(4);
        check(FlipEffectRules.Effect(1435) == DemoFlipEffect.BanishThreeDeckBurn800 &&
              FlipEffectRules.Effect(1436) == DemoFlipEffect.BanishTwoRandomHandBurn800 &&
              FlipEffectRules.Supports(1435) && FlipEffectRules.Supports(1436) &&
              FlipEffectRules.Effect(855) == DemoFlipEffect.None &&
              FlipBanishBurnRules.Cost(1435) == FlipBanishCostSource.Deck &&
              FlipBanishBurnRules.Cost(1436) == FlipBanishCostSource.Hand &&
              FlipBanishBurnRules.Cost(855) is null,
              "P051/P052: original resource IDs dispatch distinct banishment costs");

        var deck = DemoDuel.FromPartialObservation(0, [other], Slots(lady), Slots(), 8000,
            side0Deck: [a,b,c,d], effectShuffleSeed: 777);
        Hide(deck, 0);
        check(deck.ChangePosition(0,out _) &&
              deck.LastFlipBanishBurn is { EffectResourceId: 1435, Owner: 0,
                  CostSource: FlipBanishCostSource.Deck, RequiredCount: 3,
                  Applied: true, Damage: 800 } &&
              deck.LastFlipBanishBurn.BanishedCards.SequenceEqual([a,b,c]) &&
              deck.Banished(0).SequenceEqual([a,b,c]) &&
              deck.DeckSnapshot(0).SequenceEqual([d]) && deck.HandFor(0).SequenceEqual([other]) &&
              deck.Graveyard(0).Count == 0 && deck.Life.SequenceEqual([8000,7200]) &&
              !deck.ChangePosition(0,out _),
              "P051: banishes exactly top three Deck occurrences, burns once, preserves Hand and GY");

        foreach (int count in new[] {0,1,2})
        {
            Card[] initial = new[] { a, b, c }.Take(count).ToArray();
            var shortDeck = DemoDuel.FromPartialObservation(0, [], Slots(lady), Slots(), 8000,
                side0Deck: initial);
            Hide(shortDeck,0);
            check(shortDeck.ChangePosition(0,out _) &&
                  shortDeck.LastFlipBanishBurn is { Applied: false, Damage: 0,
                      RequiredCount: 3, BanishedCards.Count: 0 } &&
                  shortDeck.Banished(0).Count == 0 &&
                  shortDeck.DeckSnapshot(0).SequenceEqual(initial) &&
                  shortDeck.Life.SequenceEqual([8000,8000]) && !shortDeck.GameOver,
                  $"P051: {count} cards cannot pay three-card Deck cost; no mutation or deck-out");
        }

        Card[] fullHand = [a,b,c,d];
        var random1 = DemoDuel.FromPartialObservation(0, fullHand,
            Slots(fire), Slots(), 8000, effectShuffleSeed: 4242);
        Hide(random1,0);
        check(random1.ChangePosition(0,out _) &&
              random1.LastFlipBanishBurn is { CostSource: FlipBanishCostSource.Hand,
                  RequiredCount: 2, Applied: true, Damage: 800 } &&
              random1.Banished(0).Count == 2 &&
              random1.Banished(0).Distinct().Count() == 2 &&
              random1.Banished(0).All(card => fullHand.Contains(card)) &&
              random1.HandFor(0).Count == 2 &&
              random1.HandFor(0).Concat(random1.Banished(0)).ToHashSet().SetEquals(fullHand) &&
              random1.DeckCount(0) == 0 && random1.Graveyard(0).Count == 0 &&
              random1.Life.SequenceEqual([8000,7200]),
              "P052: two distinct random Hand occurrences banished, no discards or Deck movement");
        var random2 = DemoDuel.FromPartialObservation(0, fullHand, Slots(fire), Slots(), 8000,
            effectShuffleSeed:4242);
        Hide(random2,0);
        check(random2.ChangePosition(0,out _) &&
              random2.Banished(0).SequenceEqual(random1.Banished(0)) &&
              random2.HandFor(0).SequenceEqual(random1.HandFor(0)),
              "P052: same sandbox RNG seed produces same sampled occurrences");
        var random3 = DemoDuel.FromPartialObservation(0, [a,a,b,c], Slots(fire), Slots(), 8000,
            effectShuffleSeed:1122);
        Hide(random3,0);
        check(random3.ChangePosition(0,out _) &&
              random3.HandFor(0).Count == 2 &&
              random3.Banished(0).Count == 2 &&
              random3.HandFor(0).Count(x => ReferenceEquals(x,a)) +
              random3.Banished(0).Count(x => ReferenceEquals(x,a)) == 2,
              "P052: duplicate card occurrences remain distinct hand positions");

        foreach (int count in new[] {0,1})
        {
            Card[] hand = new[] { a, b }.Take(count).ToArray();
            var shortHand = DemoDuel.FromPartialObservation(0, hand, Slots(fire), Slots(), 8000);
            Hide(shortHand,0);
            check(shortHand.ChangePosition(0,out _) &&
                  shortHand.LastFlipBanishBurn is { Applied:false, Damage:0,
                      RequiredCount:2, BanishedCards.Count:0 } &&
                  shortHand.HandFor(0).SequenceEqual(hand) &&
                  shortHand.Banished(0).Count == 0 && shortHand.Life.SequenceEqual([8000,8000]),
                  $"P052: {count} Hand cards cannot pay two-card cost; no mutation");
        }

        var opp = DemoDuel.FromPartialObservation(1, [a,b,c], Slots(), Slots(fire), 8000,
            effectShuffleSeed:77);
        Hide(opp,1);
        check(opp.ChangePosition(0,out _) &&
              opp.LastFlipBanishBurn is { Owner:1, Applied:true } &&
              opp.Banished(1).Count == 2 && opp.Banished(0).Count == 0 &&
              opp.Life.SequenceEqual([7200,8000]),
              "P052: player 1 pays from own Hand and burns opponent player 0");

        var reveal = DemoDuel.FromPartialObservation(0, [], Slots(lady), Slots(), 8000,
            side0Deck:[a,b,c,d]);
        Hide(reveal,0);
        check(reveal.RevealMonsterByEffect(0,0,out _) &&
              reveal.Banished(0).SequenceEqual([a,b,c]) &&
              !reveal.RevealMonsterByEffect(0,0,out _) &&
              reveal.Banished(0).Count == 3 &&
              reveal.Monsters[0,0] == lady,
              "P051: external revelation follows FLIP path once");

        var noFlip = DemoDuel.FromPartialObservation(0, [], Slots(lady), Slots(), 8000,
            side0Deck:[a,b,c]);
        check(noFlip.ChangePosition(0,out _) &&
              noFlip.Banished(0).Count == 0 &&
              noFlip.LastFlipBanishBurn is null,
              "P051: ordinary face-up position change does not trigger FLIP");
        var negated = DemoDuel.FromPartialObservation(0, [a,b], Slots(fire), Slots(), 8000);
        Hide(negated,0);
        negated.Spells[1,0] = new Card(1234,1530,"Royal Command","",21u<<20);
        check(negated.ChangePosition(0,out _) &&
              negated.MonsterWasFlipped[0,0] &&
              negated.Banished(0).Count == 0 && negated.LastFlipBanishBurn is null &&
              negated.HandFor(0).SequenceEqual([a,b]) &&
              negated.Life.SequenceEqual([8000,8000]),
              "Royal Command: negation prevents banishment cost and 800 damage");

        var attacker = Monster(150, 1234, atk:2400, def:1200);
        var battle = DemoDuel.FromPartialObservation(1, [], Slots(lady), Slots(attacker), 8000,
            side0Deck:[a,b,c,d]);
        Hide(battle,0);
        check(battle.BeginBattle(out _) && battle.Attack(0,0,out _) &&
              battle.LastFlipBanishBurn is { Applied:true, Owner:0 } &&
              battle.Banished(0).SequenceEqual([a,b,c]) &&
              battle.Graveyard(0).SequenceEqual([lady]) && battle.Monsters[0,0] is null &&
              battle.Life.SequenceEqual([8000,7200]),
              "P051: battle FLIP banishes before deferred defender destruction");

        var lethal = DemoDuel.FromPartialObservation(0, [], Slots(lady), Slots(), 8000,
            side0Deck:[a,b,c]);
        Hide(lethal,0);
        lethal.Life[1] = 750;
        check(lethal.ChangePosition(0,out _) &&
              lethal.Life[1] == 0 && lethal.Winner == 0 &&
              lethal.Banished(0).Count == 3 && lethal.Graveyard(0).Count == 0,
              "P051: 800 burn uses standard LP clamp and winning condition");
    }
}
