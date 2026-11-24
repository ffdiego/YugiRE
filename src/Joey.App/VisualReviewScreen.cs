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
        string basename = SaveVisualReviewImage(canvas, "yugire");
        string json = basename + ".json";

        // Recording counters makes comparing frames with an original-game
        // screenshot easier. This metadata is about YugiRE, not a native trace.
        // Both fields describe the same pointer decision, including field Attack.
        string cursorSprite = ActiveWindowCursorSprite();
        var snapshot = new
        {
            schemaVersion = 1,
            source = "YugiRE presentation canvas (not the native executable)",
            capturedUtc = DateTimeOffset.UtcNow.ToString("O"),
            width = 800,
            height = 600,
            style = presentation.Style.Name,
            cosineSource = effectCosineFromNativeCapture ? "native-runtime-capture" : "approximate",
            paused = visualReviewPaused,
            reviewStep = visualReviewStep,
            debugVisible = duelDebug,
            detailTextFace = NativeDetailPanel.EnglishFace,
            detailSystemFontLoaded = duelTheme.DetailTextFont(bold: false) is not null,
            detailViewport = new { x = NativeDetailPanel.TextX, y = NativeDetailPanel.TextY,
                width = NativeDetailPanel.TextWidth, height = NativeDetailPanel.TextHeight },
            detailResource = DatGraphics.DuelDetailBackground,
            detailScroll = duelDescriptionScroll,
            detailCardResourceId = detailCard?.Id,
            mouseCanvas = new { x = mouse.X, y = mouse.Y },
            mousePointerSource = "original-data.dat-window-overlay",
            cursorSprite,
            actionCloud = NativeDuelCursor.ActionForSprite(cursorSprite)?.ToString(),
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
            destinyBoardWinner = duel.DestinyBoardWinner,
            destinyBoardManifest = lastDestinyBoardCandidate?.Manifest,
            destinyBoardCinematicRendered = lastDestinyBoardCandidate?.HasPlayableDraw ?? false,
            faceUpSpellResourceIds = Enumerable.Range(0, 2)
                .Select(player => Enumerable.Range(0, 5)
                    .Where(zone => !duel.SpellFaceDown[player, zone] &&
                        duel.Spells[player, zone] is not null)
                    .Select(zone => duel.Spells[player, zone]!.Id)
                    .ToArray())
                .ToArray(),
            // F12 stores current presentation numerals as well as the
            // logical attributes, so screenshot differences can be paired
            // with the exact easing state. Never disclose hidden CPU stats.
            fieldAttributes = Enumerable.Range(0, 2).SelectMany(player =>
                Enumerable.Range(0, NativeDuelLayout.MonsterSlots)
                    .Where(zone => fieldAttributeTimelines[player, zone]?.HasCard == true &&
                        duel.Monsters[player, zone] is not null &&
                        NativeFieldAttributeVisibility.ShouldDrawClient(
                            player == ViewPlayer,
                            duel.MonsterFaceDown[player, zone],
                            settled: !InFlight(player, zone, spell: false),
                            hiddenDefenderBeforeImpact:
                                IsHiddenAttackTargetBeforeImpact(player, zone)))
                    .Select(zone => new
                    {
                        player,
                        zone,
                        cardResourceId = duel.Monsters[player, zone]!.Id,
                        displayedAttack = fieldAttributeTimelines[player, zone]!.Displayed.Attack,
                        displayedDefense = fieldAttributeTimelines[player, zone]!.Displayed.Defense,
                        effectiveAttack = duel.AttackValue(player, zone),
                        effectiveDefense = duel.DefenseValue(player, zone)
                    })).ToArray(),
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
        Console.WriteLine($"[visual-review] {Path.GetFullPath(json)}");
    }

    static string SaveVisualReviewImage(RenderTexture2D canvas, string prefix)
    {
        string folder = Path.Combine("scratch", "visual-review");
        Directory.CreateDirectory(folder);
        string basename = Path.Combine(folder, prefix + "-" +
            DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
        string png = basename + ".png";
        Image image = Raylib.LoadImageFromTexture(canvas.Texture);
        try
        {
            Raylib.ImageFlipVertical(ref image);
            if (!Raylib.ExportImage(image, png))
                throw new IOException("raylib could not export the 800x600 canvas");
        }
        finally { Raylib.UnloadImage(image); }
        Console.WriteLine($"[visual-review] {Path.GetFullPath(png)}");
        return basename;
    }
}
