using Joey.Core;

internal static class ElementalBattleAttributeChecks
{
    private static Card Monster(ushort id,int attr=1,int atk=1000,int category=0) =>
        new(id,id,$"Monster {id}","",(3u<<20)|(4u<<25)|((uint)attr<<29)|((uint)category<<18)|((uint)(atk/10)<<9)|80u);
    private static Card?[] Slots(params Card[] cards) =>
        Enumerable.Range(0,5).Select(i=>i<cards.Length?cards[i]:null).ToArray();
    private static void Drain(ActionQueue q)
    {
        for(int i=0;i<150&&q.IsBusy;i++)q.Tick();
        if(q.IsBusy)throw new InvalidOperationException("Summon did not drain.");
    }
    public static void Run(Action<bool,string> check)
    {
        foreach(int controller in new[]{0,1})
        foreach(int active in new[]{0,1})
        foreach(bool battle in new[]{false,true})
        {
            foreach(int souls in new[]{0,1,2,5})
            foreach(ushort id in new ushort[]{24,1515,1516,1518})
            {
                int expected = battle
                    ? (id==1516&&controller==active || id==1518&&controller!=active ? 300:0)
                        - (controller==active?300*souls:0)
                    : 0;
                check(ElementalBattleAttributeRules.AttackDelta(id,controller,active,battle,souls)==expected,
                    "Native battle side and stacking table");
            }
        }
        foreach(int owner in new[]{0,1})
        foreach(ushort id in new ushort[]{1515,1516,1518})
        {
            int attr=id==1515?1:id==1516?4:5;
            var source=Monster(id,attr,1000,1);
            var enemy=Monster(24,2,1200);
            Card[] costs=[Monster(25,attr),Monster(26,attr)];
            var d=DemoDuel.FromPartialObservation(owner,[source],
                owner==0?Slots():Slots(enemy),owner==1?Slots():Slots(enemy),8000,
                side0Graveyard:owner==0?costs:null,side1Graveyard:owner==1?costs:null);
            var q=d.CreatePrimitiveActionQueue();
            d.TryCaptureCardTarget(owner,DuelZone.Hand,0,out var hand);
            var selected=d.QueuedGraveyardBanishSummonCandidates(hand)
                .Take(id==1515?2:1).Select(c=>c.Target).ToArray();
            check(d.TryQueueGraveyardBanishSummon(hand,0,selected,false,out _),"Summon procedure admitted");
            Drain(q);
            check(d.AttackValue(owner,0)==1000&&d.AttackValue(1-owner,0)==1200&&
                d.DefenseValue(owner,0)==800,"No battle modifier in Main Phase");
            if(owner==0){d.NextTurn();d.NextTurn();}
            check(d.BeginBattle(out _),"Own battle phase");
            check(d.AttackValue(owner,0)==(id==1516?1300:1000)&&d.AttackValue(1-owner,0)==1200,
                "Own battle bonus only for Spirit of Flames");
            check(d.EndBattle(out _)&&d.AttackValue(owner,0)==1000,"Main Phase 2 resets modifier");
            d.NextTurn();
            check(d.BeginBattle(out _),"Opponent battle phase");
            check(d.AttackValue(owner,0)==(id==1518?1300:1000)&&
                d.AttackValue(1-owner,0)==(id==1515?900:1200),
                "Opponent battle applies Rock Spirit or Soul aura");
            if(id==1515||id==1518)
            {
                check(d.Attack(0,0,out _)&&d.Monsters[owner,0]?.Id==id&&
                    d.Monsters[1-owner,0] is null&&d.Life[1-owner]==7900,
                    "Battle uses the phase-dependent ATK, not just display");
            }
            else
            {
                check(d.EndBattle(out _)&&d.AttackValue(owner,0)==1000,"Opponent Main2 reset");
                d.NextTurn();
                check(d.BeginBattle(out _)&&d.Attack(0,0,out _)&&d.Monsters[owner,0]?.Id==id&&
                    d.Monsters[1-owner,0] is null&&d.Life[1-owner]==7900,
                    "Spirit of Flames wins own battle using 300 ATK bonus");
            }
        }

        var stack=DemoDuel.FromPartialObservation(1,[],Slots(Monster(1515),Monster(1515),Monster(1515)),
            Slots(Monster(1516,4,100),Monster(24,1,200)),8000);
        stack.MonsterFaceDown[0,2]=stack.DefensePosition[0,2]=true;
        check(stack.BeginBattle(out _)&&stack.AttackValue(1,0)==0&&stack.AttackValue(1,1)==0&&
            stack.DefenseValue(1,0)==800&&stack.AttackValue(0,2)==1000,
            "Two face-up Souls stack, face-down third ignored; clamp once and DEF unchanged");
        stack.Monsters[0,0]=null;
        check(stack.AttackValue(1,0)==100,"Removing a Soul immediately recalculates ATK");
        stack.MonsterFaceDown[1,0]=stack.DefensePosition[1,0]=true;
        check(stack.AttackValue(1,0)==100,"Face-down target returns base ATK");

        var combine=DemoDuel.FromPartialObservation(1,[],Slots(Monster(1515)),Slots(Monster(1516,4,1000)),8000);
        check(combine.BeginBattle(out _)&&combine.AttackValue(1,0)==1000,
            "Spirit boost and Soul penalty combine before clamp");
    }
}
