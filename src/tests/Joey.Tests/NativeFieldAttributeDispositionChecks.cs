using Joey.LookAndFeel;

internal static class NativeFieldAttributeDispositionChecks
{
    internal static void Run(Action<bool,string> check)
    {
        const ushort id=25;
        const int ready=NativeFieldAttributeVisibility.SettledCounter;
        NativeFieldAttributeDisposition N(ushort desc,int view=0,ushort occupant=id,
            bool active=true,int movement=ready,int state=0) =>
            NativeFieldAttributeVisibility.ClassifyNative(desc,view,id,occupant,active,movement,state);
        var preserve=NativeFieldAttributeDisposition.Preserve;
        var reset=NativeFieldAttributeDisposition.ResetToBase;
        var advance=NativeFieldAttributeDisposition.Advance;
        check(N(0)==advance && N(1,view:1)==advance && N(0x4000)==advance,
            "V05: own face-down and defense retain attribute Draw");
        check(N(1)==reset && N(0,view:1)==reset &&
            N(0x8001)==advance && N(0xc000,view:1)==advance,
            "V05: hidden opponent resets while revealed opponent advances");
        check(N(2)==reset && N((ushort)(11<<6))==reset,
            "V05: non-monster and region+offset out of range reset");
        check(N(0,occupant:26)==preserve && N(0x8001,occupant:26)==preserve,
            "V05: native identity mismatch preserves accumulated numbers");
        check(N(0,active:false)==preserve && N(0,movement:224)==preserve &&
            N(1,state:5)==preserve,
            "V05: outer Draw skip takes precedence over reset");
        check(NativeFieldAttributeVisibility.ClassifyClient(false,true,false,false,false)==reset &&
            NativeFieldAttributeVisibility.ClassifyClient(true,true,false,false,false)==preserve,
            "V05: vacated cache is discarded; populated in-flight slot is preserved");
        check(NativeFieldAttributeVisibility.ClassifyClient(true,false,false,true,true)==reset &&
            NativeFieldAttributeVisibility.ClassifyClient(true,true,true,true,false)==advance &&
            NativeFieldAttributeVisibility.ShouldDrawClient(false,false,true,false),
            "V05: client contact privacy and local Set numbers");
        var timeline=new NativeFieldAttributeTimeline();
        object first=new(), second=new();
        var a=timeline.Sample(first,1000,500,1800,900,1,true);
        // No Sample during an in-flight frame: same object continues rather than reseeding.
        var hold=timeline.Displayed;
        var next=timeline.Sample(first,1000,500,1800,900,1,true);
        check(a==new NativeFieldDisplayedAttributes(1100,550) && hold==a &&
            next==new NativeFieldDisplayedAttributes(1188,594),
            "V05: preserved accumulator resumes at next eligible frame");
        var replaced=timeline.Sample(second,1000,500,1800,900,1,true);
        timeline.Reset();
        var revealed=timeline.Sample(second,1000,500,1800,900,1,true);
        check(replaced==a && revealed==a,
            "V05: identity replacement and hidden reset seed from base");
        // Recipe 33: exact little-endian pairs from 005DB560 and 005DB510.
        CardPoint[] monsters=[new(369,352),new(447,352),new(525,352),new(603,352),new(681,352),
            new(681,248),new(603,248),new(525,248),new(447,248),new(369,248)];
        CardPoint[] spells=[new(369,460),new(447,460),new(525,460),new(603,460),new(681,460),
            new(681,140),new(603,140),new(525,140),new(447,140),new(369,140)];
        foreach(bool near in new[]{true,false})
        for(int slot=0;slot<5;slot++)
        {
            int index=slot+(near?0:5);
            check(NativeDuelLayout.MonsterCenter(slot,near)==monsters[index] &&
                DuelCardLayout.Center(DuelCardZone.Monster,near,slot)==monsters[index] &&
                DuelCardLayout.Center(DuelCardZone.Spell,near,slot)==spells[index],
                $"V02/V05/V06: recipe 33 exact native zone centers near={near} slot={slot}");
            foreach(bool defense in new[]{false,true})
            foreach(bool hidden in new[]{false,true})
            {
                var geometry=DuelCardLayout.OnBoard(DuelCardZone.Monster,near,slot,defense,hidden);
                var center=monsters[index];
                CardPoint? expected=(!near&&hidden)?null:new CardPoint(center.X,center.Y+(near?46:-46));
                check(geometry.AttributesCenter==expected,
                    $"V05: recipe 33 attribute anchor near={near} slot={slot} defense={defense} hidden={hidden}");
            }
        }
        var local=NativeFieldAttributeFrame.Compose(new CardPoint(525,398),7,10,1500,1200,1500,1200,false);
        var opponent=NativeFieldAttributeFrame.Compose(new CardPoint(525,202),7,10,1500,1200,1500,1200,true);
        check(local.Attack.CenterX==505 && local.Defense.CenterX==545 &&
            local.SeparatorX==521 && local.Attack.TopY==393 && opponent.Attack.TopY==197 &&
            local.Attack.Opacity==256 && opponent.Defense.Opacity==256,
            "V05: actual DAT glyph dimensions and documented center anchors");
    }
}
