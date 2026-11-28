using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Globalization;
using System.Numerics;
using System.Text.Json;

static partial class Game
{
    static NativeReaction07? reactionReview;
    static NativeReactionPlayback reactionPlayback = new(NativeReaction07.ObservedPrimarySound);
    static uint reactionInitialScene, reactionScene;
    static bool reactionWaitAudio, reactionAudioComplete;
    static NativeReactionStep? reactionLastStep;
    static readonly NativeReviewClock nativeReviewClock = new();

    // Shared transport for native Update/Draw reviews. F9 is
    // exclusively the optional simulated audio predicate.
    static bool ReviewNativeStep(bool active, Action reset)
    {
        if(Raylib.IsKeyPressed(KeyboardKey.F8))
        {
            nativeReviewClock.Pause();
            reset();
            return false;
        }
        if(Raylib.IsKeyPressed(KeyboardKey.F7))
        {
            nativeReviewClock.Pause();
            return active;
        }
        int requestedRate=Raylib.IsKeyPressed(KeyboardKey.F5)?30:
            Raylib.IsKeyPressed(KeyboardKey.F6)?60:0;
        if(requestedRate!=0)
        {
            if(!active)
            {
                nativeReviewClock.Pause();
                reset();
                active=true;
            }
            nativeReviewClock.Toggle(requestedRate);
        }
        if(!active) { nativeReviewClock.Pause(); return false; }
        return nativeReviewClock.Advance(Raylib.GetFrameTime());
    }

    static void ConfigureReactionReview(string[] args, Archive archive)
    {
        if (Option(args,"--review-reaction=") is not string value) return;
        if (args.Contains("--duel-console") || Option(args,"--duel-script=") is not null ||
            args.Contains("--alpha-headless"))
            throw new InvalidDataException("Reaction review requires an isolated graphical session.");
        if (value.StartsWith("start:",StringComparison.Ordinal))
        {
            ConfigureOpeningBustReview(value[6..],args,archive);
            return;
        }
        if (value.StartsWith("result:",StringComparison.Ordinal))
        {
            ConfigureResultBustReview(value[7..],args,archive);
            return;
        }
        if (value != "07") throw new InvalidDataException("Expected reaction07, start:0..2 or result:win/lose/draw:variant.");
        if (Option(args,"--reaction-scene-state=") is string phase &&
            !uint.TryParse(phase,NumberStyles.None,CultureInfo.InvariantCulture,out reactionInitialScene))
            throw new InvalidDataException("Expected unsigned decimal scene state.");
        reactionWaitAudio = args.Contains("--reaction-wait-audio");
        reactionReview = ReadActivationReaction(archive);
        ResetReactionReview();
    }

    static void ResetReactionReview()
    {
        reactionPlayback.Reset(NativeReaction07.ObservedPrimarySound);
        reactionScene = reactionInitialScene;
        reactionAudioComplete = !reactionWaitAudio;
        reactionLastStep = null;
    }

    static void InitializeReactionReview()
    {
        if (resultBustReview is not null) { InitializeResultBustReview(); return; }
        if (openingBustReview is not null) { InitializeOpeningBustReview(); return; }
        if (reactionReview is not { } reaction) return;
        ValidateActivationReactionTextures(reaction);
    }

    static void DrawReactionReview()
    {
        if (resultBustReview is not null) { DrawResultBustReview(); return; }
        if (openingBustReview is not null) { DrawOpeningBustReview(); return; }
        if (reactionReview is not { } reaction) return;
        if (reactionWaitAudio && Raylib.IsKeyPressed(KeyboardKey.F9) &&
            reactionPlayback.Counter >= 8) reactionAudioComplete = true;
        if (ReviewNativeStep(reactionPlayback.Active,ResetReactionReview))
        {
            reactionScene = unchecked(reactionScene+1);
            reactionLastStep = reactionPlayback.Step(reactionAudioComplete);
            if(!reactionPlayback.Active) nativeReviewClock.Pause();
        }
        Raylib.DrawTexture(graphics.Texture(DatGraphics.DuelDetailBackground),0,0,Color.White);
        if (duelTheme.Board is Texture2D board)
            Raylib.DrawTexturePro(board,new Rectangle(0,0,board.Width,board.Height),
                ToRectangle(DuelSurfaceLayout.Board),Vector2.Zero,0,Color.White);
        // Review displays the post-step boundary. On the terminating step,
        // show the board again rather than replaying the final native Draw.
        if (!reactionPlayback.Active || reactionPlayback.Counter == 0) return;
        DrawActivationReactionLayers(reaction, reactionScene);
    }

    static void CaptureReactionReview(RenderTexture2D canvas)
    {
        if (resultBustReview is not null) { CaptureResultBustReview(canvas); return; }
        if (openingBustReview is not null) { CaptureOpeningBustReview(canvas); return; }
        if (reactionReview is not { } reaction) return;
        string basename = SaveVisualReviewImage(canvas,"reaction07");
        bool visible = reactionPlayback.Active && reactionPlayback.Counter != 0;
        File.WriteAllText(basename+".json",JsonSerializer.Serialize(new
        {
            schemaVersion=1,
            source="YugiRE isolated V31 reaction07 review",
            routine="00416BC0",
            manifest=NativeReaction07.Manifest,
            counter=reactionPlayback.Counter,
            active=reactionPlayback.Active,
            visible,
            sceneState=reactionScene,
            audioPredicate= reactionAudioComplete,
            simulatedAudio=true,
            playbackFps=nativeReviewClock.FramesPerSecond,
            lastStep=reactionLastStep,
            baseResource=visible?reaction.BaseResource:null,
            origin=reaction.Origin,
            layers=visible?reaction.Sample(reactionScene):Array.Empty<NativeReactionLayer>(),
            compositor="rectangular raylib bitmap preview; native composition not verified",
            boundary="post-step; inactive object omitted"
        },new JsonSerializerOptions { WriteIndented=true })+Environment.NewLine);
        Console.WriteLine("[visual-review] "+Path.GetFullPath(basename+".json"));
    }
}
