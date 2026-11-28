using Joey.Core;
using Joey.LookAndFeel;

internal static class LiveAffectChecks
{
    public static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        var loop=new byte[100*69*4];
        for(int y=0;y<69;y++)
        for(int x=0;x<100;x++)
        {
            int i=(y*100+x)*4;
            loop[i]=(byte)y;loop[i+1]=(byte)x;loop[i+2]=77;
        }
        var mask=new byte[50*50*4];mask[2]=255;mask[6]=128;
        var atlas=NativeAffectPixels.BuildAtlas(loop,mask);
        check(atlas.Length==50*50*32*4,"Affect: all 32 phases in one cached texture");
        for(int phase=0;phase<32;phase++)
        {
            int i=phase*50*50*4;
            int bottom=i+49*50*4;
            check(atlas[i]==phase && atlas[i+3]==255 && atlas[i+7]==16*255/31 &&
                atlas[bottom]==(phase+49)%69 && atlas[bottom+3]==0 &&
                NativeAffectPixels.Source(phase)==new CardBounds(0,phase*50,50,50),
                "Affect: original loop phase and wrapped row, blue mask alpha");
        }
        reject(()=>NativeAffectPixels.BuildAtlas(loop,mask[..^1]),"Affect: wrong mask dimensions");
        foreach(int owner in new[]{0,1})
        foreach(int targetSide in new[]{0,1})
        {
            var spell=new Card(300,300,"Sword","",22u<<20);
            var unit=new Card(77,77,"Warrior","",(15u<<20)|(2u<<29)|(4u<<25)|(100u<<9)|100u);
            Card?[] field=[unit,null,null,null,null];
            var d=DemoDuel.FromPartialObservation(owner,[spell],targetSide==0?field:new Card?[5],
                targetSide==1?field:new Card?[5],8000,equipmentContextResolved:true);
            var q=d.CreatePrimitiveActionQueue();
            check(d.TryCaptureCardTarget(targetSide,DuelZone.Monster,0,out var monster) &&
                  d.AffectRelations(monster).Count==0,"Affect: no link before equipment");
            check(d.TryQueueEquipment(DuelZone.Hand,0,targetSide,0,out _),"Affect: admit equip");
            for(int i=0;i<100&&q.IsBusy;i++)q.Tick();
            check(!q.IsBusy && d.TryCaptureCardTarget(owner,DuelZone.SpellTrap,0,out var equip),
                "Affect: committed equip occurrence");
            d.TryCaptureCardTarget(owner,DuelZone.SpellTrap,0,out var source);
            check(d.AffectRelations(monster).SequenceEqual([new DemoAffectRelation(source,0,DemoAffectKind.Equipment)]) &&
                  d.AffectRelations(source).SequenceEqual([new DemoAffectRelation(monster,0,DemoAffectKind.Equipment)]),
                "Affect: actual link both directions, both sides");
            d.MonsterFaceDown[targetSide,0]=true;
            check(d.AffectRelations(monster).Count==0 && d.AffectRelations(source).Count==0,
                "Affect: never reveal a hidden endpoint");
            d.MonsterFaceDown[targetSide,0]=false;
            d.Monsters[targetSide,0]=unit with {};
            check(d.AffectRelations(monster).Count==0 && d.AffectRelations(source).Count==0,
                "Affect: no stale highlight after replacement by identical resource");
        }
    }
}
