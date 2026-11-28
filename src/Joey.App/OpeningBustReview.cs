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
    static bool openingBustVoiceStarted;
    static bool ReactionReviewActive => reactionReview is not null || openingBustReview is not null;

    static void ConfigureOpeningBustReview(string variant, string[] args, Archive archive)
    {
        if (!int.TryParse(variant,out openingBustVariant) || openingBustVariant is < 0 or > 2)
            throw new InvalidDataException("Expected opening variant 0, 1 or 2.");
        openingBustWaitAudio=args.Contains("--reaction-wait-audio");
        openingBustReview=NativeOpeningBank.Parse(
            Encoding.Latin1.GetString(archive.Read(NativeOpeningBank.Manifest)),
            archive.Entries.Select(e=>e.Name));
        ResetOpeningBustReview();
    }

    static void ResetOpeningBustReview()
    {
        duelAudio?.StopOpeningVoice();
        openingBustPlayback=new(openingBustVariant);
        openingBustAudioComplete=!openingBustWaitAudio;
        openingBustVoiceStarted=false;
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
        ? openingBustReview!.Sample(openingBustPlayback.Counter,openingBustVariant)
        : [];

    static void DrawOpeningBustReview()
    {
        if(openingBustWaitAudio && Raylib.IsKeyPressed(KeyboardKey.F9) && openingBustPlayback.VoiceRequested)
            openingBustAudioComplete=true;
        bool advance=ReviewReactionStep(openingBustPlayback.Active,ResetOpeningBustReview);
        bool paused=reactionReviewClock.FramesPerSecond==0;
        // Pause/resume the actual voice with the review transport. Speedy
        // changes the visual cadence, not the WAV pitch or playback rate.
        duelAudio?.SetOpeningVoicePaused(paused);
        if(advance)
        {
            if(!openingBustWaitAudio && openingBustVoiceStarted)
                openingBustAudioComplete=duelAudio?.OpeningVoiceComplete!=false;
            if(openingBustPlayback.AdvanceNativeUpdate(openingBustAudioComplete) is uint request)
            {
                openingBustRequestedSound=request;
                if(!openingBustWaitAudio && presentation.Style.PlayJoeyVoice)
                    openingBustVoiceStarted=duelAudio?.PlayOpeningVoice(request)==true;
                if(openingBustVoiceStarted)
                {
                    openingBustAudioComplete=false;
                    duelAudio?.SetOpeningVoicePaused(paused);
                }
            }
            openingBustPlayback.CompleteNativeDraw();
            if(!openingBustPlayback.Active)
            {
                reactionReviewClock.Pause();
                duelAudio?.StopOpeningVoice();
            }
        }

        // Post-step review: once the terminal native Draw clears active,
        // show the neutral canvas. This review never runs duel rules.
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
            simulatedAudio=openingBustWaitAudio || !openingBustVoiceStarted,
            voiceStarted=openingBustVoiceStarted,
            voiceResource=NativeOpeningPlayback.VoicePath(openingBustPlayback.VoiceRequest),
            playbackFps=reactionReviewClock.FramesPerSecond,
            mouthTable=new[]{0,5,6,5},
            mouthTableKnown=true,
            layers=OpeningBustLayers(),
            compositor="diagnostic raylib layers; native temporary surface not reproduced",
            boundary="post-step; terminal inactive frame omitted"
        },new JsonSerializerOptions { WriteIndented=true })+Environment.NewLine);
    }
}
