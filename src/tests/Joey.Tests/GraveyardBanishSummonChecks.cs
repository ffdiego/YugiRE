using Joey.Core;

internal static class GraveyardBanishSummonChecks
{
    static Card Monster(ushort id, int index, int type = 3, int level = 4) =>
        new(index, id, $"Monster {id}", "", ((uint)type << 20) |
            (1u << 18) | ((uint)level << 25) | (120u << 9) | 100u);
    static Card?[] Slots(Card? a = null) => [a, null, null, null, null];

    public static void Run(Action<bool,string> check)
    {
        var necro = Monster(1514, 513, level: 8);
        var a = Monster(47, 248);
        var b = Monster(1114, 383);
        var c = Monster(1114, 1383);
        var d = Monster(400, 1400, type: 15);
        var spell = new Card(1000, 335, "Dark Hole", "", 22u << 20);
        var normal = Monster(500, 1500, type: 15);
        var duel = DemoDuel.FromPartialObservation(0, [necro,normal], Slots(), Slots(), 8000,
            side0Graveyard:[a,d,b,spell,c]);
        Card physicalNecro = duel.Hand[0];
        var candidates = duel.GraveyardBanishSummonCandidates(0);
        check(NormalSummonRules.RequiresSpecialSummon(necro) &&
              !duel.CanSummonFromHand(0) && !duel.CanSetFromHand(0) &&
              duel.CanSpecialSummonByGraveyardBanish(0) &&
              candidates.Select(x => x.SourceIndex).SequenceEqual([0,2,4]),
              "P252: only Fiend monsters in own GY; cannot Normal Summon or Set");

        check(!duel.SpecialSummonByGraveyardBanish(0,0,physicalNecro,[candidates[0],candidates[0],candidates[2]],false,out _) &&
              !duel.SpecialSummonByGraveyardBanish(0,0,physicalNecro,[candidates[0],candidates[1]],false,out _) &&
              !duel.SpecialSummonByGraveyardBanish(0,0,physicalNecro,[candidates[0],candidates[1],new(1,d)],false,out _) &&
              !duel.SpecialSummonByGraveyardBanish(0,0,normal,candidates,false,out _) &&
              duel.Banished(0).Count == 0 && duel.Graveyard(0).Count == 5 &&
              duel.HandFor(0).SequenceEqual([necro,normal]),
              "P252: duplicate indices, insufficient cost, non-Fiend and stale hand source reject atomically");

        check(duel.SpecialSummonByGraveyardBanish(0,2,physicalNecro,candidates,false,out _) &&
              duel.Banished(0).SequenceEqual([a,b,c]) &&
              duel.Graveyard(0).SequenceEqual([d,spell]) &&
              ReferenceEquals(duel.Monsters[0,2],physicalNecro) &&
              !duel.MonsterFaceDown[0,2] && !duel.DefensePosition[0,2] &&
              duel.HandFor(0).SequenceEqual([normal]) &&
              duel.CanSummonFromHand(0),
              "P252: banishes exactly three occurrences and preserves Normal Summon allowance");

        var afterNormal = DemoDuel.FromPartialObservation(0,[normal,necro],Slots(),Slots(),8000,
            side0Graveyard:[a,b,c]);
        check(afterNormal.Summon(0,0,0,true,out _) &&
              afterNormal.CanSpecialSummonByGraveyardBanish(0) &&
              afterNormal.SpecialSummonByGraveyardBanish(0,1,afterNormal.Hand[0],
                afterNormal.GraveyardBanishSummonCandidates(0),true,out _) &&
              afterNormal.DefensePosition[0,1] && !afterNormal.MonsterFaceDown[0,1],
              "P252: Special Summon after Normal Summon, face-up Defense allowed");

        var wrongSide = DemoDuel.FromPartialObservation(1,[necro],Slots(),Slots(),8000,
            side0Graveyard:[a,b,c],side1Graveyard:[a,b]);
        check(!wrongSide.CanSpecialSummonByGraveyardBanish(0) &&
              wrongSide.GraveyardBanishSummonCandidates(0).Count==0 &&
              wrongSide.Banished(1).Count==0,
              "P252: opponent Graveyard cannot pay cost");

        var full = DemoDuel.FromPartialObservation(0,[necro],[normal,normal,normal,normal,normal],
            Slots(),8000,side0Graveyard:[a,b,c]);
        check(!full.CanSpecialSummonByGraveyardBanish(0) &&
              full.Graveyard(0).Count==3 && full.Banished(0).Count==0,
              "P252: full Monster Zone blocks payment");

        var stale = DemoDuel.FromPartialObservation(0,[necro],Slots(),Slots(),8000,
            side0Graveyard:[a,b,c]);
        check(!stale.SpecialSummonByGraveyardBanish(0,0,stale.Hand[0],[new(0,b),new(1,a),new(2,c)],false,out _) &&
              stale.Graveyard(0).SequenceEqual([a,b,c]),
              "P252: stale candidate identity at original index cannot mutate zones");

        var copy = DemoDuel.FromPartialObservation(1,[necro],Slots(),Slots(),8000,
            side1Graveyard:[a,a,a]);
        check(copy.SpecialSummonByGraveyardBanish(0,0,copy.Hand[0],
                copy.GraveyardBanishSummonCandidates(0),false,out _) &&
              copy.Banished(1).Count==3 && copy.Banished(0).Count==0 &&
              copy.Monsters[1,0]==necro,
              "P252: three separate occurrences of same object are distinct costs, correct side");
    }
}
