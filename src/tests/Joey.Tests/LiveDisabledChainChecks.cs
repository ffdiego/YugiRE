using Joey.Core;
using Joey.LookAndFeel;

internal static class LiveDisabledChainChecks
{
    public static void Run(Action<bool,string> check)
    {
        var jinzo=new Card(751,751,"Jinzo","",(7u<<20)|(6u<<25)|(2u<<29)|(240u<<9)|150u);
        var trap=new Card(1530,1530,"Royal Command","",21u<<20);
        var spell=new Card(300,300,"Equip","",22u<<20);
        foreach(int side in new[]{0,1})
        {
            Card?[] monsters=[jinzo,null,null,null,null];
            var d=DemoDuel.FromPartialObservation(side,[],side==0?monsters:new Card?[5],
                side==1?monsters:new Card?[5],8000);
            var session=new DuelConsoleSession(d,cards:[jinzo,trap,spell]);
            void Spawn(ushort id,int p,int z)
            {
                if(!session.Execute($"spawn {id} {p} spell {z}").Enqueued)
                    throw new InvalidOperationException("V12 fixture admission");
                for(int i=0;i<100&&session.Queue.IsBusy;i++)session.Queue.Tick();
                if(session.Queue.IsBusy)throw new InvalidOperationException("V12 fixture drain");
            }
            Spawn(1530,0,1);Spawn(1530,1,3);Spawn(1530,0,2);Spawn(300,1,4);
            d.SpellFaceDown[0,1]=false;d.SpellFaceDown[1,3]=false;
            d.SpellFaceDown[1,4]=false;
            var source=d.Monsters[side,0]!;
            var targets=d.JinzoSuppressionPresentationTargets(side,0,source);
            check(targets.Select(t=>(t.Target.Side,t.Slot)).SequenceEqual([(0,1),(1,3)]) &&
                targets.All(d.IsCurrentSuppressionPresentationTarget),
                "V12: revealed traps only in native side/slot order");
            check(d.JinzoSuppressionPresentationTargets(side,0,source with {}).Count==0,
                "V12: identical catalogue copy cannot trigger sweep");
            d.MonsterFaceDown[side,0]=true;
            check(d.JinzoSuppressionPresentationTargets(side,0,source).Count==0,
                "V12: hidden Jinzo does not trigger sweep");
            d.MonsterFaceDown[side,0]=false;
            d.SpellFaceDown[0,1]=true;
            check(!d.IsCurrentSuppressionPresentationTarget(targets[0]),
                "V12: no overlay over hidden or stale target");
        }
        var timeline=new NativeDisabledChainTimeline();
        for(int i=0;i<24;i++)
        {
            check(timeline.BlocksRelease && timeline.Sample(0)?.FrameIndex==(i/2)%6,
                "V12: active blocking gate for every visible update");
            timeline.AdvanceNativeUpdate();
        }
        check(!timeline.BlocksRelease && timeline.Sample(0) is null,"V12: release exactly at 24");
    }
}
