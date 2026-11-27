using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Numerics;

static partial class Game
{
    static void QueueEquipmentTravel(DemoEquipmentActivation equip)
    {
        if(!presentation.Style.PlayAnimations)return;
        // Source is the revealed equipment already committed to its S/T slot.
        string? artwork=graphics.Artwork.For(equip.Card);
        if(artwork is null)return;
        var source=DuelCardLayout.Center(DuelCardZone.Spell,equip.Owner==ViewPlayer,equip.SourceSlot);
        var target=DuelCardLayout.Center(DuelCardZone.Monster,equip.TargetSide==ViewPlayer,equip.TargetSlot);
        QueueCardEffect(new EquipmentTravelVisual(artwork,source,target,256*(equip.Owner^ViewPlayer)),
            NativeEquipmentTravel.Updates+1);
    }

    static void DrawEquipmentTravel(EquipmentTravelVisual equip,int elapsedUpdates)
    {
        var frame=NativeEquipmentTravel.Sample(elapsedUpdates*NativeEquipmentTravel.Increment,
            equip.Source,equip.Target,equip.RotationIndex/256,0,effectCosine);
        var texture=graphics.Texture(equip.Artwork);
        float w=texture.Width*frame.ScaleFixed16/65536f,h=texture.Height*frame.ScaleFixed16/65536f;
        // Same native center/scale and raw argument; RGBA tint is still a
        // diagnostic approximation of 00422C00, not pixel-equivalent blending.
        Raylib.DrawTexturePro(texture,new Rectangle(0,0,texture.Width,texture.Height),
            new Rectangle(frame.Center.X,frame.Center.Y,w,h),new Vector2(w/2,h/2),
            frame.RotationIndex*360f/512,new Color(255,255,255,frame.Intensity));
        // The terminal sound request is modeled, but Voice.dat mapping
        // 1000000A has not yet been validated. Do not substitute another cue.
        if(frame.Terminal)equip.TerminalDrawn=true;
    }
}
