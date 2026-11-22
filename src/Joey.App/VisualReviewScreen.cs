using Joey.LookAndFeel;
using Raylib_cs;
using System.Text.Json;

static partial class Game
{
    // F6 pause, F7 one nominal presentation Update, F12 screenshot + metadata.
    // Debug controls are never visible in screenshots unless F3 is enabled.
    static bool visualReviewPaused;
    static bool visualReviewStep;

    static void CaptureVisualReview(RenderTexture2D canvas)
    {
        // Read the 800x600 render target, not the potentially resized window.
        // Explicit F12 is the only trigger; no automatic captures or storage.
        string folder = Path.Combine("scratch", "visual-review");
        Directory.CreateDirectory(folder);
        string basename = "yugire-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        string png = Path.Combine(folder, basename + ".png");
        string json = Path.Combine(folder, basename + ".json");

        Image image = Raylib.LoadImageFromTexture(canvas.Texture);
        try
        {
            Raylib.ImageFlipVertical(ref image);
            if (!Raylib.ExportImage(image, png))
                throw new IOException("raylib could not export the 800x600 canvas");
        }
        finally { Raylib.UnloadImage(image); }

        // Recording counters makes comparing frames with an original-game
        // screenshot easier. This metadata is about YugiRE, not a native trace.
        var snapshot = new
        {
            schemaVersion = 1,
            source = "YugiRE presentation canvas (not the native executable)",
            capturedUtc = DateTimeOffset.UtcNow.ToString("O"),
            width = 800,
            height = 600,
            style = presentation.Style.Name,
            paused = visualReviewPaused,
            reviewStep = visualReviewStep,
            debugVisible = duelDebug,
            turn = duel.Turn,
            phase = duel.Phase.ToString(),
            playerPerspective = ViewPlayer,
            logicalLife = new[] { duel.Life[0], duel.Life[1] },
            visibleLife = new[] { lifeDisplays[0].Displayed, lifeDisplays[1].Displayed },
            pendingLifeDelta = new[] { lifeDisplays[0].PendingDelta, lifeDisplays[1].PendingDelta },
            cardEffect = cardEffects.Current?.Kind.GetType().Name ?? "None",
            cardEffectUpdate = cardEffects.ElapsedUpdates,
            cinematicKind = cardEffects.Current?.Kind is CinematicScene active
                ? active.Cue.Kind.ToString() : "None",
            cinematicManifest = cardEffects.Current?.Kind is CinematicScene scene
                ? scene.Cue.Manifest : null,
            lastSignatureCandidateResourceId = lastSignatureCandidate?.CardResourceId,
            lastSignatureCandidateManifest = lastSignatureCandidate?.Manifest,
            signatureCinematicRendered = lastSignatureCandidate?.HasPlayableDraw ?? false,
            flightUpdate = flight is null ? (int?)null : travelUpdate,
            attackUpdate = attackCue is null ? (int?)null : attackUpdates,
            activationEffect = activation.Current?.Kind.ToString() ?? "None",
            activationUpdate = activation.ElapsedUpdates,
            gameOver = duel.GameOver,
            winner = duel.Winner,
            notes = "Position, sprite selection and compositor parity must be checked in Joey."
        };
        File.WriteAllText(json, JsonSerializer.Serialize(snapshot,
            new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        Console.WriteLine($"[visual-review] {Path.GetFullPath(png)}");
        Console.WriteLine($"[visual-review] {Path.GetFullPath(json)}");
    }
}
