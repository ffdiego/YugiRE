using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Numerics;
using System.Text;

static partial class Game
{
    static string[] liveAffectResources=[];
    static NativeAffectTimeline liveAffect=new();

    static void ConfigureLiveAffect(Archive archive)
    {
        liveAffectResources=[];
        try
        {
            var resources=NativeAffectDraw.Resources(
                Encoding.Latin1.GetString(archive.Read(NativeAffectDraw.Manifest)),
                archive.Entries.Select(e=>e.Name));
            string[] names=["loop_blue.bmp","ico_equip.bmp","ico_conti.bmp","ico_field.bmp","ico_kousoku.bmp","ico_power.bmp"];
            for(int i=0;i<names.Length;i++)
            {
                var expected=@"j\duel\effect\"+names[i];
                var info=BitmapInfo.Read(archive.Read(resources[i]));
                if(!resources[i].Equals(expected,StringComparison.OrdinalIgnoreCase) ||
                    info.Width!=(i==0?100:50) || info.Height!=(i==0?69:50))
                    throw new InvalidDataException("Unexpected Affect bank record: "+resources[i]);
            }
            liveAffectResources=resources;
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or KeyNotFoundException or OverflowException)
        { Console.Error.WriteLine("[affect] "+ex.Message); }
    }

    static CardTarget? AffectFocus()
    {
        for(int side=0;side<2;side++)
        {
            if(Hit(FieldZoneBounds(side)) && duel.TryCaptureCardTarget(side,DuelZone.FieldSpell,0,out var field))
                return field;
            for(int z=0;z<5;z++)
            {
                if(Hit(ZoneBounds(side,z,false)) && duel.TryCaptureCardTarget(side,DuelZone.Monster,z,out var monster))
                    return monster;
                if(Hit(ZoneBounds(side,z,true)) && duel.TryCaptureCardTarget(side,DuelZone.SpellTrap,z,out var spell))
                    return spell;
            }
        }
        return null;
    }

    static void DrawLiveAffect(int updates,bool allowFocus)
    {
        if(liveAffectResources.Length!=6)return;
        var focus=allowFocus?AffectFocus():null;
        long identity=focus?.Occurrence.Value??0;
        // Focus changes must not render the previous pulse on a new card.
        // In Instant mode show a static maximum-intensity context icon.
        for(int i=0;i<updates;i++)liveAffect.AdvanceNativeUpdate(identity);
        if(focus is not { } current)return;
        bool animated=presentation.Style.PlayAnimations;
        if(animated && liveAffect.FocusIdentity!=identity)return;
        int phase=animated?liveAffect.Counter/8:16;
        int intensity=animated?liveAffect.Intensity(effectCosine):224;
        if(intensity<=0)return;
        try
        {
            foreach(var relation in duel.AffectRelations(current))
            {
                int selector=relation.Kind switch {
                    DemoAffectKind.Equipment=>1,DemoAffectKind.Field=>3,
                    DemoAffectKind.Restriction=>4,DemoAffectKind.Power=>5,_=>0 };
                if(selector==0)continue;
                var center=DuelCardLayout.Center(relation.Target.Zone switch {
                    DuelZone.Monster=>DuelCardZone.Monster,DuelZone.SpellTrap=>DuelCardZone.Spell,
                    _=>DuelCardZone.Field },relation.Target.Side==ViewPlayer,relation.Slot);
                var texture=graphics.AffectTexture(liveAffectResources[0],liveAffectResources[selector]);
                Raylib.DrawTexturePro(texture,ToRectangle(NativeAffectPixels.Source(phase)),
                    new Rectangle(center.X-25,center.Y-25,50,50),Vector2.Zero,0,
                    new Color(255,255,255,Math.Clamp(intensity,0,255)));
            }
        }
        catch(Exception ex) when(ex is IOException or InvalidDataException or KeyNotFoundException or OverflowException)
        {
            liveAffectResources=[];
            Console.Error.WriteLine("[affect] disabled unsupported bank: "+ex.Message);
        }
    }
}
