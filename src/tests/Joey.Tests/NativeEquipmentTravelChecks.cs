using Joey.Core;
using Joey.LookAndFeel;

internal static class NativeEquipmentTravelChecks
{
    public static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        int[] cosine=NativeCosineCapture.GenerateTable();
        foreach(int owner in new[]{0,1})
        foreach(int perspective in new[]{0,1})
        foreach(int targetSide in new[]{0,1})
        {
            var source=DuelCardLayout.Center(DuelCardZone.Spell,owner==perspective,2);
            var target=DuelCardLayout.Center(DuelCardZone.Monster,targetSide==perspective,4);
            for(int counter=0;counter<=192;counter+=8)
            {
                var frame=NativeEquipmentTravel.Sample(counter,source,target,owner,perspective,cosine);
                int t=counter-128,weight=counter<128?0:4*t;
                check(frame.Center==new CardPoint(source.X+(target.X-source.X)*weight/256,
                    source.Y+(target.Y-source.Y)*weight/256) &&
                    frame.ScaleFixed16==(256+cosine[(counter<128?t:2*t)&511]/256)*64 &&
                    frame.Intensity==128 && frame.RotationIndex==256*(owner^perspective) &&
                    frame.Terminal==(counter==192) &&
                    frame.SoundRequest==(counter==192?(uint?)0x1000000a:null),
                    "B2: exact growth, flight, terminal Draw, raw intensity and orientation");
            }
            check(NativeEquipmentTravel.Sample(0,source,target,owner,perspective,cosine).Center==source &&
                  NativeEquipmentTravel.Sample(128,source,target,owner,perspective,cosine).ScaleFixed16==32768 &&
                  NativeEquipmentTravel.Sample(192,source,target,owner,perspective,cosine).Center==target,
                "B2: starts on source, max half-scale, ends at target");
        }
        reject(()=>NativeEquipmentTravel.Sample(200,new(0,0),new(1,1),0,0,cosine),"B2: reject beyond terminal");
        reject(()=>NativeEquipmentTravel.Sample(7,new(0,0),new(1,1),0,0,cosine),"B2: reject fractional update");

        foreach(int owner in new[]{0,1})
        foreach(int targetSide in new[]{0,1})
        {
            var spell=new Card(300,300,"Sword","",22u<<20);
            var unit=new Card(77,77,"Warrior","",(15u<<20)|(4u<<25)|(100u<<9)|100u);
            Card?[] own=[unit,null,null,null,null];
            var d=DemoDuel.FromPartialObservation(owner,[spell],targetSide==0?own:new Card?[5],
                targetSide==1?own:new Card?[5],8000,equipmentContextResolved:true);
            var observed=new List<DemoEquipmentActivation>();
            d.EquipmentActivated+=observed.Add;
            var q=d.CreatePrimitiveActionQueue();
            check(d.TryQueueEquipment(DuelZone.Hand,0,targetSide,0,out _) && observed.Count==0,
                "B2: no visual observation at admission");
            for(int i=0;i<100&&q.IsBusy;i++)q.Tick();
            check(!q.IsBusy && observed.SequenceEqual([new DemoEquipmentActivation(spell,owner,0,targetSide,0)]) &&
                d.EquipmentCards.Count==1 && d.AttackValue(targetSide,0)==1300,
                "B2: exactly one successful queued equip observation");
        }
        var stale=DemoDuel.FromPartialObservation(0,[new Card(300,300,"Sword","",22u<<20)],
            [new Card(77,77,"Warrior","",(15u<<20)|(4u<<25)|(100u<<9)|100u),null,null,null,null],
            new Card?[5],8000,equipmentContextResolved:true);
        int count=0;stale.EquipmentActivated+=_=>count++;
        var sq=stale.CreatePrimitiveActionQueue();
        check(stale.TryQueueEquipment(DuelZone.Hand,0,0,0,out _),"B2: stale fixture admitted");
        stale.Monsters[0,0]=null;
        for(int i=0;i<100&&sq.IsBusy;i++)sq.Tick();
        check(count==0 && stale.EquipmentCards.Count==0,"B2: stale occurrence does not animate");
    }
}
