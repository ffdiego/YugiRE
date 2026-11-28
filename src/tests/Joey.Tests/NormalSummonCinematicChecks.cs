using Joey.Core;

internal static class NormalSummonCinematicChecks
{
    private static Card Monster(ushort id, int index, int level) =>
        new(index, id, id == 81 ? "Red-Eyes B. Dragon" : $"Monster {id}", "",
            (1u << 20) | ((uint)level << 25));

    public static void Run(Action<bool, string> check)
    {
        Card weak = Monster(140, 44, 4);
        Card redEyes = Monster(81, 1082, 7);
        var summoned = new DemoDuel([redEyes]);
        summoned.Monsters[0, 0] = weak;
        summoned.Monsters[0, 1] = weak;
        Card physicalRedEyes = summoned.Hand[0];
        List<DemoNormalSummon> normalEvents = [];
        List<DemoMonsterTribute> tributes = [];
        summoned.MonsterNormalSummoned += normalEvents.Add;
        summoned.MonsterTributed += tributes.Add;

        check(!summoned.Summon(0, 0, 2, true, out _, [0]) &&
              normalEvents.Count == 0 && tributes.Count == 0 &&
              summoned.Monsters[0, 2] is null,
              "Signature summon: rejected Red-Eyes payment emits no visual cue");
        check(summoned.Summon(0, 0, 2, true, out _, [0, 1]) &&
              tributes.Select(t => t.Zone).SequenceEqual([0, 1]) &&
              normalEvents is [{ Player: 0, Zone: 2, FaceDown: false, Card: var card }] &&
              ReferenceEquals(card, physicalRedEyes) &&
              summoned.Monsters[0, 2] == redEyes,
              "Signature summon: normal-summon cue fires once after committed tributes");

        var faceDown = new DemoDuel([weak]);
        Card physicalWeak = faceDown.Hand[0];
        List<DemoNormalSummon> sets = [];
        faceDown.MonsterNormalSummoned += sets.Add;
        check(faceDown.Summon(0, 0, 3, true, out _, faceDown: true) &&
              sets is [{ Player: 0, Zone: 3, FaceDown: true, Card: var setCard }] &&
              ReferenceEquals(setCard, physicalWeak) &&
              faceDown.MonsterFaceDown[0, 3],
              "Normal summon cue: an ordinary monster Set is explicitly face-down");

        var cpuSide = new DemoDuel([weak]);
        List<DemoNormalSummon> opponentEvents = [];
        cpuSide.MonsterNormalSummoned += opponentEvents.Add;
        TurnFixture.Advance(cpuSide);
        check(cpuSide.Summon(0, 1, 1, true, out _) &&
              opponentEvents is [{ Player: 1, Zone: 1, FaceDown: false }],
              "Normal summon cue: player one (CPU perspective) publishes owner and zone");
    }
}
