using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

static partial class Game
{
    static NativeReaction07? reactionReview;
    static NativeReactionPlayback reactionPlayback = new(NativeReaction07.ObservedPrimarySound);
    static uint reactionInitialScene, reactionScene;
    static bool reactionWaitAudio, reactionAudioComplete;
    static NativeReactionStep? reactionLastStep;

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
        if (value != "07") throw new InvalidDataException("Expected reaction07 (07) or opening (start:0..2).");
        if (Option(args,"--reaction-scene-state=") is string phase &&
            !uint.TryParse(phase,NumberStyles.None,CultureInfo.InvariantCulture,out reactionInitialScene))
            throw new InvalidDataException("Expected unsigned decimal scene state.");
        reactionWaitAudio = args.Contains("--reaction-wait-audio");
        reactionReview = NativeReaction07.Parse(Encoding.Latin1.GetString(archive.Read(NativeReaction07.Manifest)),
            archive.Entries.Select(entry => entry.Name));
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
        if (openingBustReview is not null) { InitializeOpeningBustReview(); return; }
        if (reactionReview is not { } reaction) return;
        foreach (string resource in reaction.Resources) _ = graphics.Texture(resource);
        var basis = graphics.Texture(reaction.BaseResource);
        if (basis.Width != NativeReaction07.Width || basis.Height != NativeReaction07.Height)
            throw new InvalidDataException("Unexpected reaction07 base bitmap dimensions.");
    }

    static void DrawReactionReview()
    {
        if (openingBustReview is not null) { DrawOpeningBustReview(); return; }
        if (reactionReview is not { } reaction) return;
        if (Raylib.IsKeyPressed(KeyboardKey.F8)) ResetReactionReview();
        else
        {
            // F5 supplies the simulated audio predicate. No actual sound is
            // played in this review; a device cannot change the frame index.
            if (reactionWaitAudio && Raylib.IsKeyPressed(KeyboardKey.F5) &&
                reactionPlayback.Counter >= 8) reactionAudioComplete = true;
            if (Raylib.IsKeyPressed(KeyboardKey.F7) && reactionPlayback.Active)
            {
                reactionScene = unchecked(reactionScene+1);
                reactionLastStep = reactionPlayback.Step(reactionAudioComplete);
            }
        }
        Raylib.DrawTexture(graphics.Texture(DatGraphics.DuelDetailBackground),0,0,Color.White);
        if (duelTheme.Board is Texture2D board)
            Raylib.DrawTexturePro(board,new Rectangle(0,0,board.Width,board.Height),
                ToRectangle(DuelSurfaceLayout.Board),Vector2.Zero,0,Color.White);
        // Review displays the post-step boundary. On the terminating step,
        // show the board again rather than replaying the final native Draw.
        if (!reactionPlayback.Active || reactionPlayback.Counter == 0) return;
        Raylib.DrawTexture(graphics.Texture(reaction.BaseResource),reaction.Origin.X,reaction.Origin.Y,Color.White);
        foreach (var layer in reaction.Sample(reactionScene))
            Raylib.DrawTexture(graphics.Texture(layer.Resource),layer.Position.X,layer.Position.Y,Color.White);
    }

    static void CaptureReactionReview(RenderTexture2D canvas)
    {
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
