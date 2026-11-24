using Joey.Core;

internal static class BattleRecruiterChecks
{
    private static Card Monster(ushort id, int index, byte attribute, int atk = 1200, int def = 800) =>
        new(index, id, $"Monster {id}", "", (3u << 20) | (4u << 25) |
            ((uint)attribute << 29) | ((uint)(atk / 10) << 9) | (uint)(def / 10));
    private static Card?[] Slots(Card? first = null, Card? second = null, Card? third = null,
        Card? fourth = null, Card? fifth = null) => [first, second, third, fourth, fifth];

    public static void Run(Action<bool, string> check)
    {
        var recruiters = new (ushort Resource, byte Attribute)[]
        {
            (1123,2), (1108,5), (1117,1), (1110,4), (1119,3), (1120,6)
        };
        var attacker = Monster(800, 1300, 1, 2200, 2000);

        check(recruiters.All(r => CardSelectionRules.BattleRecruitAttribute(r.Resource) == r.Attribute) &&
              CardSelectionRules.BattleRecruitAttribute(47) is null &&
              CardSelectionRules.BattleRecruitAttribute(573) is null &&
              CardSelectionRules.BattleRecruitAttribute(590) is null,
              "P164: six exact original resource IDs map to LIGHT=1 DARK=2 WATER=3 FIRE=4 EARTH=5 WIND=6");

        foreach (var (resource, attribute) in recruiters)
        {
            var recruiter = Monster(resource, (int)resource + 50, attribute, 1400, 1000);
            var legal = Monster(900, 650, attribute, 1500, 2000);
            var tooStrong = Monster(901, 651, attribute, 1600, 900);
            var different = Monster(902, 652, attribute == 6 ? (byte)1 : (byte)(attribute + 1), 1000, 700);
            var spell = new Card(653, 342, "Spell", "", (22u << 20) | ((uint)attribute << 29));
            check(CardSelectionRules.IsEligible(legal, CardSelectionFilter.MonsterAttributeAttackAtMost1500, attribute) &&
                  !CardSelectionRules.IsEligible(tooStrong, CardSelectionFilter.MonsterAttributeAttackAtMost1500, attribute) &&
                  !CardSelectionRules.IsEligible(different, CardSelectionFilter.MonsterAttributeAttackAtMost1500, attribute) &&
                  !CardSelectionRules.IsEligible(spell, CardSelectionFilter.MonsterAttributeAttackAtMost1500, attribute) &&
                  !CardSelectionRules.IsEligible(legal, CardSelectionFilter.MonsterAttributeAttackAtMost1500),
                  $"P164 card {resource}: only matching-attribute monsters ATK <=1500, never missing-attribute");

            var duel = DemoDuel.FromPartialObservation(1, [], Slots(recruiter), Slots(attacker), 8000,
                side0Deck: [tooStrong, different, legal, spell], effectShuffleSeed: 77);
            check(duel.BeginBattle(out _) && duel.Attack(0, 0, out _) &&
                  duel.PendingCardSelection is { Owner: 0,
                      Source: CardSelectionSource.Deck, Destination: CardSelectionDestination.Field,
                      MinSelections: 0, MaxSelections: 1,
                      Filter: CardSelectionFilter.MonsterAttributeAttackAtMost1500 } &&
                  duel.PendingCardSelection.EffectResourceId == resource &&
                  duel.PendingCardSelection.RequiredAttributeCode == attribute &&
                  duel.PendingCardSelection.EligibleCards.Single().SourceIndex == 2 &&
                  duel.Graveyard(0).Single() == recruiter && duel.Monsters[0, 0] is null &&
                  !duel.EndTurn(out _),
                  $"P164 card {resource}: destruction in battle opens optional deck->field selection");
            check(!duel.ResolveCardSelection(0, out _) && !duel.ResolveCardSelection(1, out _) &&
                  !duel.ResolveCardSelection(3, out _) && duel.DeckCount(0) == 4 &&
                  duel.PendingCardSelection is not null,
                  $"P164 card {resource}: cannot choose high ATK, wrong attribute or nonmonster");
            check(duel.ResolveCardSelection(2, out _) &&
                  duel.PendingCardSelection is null && duel.DeckCount(0) == 3 &&
                  duel.Monsters[0, 0] == legal && !duel.MonsterFaceDown[0, 0] &&
                  !duel.DefensePosition[0, 0] && duel.HandFor(0).Count == 0 &&
                  !duel.DeckSnapshot(0).Contains(legal),
                  $"P164 card {resource}: eligible monster Special Summoned face-up attack and deck shuffles");
        }

        var tomato = Monster(1123, 501, 2, 1400, 1100);
        var dark = Monster(204, 503, 2, 1000, 1000);
        var big = Monster(206, 504, 1, 2000, 1600);
        var optional = DemoDuel.FromPartialObservation(1, [], Slots(tomato), Slots(attacker), 8000,
            side0Deck: [dark, big]);
        check(optional.BeginBattle(out _) && optional.Attack(0, 0, out _) &&
              optional.PendingCardSelection is { MinSelections: 0 } &&
              !optional.EndTurn(out _), "P164: optional choice blocks other actions until resolved");
        var order = optional.DeckSnapshot(0).ToArray();
        check(optional.SkipOptionalCardSelection(out _) &&
              optional.PendingCardSelection is null &&
              optional.DeckSnapshot(0).SequenceEqual(order) &&
              optional.DeckCount(0) == 2 && optional.HandFor(0).Count == 0 &&
              optional.Monsters[0, 0] is null &&
              !optional.SkipOptionalCardSelection(out _),
              "P164: owner may skip without deck shuffle or special summon");

        var noTarget = DemoDuel.FromPartialObservation(1, [], Slots(tomato), Slots(attacker), 8000,
            side0Deck: [big]);
        check(noTarget.BeginBattle(out _) && noTarget.Attack(0, 0, out _) &&
              noTarget.PendingCardSelection is null &&
              noTarget.Graveyard(0).Single() == tomato &&
              noTarget.DeckSnapshot(0).Single() == big,
              "P164: no matching monster means no pending selection");

        var allFull = DemoDuel.FromPartialObservation(1, [],
            Slots(tomato, big, big, big, big), Slots(attacker), 8000, side0Deck: [dark]);
        check(allFull.BeginBattle(out _) && allFull.Attack(0, 0, out _) &&
              allFull.PendingCardSelection is { Destination: CardSelectionDestination.Field } &&
              allFull.ResolveCardSelection(0, 0, out _) &&
              allFull.Monsters[0, 0] == dark &&
              allFull.Monsters[0, 1] == big,
              "P164: destroyed recruiter's slot becomes free and can be chosen explicitly");

        var wrongSlot = DemoDuel.FromPartialObservation(1, [],
            Slots(tomato, big), Slots(attacker), 8000, side0Deck: [dark]);
        check(wrongSlot.BeginBattle(out _) && wrongSlot.Attack(0, 0, out _) &&
              !wrongSlot.ResolveCardSelection(0, 1, out _) &&
              !wrongSlot.ResolveCardSelection(0, 5, out _) &&
              wrongSlot.DeckCount(0) == 1 &&
              wrongSlot.PendingCardSelection is not null &&
              wrongSlot.ResolveCardSelection(0, 4, out _) &&
              wrongSlot.Monsters[0, 4] == dark,
              "P164: invalid/occupied target zone never consumes card; valid explicit zone honored");

        var effect = DemoDuel.FromPartialObservation(0, [],
            Slots(Monster(500, 505, 1)), Slots(tomato), 8000, side1Deck: [dark]);
        effect.MonsterFaceDown[0, 0] = true;
        check(effect.RevealMonsterByEffect(0, 0, out _) &&
              effect.ResolveFlipTarget(1, 0, out _) &&
              effect.Graveyard(1).Single() == tomato &&
              effect.PendingCardSelection is null &&
              effect.Monsters[1, 0] is null,
              "P164: destruction by Man-Eater Bug does not recruit (battle only)");

        var tribute = DemoDuel.FromPartialObservation(0, [Monster(202, 507, 1, 2400, 1800) with
            { Properties = (1u << 20) | (6u << 25) | (1u << 29) | (240u << 9) | 180u }],
            Slots(tomato), Slots(), 8000, side0Deck: [dark]);
        check(tribute.Summon(0, 0, 0, true, out _, [0]) &&
              tribute.PendingCardSelection is null &&
              tribute.Graveyard(0).Single() == tomato &&
              tribute.DeckCount(0) == 1,
              "P164: sending recruiter as tribute never activates battle-only effect");

        // If a battle destroys both sides, both eligible recruiters must be
        // serialized rather than discarded or accidentally overwritten.
        var first = Monster(1108, 509, 5, 1400, 900);
        var second = Monster(1123, 510, 2, 1400, 900);
        var earthTarget = Monster(208, 512, 5, 1200, 900);
        var both = DemoDuel.FromPartialObservation(1, [], Slots(second), Slots(first), 8000,
            side0Deck: [dark], side1Deck: [earthTarget]);
        check(both.BeginBattle(out _) && both.Attack(0, 0, out _) &&
              both.Monsters[0, 0] is null && both.Monsters[1, 0] is null &&
              both.PendingCardSelection is { Owner: 1, EffectResourceId: 1108 } &&
              both.Graveyard(0).Single() == second &&
              both.Graveyard(1).Single() == first,
              "P164: equal ATK destroys both and queues both recruitment effects");
        check(both.SkipOptionalCardSelection(out _) &&
              both.PendingCardSelection is { Owner: 0, EffectResourceId: 1123 } &&
              both.ResolveCardSelection(0, out _) &&
              both.Monsters[0, 0] == dark &&
              both.PendingCardSelection is null &&
              both.DeckSnapshot(1).Single() == earthTarget,
              "P164: optional skip advances to other player's valid pending recruiter");
    }
}
