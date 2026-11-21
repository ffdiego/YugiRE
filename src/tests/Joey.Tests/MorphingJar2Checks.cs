using Joey.Core;

/// <summary>
/// Bounded PSV-era Morphing Jar #2 behavior without Extra Deck, ownership
/// changes, tokens, chain timing or target-selection UI.
/// </summary>
internal static class MorphingJar2Checks
{
    private static Card Monster(ushort id, int index, int level = 3,
                                int attack = 800, int defense = 700) =>
        new(index, id, id == FlipEffectRules.MorphingJar2 ? "Morphing Jar #2" : $"Monster {id}",
            "", (6u << 20) | ((uint)level << 25) |
            ((uint)(attack / 10) << 9) | (uint)(defense / 10));

    private static Card Spell(int index) =>
        new(index, (ushort)(2000 + index), $"Spell {index}", "", 22u << 20);

    private static Card?[] Slots(Card? first = null, Card? second = null) =>
        [first, second, null, null, null];

    public static void Run(Action<bool, string> check)
    {
        var jar = Monster(1163, 146);
        var large = Monster(1600, 300, level: 7);
        var opponent = Monster(1400, 301, level: 4, attack: 1300);

        check(FlipEffectRules.Effect(1163) == DemoFlipEffect.ShuffleAndExcavate &&
              FlipEffectRules.Supports(1163) &&
              FlipEffectRules.Effect(590) == DemoFlipEffect.ReplaceHands &&
              FlipEffectRules.Effect(1164) == DemoFlipEffect.None,
              "Morphing Jar #2: exact Joey resource ID; not Morphing Jar or unsupported cards");

        var flip = DemoDuel.FromPartialObservation(0, [jar], Slots(), Slots(), 8000);
        check(flip.SetFromHand(0, out _) && flip.MonsterFaceDown[0, 0] &&
              !flip.ChangePosition(0, out _), "Morphing Jar #2: cannot Flip Summon in set turn");
        flip.NextTurn(); flip.NextTurn();
        check(flip.ChangePosition(0, out _) &&
              flip.PendingFlipTarget is null &&
              flip.LastMorphingJar2.Count == 2 &&
              flip.LastMorphingJar2[0] is { ReturnedToDeck: 1, Excavated: 1,
                  MonstersFound: 1, SpecialSummoned: 1, SentToGraveyard: 0, DeckExhausted: false } &&
              flip.LastMorphingJar2[1].ReturnedToDeck == 0 &&
              flip.LastMorphingJar2[1].Excavated == 0 &&
              flip.Monsters[0, 0] == jar &&
              flip.MonsterFaceDown[0, 0] && flip.DefensePosition[0, 0] &&
              !flip.ChangePosition(0, out _) &&
              flip.HandFor(0).Count == 0 && flip.Graveyard(0).Count == 0 &&
              flip.Life.SequenceEqual([8000, 8000]) && !flip.GameOver,
              "Morphing Jar #2: Flip Summon re-shuffles itself and special summons face-down without another Flip");
        check(flip.DeckSnapshot(0).Count == 0 && flip.DeckSnapshot(1).Count == 0,
              "Morphing Jar #2: excavation is not a draw and zero quota consumes no deck cards");

        var byEffect = DemoDuel.FromPartialObservation(0, [],
            Slots(jar, large), Slots(opponent), 8000);
        var specialSummons = new List<DemoSpecialSummon>();
        var shuffledDecks = new List<int>();
        byEffect.MonsterSpecialSummoned += specialSummons.Add;
        byEffect.DeckShuffled += shuffledDecks.Add;
        byEffect.MonsterFaceDown[0, 0] = byEffect.DefensePosition[0, 0] = true;
        check(byEffect.RevealMonsterByEffect(0, 0, out string summary) &&
              summary.Contains("excavated", StringComparison.OrdinalIgnoreCase) &&
              byEffect.PendingFlipTarget is null &&
              byEffect.LastMorphingJar2[0] is { ReturnedToDeck: 2, Excavated: 2,
                  MonstersFound: 2, SpecialSummoned: 1, SentToGraveyard: 1 } &&
              byEffect.LastMorphingJar2[1] is { ReturnedToDeck: 1, Excavated: 1,
                  MonstersFound: 1, SpecialSummoned: 1, SentToGraveyard: 0 } &&
              byEffect.Monsters[0, 0] == jar && byEffect.MonsterFaceDown[0, 0] &&
              byEffect.Monsters[1, 0] == opponent && byEffect.MonsterFaceDown[1, 0] &&
              byEffect.Graveyard(0).Single() == large &&
              byEffect.Graveyard(1).Count == 0 &&
              byEffect.DeckCount(0) == 0 && byEffect.DeckCount(1) == 0 &&
              !byEffect.GameOver,
              "Morphing Jar #2: both players return their own monsters; Level 7 counts but goes to GY");

        check(shuffledDecks.SequenceEqual([0, 1]),
              "Morphing Jar #2: deck-shuffle visual events are emitted after both actual deck commits");
        check(specialSummons.Count == 2 &&
              specialSummons.Any(x => x.Player == 0 && x.Zone == 0 && x.Card == jar && x.FaceDownDefense) &&
              specialSummons.Any(x => x.Player == 1 && x.Zone == 0 && x.Card == opponent && x.FaceDownDefense),
              "Morphing Jar #2: visual placement events report both field owners without changing rules");

        // A card needing a special summon cannot bypass that gate just because
        // it was excavated; it still counts towards the monster quota.
        var specialOnlyBase = Monster(0x37, 302, level: 3);
        var specialOnly = specialOnlyBase with { Properties = specialOnlyBase.Properties | (1u << 18) };
        var gated = DemoDuel.FromPartialObservation(0, [],
            Slots(jar, specialOnly), Slots(), 8000);
        gated.MonsterFaceDown[0, 0] = true;
        check(gated.RevealMonsterByEffect(0, 0, out _) &&
              gated.LastMorphingJar2[0].MonstersFound == 2 &&
              gated.LastMorphingJar2[0].SpecialSummoned == 1 &&
              gated.Graveyard(0).Single() == specialOnly,
              "Morphing Jar #2: special-summon-only monsters are counted but cannot be set");

        // In battle, a Jar already marked for destruction stays physically on
        // the field until the effect finishes; it does not return to the deck.
        var battle = DemoDuel.FromPartialObservation(1, [],
            Slots(jar), Slots(opponent), 8000);
        battle.MonsterFaceDown[0, 0] = battle.DefensePosition[0, 0] = true;
        check(battle.BeginBattle(out _) && battle.Attack(0, 0, out _) &&
              battle.LastMorphingJar2[0].ReturnedToDeck == 0 &&
              battle.LastMorphingJar2[1].ReturnedToDeck == 1 &&
              battle.LastMorphingJar2[0].Excavated == 0 &&
              battle.LastMorphingJar2[1].Excavated == 1 &&
              battle.Monsters[0, 0] is null &&
              battle.Graveyard(0).Single() == jar &&
              battle.Monsters[1, 0] == opponent &&
              battle.MonsterFaceDown[1, 0] && battle.DefensePosition[1, 0] &&
              battle.HandFor(0).Count == 0 && battle.Life.SequenceEqual([8000, 8000]),
              "Morphing Jar #2: battle-destroyed Jar goes to graveyard; attacker is reshuffled and set");

        // Excavation is not drawing: a deck with no monsters is not required
        // to draw 5 or lose due to missing the draw quota.
        Card[] spells0 = Enumerable.Range(0, 40).Select(i => Spell(400 + i)).ToArray();
        Card[] spells1 = Enumerable.Range(0, 40).Select(i => Spell(500 + i)).ToArray();
        DemoDuel Fixture(int seed)
        {
            var duel = DemoDuel.FromDecks(spells0, spells1, seed);
            duel.Monsters[0, 0] = jar;
            duel.MonsterFaceDown[0, 0] = true;
            duel.DefensePosition[0, 0] = true;
            return duel;
        }
        var first = Fixture(29);
        var second = Fixture(29);
        check(first.RevealMonsterByEffect(0, 0, out _) &&
              second.RevealMonsterByEffect(0, 0, out _) &&
              first.LastMorphingJar2.SequenceEqual(second.LastMorphingJar2) &&
              first.Graveyard(0).Select(c => c.Index)
                  .SequenceEqual(second.Graveyard(0).Select(c => c.Index)) &&
              first.DeckSnapshot(0).Select(c => c.Index)
                  .SequenceEqual(second.DeckSnapshot(0).Select(c => c.Index)),
              "Morphing Jar #2: effect shuffle is deterministic under an explicit seed");
        var excavation = first.LastMorphingJar2[0];
        check(excavation.ReturnedToDeck == 1 &&
              excavation.MonstersFound == 1 &&
              excavation.Excavated == excavation.SentToGraveyard + excavation.SpecialSummoned &&
              excavation.SpecialSummoned == 1 &&
              first.Graveyard(0).Count == excavation.SentToGraveyard &&
              first.DeckCount(0) + excavation.Excavated == 36 &&
              first.DeckCount(1) == 35 &&
              first.HandFor(0).Count == 5 && first.HandFor(1).Count == 5 &&
              first.Monsters[0, 0] == jar &&
              !first.GameOver,
              "Morphing Jar #2: excavated spells go to GY, all cards conserved, other deck unaffected");
        check(first.LastMorphingJar2[1] is { ReturnedToDeck: 0, Excavated: 0 } &&
              first.Monsters[1, 0] is null,
              "Morphing Jar #2: a player who returned no monsters does not excavate");

        var empty = DemoDuel.FromPartialObservation(1, [],
            Slots(jar), Slots(opponent), 8000);
        empty.MonsterFaceDown[0, 0] = empty.DefensePosition[0, 0] = true;
        check(empty.BeginBattle(out _) && empty.Attack(0, 0, out _) &&
              !empty.LastMorphingJar2.Any(r => r.DeckExhausted) &&
              !empty.IsDraw && empty.Winner is null,
              "Morphing Jar #2: excavation never applies Morphing Jar's draw-five deck-out rule");
    }
}
