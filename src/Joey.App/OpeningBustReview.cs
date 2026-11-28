using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Text;
using System.Text.Json;

static partial class Game
{
    static NativeOpeningBank? openingBustReview;
    static NativeOpeningPlayback openingBustPlayback = new(0);
    static int openingBustVariant;
    static bool openingBustWaitAudio, openingBustAudioComplete;
    static uint? openingBustRequestedSound;
    static int[]? openingBustMouthKeys;
    static bool ReactionReviewActive => reactionReview is not null || openingBustReview is not null;

    static void ConfigureOpeningBustReview(string variant, string[] args, Archive archive)
    {
        if (!int.TryParse(variant,out openingBustVariant) || openingBustVariant is < 0 or > 2)
            throw new InvalidDataException("Expected opening variant 0, 1 or 2.");
        if (Option(args,"--opening-mouth-table=") is string table)
        {
            var fields=table.Split(',');
            if(fields.Length!=3 || fields.Any(f=>f!="5" && f!="6"))
                throw new InvalidDataException("Expected three observed mouth keys: 5 or 6.");
            openingBustMouthKeys=fields.Select(int.Parse).ToArray();
        }
        openingBustWaitAudio=args.Contains("--reaction-wait-audio");
        openingBustReview=NativeOpeningBank.Parse(
            Encoding.Latin1.GetString(archive.Read(NativeOpeningBank.Manifest)),
            archive.Entries.Select(e=>e.Name));
        ResetOpeningBustReview();
    }

    static void ResetOpeningBustReview()
    {
        openingBustPlayback=new(openingBustVariant);
        openingBustAudioComplete=!openingBustWaitAudio;
        openingBustRequestedSound=null;
    }

    static void InitializeOpeningBustReview()
    {
        foreach(var r in openingBustReview!.Resources)
        {
            // Prime the same texture variant used by the compositor.
            var texture=new NativeOpeningLayer(r.Key,r.Path,r.Position).UsesCornerColorKey
                ? graphics.CornerKeyedBitmapTexture(r.Path) : graphics.Texture(r.Path);
            if(texture.Width!=r.Width || texture.Height!=r.Height)
                throw new InvalidDataException("Unexpected opening resource dimensions: "+r.Path);
        }
    }

    static NativeOpeningLayer[] OpeningBustLayers() => openingBustPlayback.Active
        ? openingBustReview!.Sample(openingBustPlayback.Counter,openingBustVariant,openingBustMouthKeys)
        : [];

    static void DrawOpeningBustReview()
    {
        if(openingBustWaitAudio && Raylib.IsKeyPressed(KeyboardKey.F9) && openingBustPlayback.VoiceRequested)
            openingBustAudioComplete=true;
        if(ReviewReactionStep(openingBustPlayback.Active,ResetOpeningBustReview))
        {
            openingBustRequestedSound=openingBustPlayback.AdvanceNativeUpdate(openingBustAudioComplete);
            openingBustPlayback.CompleteNativeDraw();
            if(!openingBustPlayback.Active) reactionReviewClock.Pause();
        }

        // Post-step review: once the terminal native Draw clears active,
        // show the neutral canvas. No real audio or duel is running.
        foreach(var layer in OpeningBustLayers())
        {
            var texture=layer.UsesCornerColorKey ? graphics.CornerKeyedBitmapTexture(layer.Resource)
                : graphics.Texture(layer.Resource);
            Raylib.DrawTexture(texture,layer.Position.X,layer.Position.Y,Color.White);
        }
    }

    static void CaptureOpeningBustReview(RenderTexture2D canvas)
    {
        string basename=SaveVisualReviewImage(canvas,"opening-bust");
        File.WriteAllText(basename+".json",JsonSerializer.Serialize(new
        {
            schemaVersion=1,
            source="YugiRE isolated V31 opening bust review",
            routine="00415840",
            variant=openingBustVariant,
            counter=openingBustPlayback.Counter,
            active=openingBustPlayback.Active,
            voiceRequested=openingBustPlayback.VoiceRequested,
            lastSoundRequest=openingBustRequestedSound,
            audioPredicate=openingBustAudioComplete,
            simulatedAudio=true,
            playbackFps=reactionReviewClock.FramesPerSecond,
            mouthTable=openingBustMouthKeys,
            mouthTableKnown=openingBustMouthKeys is not null,
            layers=OpeningBustLayers(),
            compositor="diagnostic raylib layers; native temporary surface not reproduced",
            boundary="post-step; terminal inactive frame omitted"
        },new JsonSerializerOptions { WriteIndented=true })+Environment.NewLine);
    }
}
