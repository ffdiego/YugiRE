using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Numerics;
using System.Text;
using System.Text.Json;

static partial class Game
{
    static NativeGeneralFusionTimeline? generalFusionReview;
    static string[] generalFusionResources=[];
    static CardBounds[] generalFusionSizes=[];
    static NativeGeneralFusionEffectFrame? generalFusionFrame;

    static void ConfigureGeneralFusionReview(string[] args,Archive archive)
    {
        if(!args.Contains("--review-general-fusion"))return;
        generalFusionResources=NativeFusionResources.Parse(
            Encoding.Latin1.GetString(archive.Read(NativeFusionResources.Manifest)),
            archive.Entries.Select(e=>e.Name));
        NativeFusionResources.ValidateObservedBank(generalFusionResources);
        generalFusionReview=new(0);
    }

    static void InitializeGeneralFusionReview()
    {
        generalFusionSizes=generalFusionResources.Select((path,index)=>
        {
            var texture=graphics.Texture(path);
            return NativeFusionResources.ValidateObservedSize(index,texture.Width,texture.Height);
        }).ToArray();
        SampleGeneralFusionReview();
    }

    static void SampleGeneralFusionReview() =>
        generalFusionFrame=NativeGeneralFusionEffect.Draw(generalFusionReview!,effectCosine,
            key=>generalFusionSizes[key]);

    static void DrawGeneralFusionReview()
    {
        if(Raylib.IsKeyPressed(KeyboardKey.F8))
        {
            generalFusionReview=new(0);
            SampleGeneralFusionReview();
        }
        else if(Raylib.IsKeyPressed(KeyboardKey.F7))
        {
            if(generalFusionReview!.Counter<NativeGeneralFusionEffect.EffectEndCounter)
                generalFusionReview.AdvanceNativeUpdate();
            SampleGeneralFusionReview();
        }
        else if(Raylib.IsKeyPressed(KeyboardKey.F9))SampleGeneralFusionReview();

        // Isolated effects layer over the existing review board. Native
        // scratch capture/material/result passes are NOT simulated.
        Raylib.DrawTexture(graphics.Texture(DatGraphics.DuelDetailBackground),0,0,Color.White);
        if(duelTheme.Board is Texture2D board)
            Raylib.DrawTexturePro(board,new Rectangle(0,0,board.Width,board.Height),
                ToRectangle(DuelSurfaceLayout.Board),Vector2.Zero,0,Color.White);
        if(generalFusionFrame is not { } frame)return;
        DrawFusionReviewPass(NativeGeneralFusionEffect.Center,frame.Primary,generalFusionResources);
        if(frame.Additional is { } extra)
            DrawFusionReviewPass(NativeGeneralFusionEffect.Center,extra,generalFusionResources);
        foreach(var particle in frame.Particles)
        {
            var texture=graphics.Texture(generalFusionResources[particle.Key]);
            Raylib.DrawTexturePro(texture,new Rectangle(0,0,texture.Width,texture.Height),
                ToRectangle(particle.Destination),Vector2.Zero,0,Color.White);
        }
    }

    static void CaptureGeneralFusionReview(RenderTexture2D canvas)
    {
        string basename=SaveVisualReviewImage(canvas,"general-fusion-effects");
        File.WriteAllText(basename+".json",JsonSerializer.Serialize(new
        {
            schemaVersion=1,routine="004524F0",center=NativeGeneralFusionEffect.Center,
            counter=generalFusionReview!.Counter,angle=generalFusionReview.Angle,
            effectLayerActive=generalFusionFrame.HasValue,frame=generalFusionFrame,
            resources=generalFusionResources,sizes=generalFusionSizes,cosineSource=effectCosineSource,
            compositor="diagnostic raylib; native scratch, materials and result card omitted",
            audio="requests only, not played"
        },new JsonSerializerOptions{WriteIndented=true})+Environment.NewLine);
    }
}
