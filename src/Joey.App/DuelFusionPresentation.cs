using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Numerics;
using System.Text;

static partial class Game
{
    static string[] liveFusionResources=[];

    static void ConfigureLiveGeneralFusion(Archive archive)
    {
        try
        {
            var bank=NativeFusionResources.Parse(Encoding.Latin1.GetString(archive.Read(NativeFusionResources.Manifest)),
                archive.Entries.Select(e=>e.Name));
            NativeFusionResources.ValidateObservedBank(bank);
            liveFusionResources=bank;
        }
        catch(Exception ex) when(ex is InvalidDataException or KeyNotFoundException or IOException)
        {
            liveFusionResources=[];
            Console.Error.WriteLine("[fusion] General fusion visuals unavailable: "+ex.Message);
        }
    }

    static bool TryQueueGeneralFusion(DemoSpecialSummon summon)
    {
        if(!presentation.Style.PlayAnimations||liveFusionResources.Length!=9||
            summon.FusionMaterials is not { Count: 2 or 3 } materials)return false;
        try
        {
            var minis=materials.Append(summon.Card).Select(c=>graphics.Artwork.MiniFor(c)??
                throw new InvalidDataException("Missing fusion miniature: "+c.Id)).ToArray();
            foreach(string mini in minis)
            {
                var texture=graphics.Texture(mini);
                if(texture.Width!=50||texture.Height!=72)
                    throw new InvalidDataException("Unexpected fusion miniature dimensions: "+mini);
            }
            var sizes=liveFusionResources.Select((path,index)=>
            {
                var t=graphics.Texture(path);
                return NativeFusionResources.ValidateObservedSize(index,t.Width,t.Height);
            }).ToArray();
            var playback=new NativeFusionPlayback(materials.Count);
            playback.AdvanceTo(0,effectCosine,key=>sizes[key]);
            QueueCardEffect(new GeneralFusionPlacement(summon,playback,minis,sizes),NativeFusionPlayback.Updates);
            return true;
        }
        catch(Exception ex) when(ex is InvalidDataException or KeyNotFoundException or IOException)
        {
            Console.Error.WriteLine("[fusion] Keeping existing special summon visual: "+ex.Message);
            return false;
        }
    }

    static bool FusionResultHidden(int player,int zone) =>
        cardEffects.Pending.Any(step=>step.Kind is GeneralFusionPlacement f &&
            f.Summon.Player==player&&f.Summon.Zone==zone&&
            ReferenceEquals(duel.Monsters[player,zone],f.Summon.Card));

    static void DrawLiveGeneralFusion(GeneralFusionPlacement fusion)
    {
        if(fusion.Playback.Effect is { } frame)
        {
            DrawFusionReviewPass(NativeGeneralFusionEffect.Center,frame.Primary,liveFusionResources);
            if(frame.Additional is { } additional)
                DrawFusionReviewPass(NativeGeneralFusionEffect.Center,additional,liveFusionResources);
            foreach(var particle in frame.Particles)
            {
                var t=graphics.Texture(liveFusionResources[particle.Key]);
                Raylib.DrawTexturePro(t,new Rectangle(0,0,t.Width,t.Height),
                    ToRectangle(particle.Destination),Vector2.Zero,0,Color.White);
            }
        }
        if(fusion.Playback.Cards is not { } cardsFrame)return;
        void DrawMini(int index,CardBounds bounds)
        {
            if(bounds.Width<=0||bounds.Height<=0)return;
            var t=graphics.Texture(fusion.Miniatures[index]);
            Raylib.DrawTexturePro(t,new Rectangle(0,0,t.Width,t.Height),
                ToRectangle(bounds),Vector2.Zero,0,Color.White);
        }
        foreach(var material in cardsFrame.Materials)DrawMini(material.MaterialIndex,material.Destination);
        if(cardsFrame.Result is { } result)DrawMini(fusion.Miniatures.Length-1,result);
    }
}
