using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

static partial class Game
{
    static Card? fusionReviewCard;
    static NativeDisabledChainTarget fusionReviewTarget;
    static int fusionReviewPerspective;
    static bool fusionReviewReleased, fusionReviewCardActive = true;
    static NativeAttachedFusionTimeline fusionReviewTimeline = new(0);
    static string[] fusionReviewResources = [];
    static NativeFusionSprite? fusionReviewPass;

    static void ConfigureFusionReview(string[] args, Archive archive)
    {
        if (Option(args, "--review-attached-fusion=") is not string value) return;
        // Exact sample data is available through the already-existing
        // pending script 11. Never substitute the approximate cosine
        // table in this diagnostic path.
        if (!effectCosineFromNativeCapture)
            throw new InvalidDataException("Attached fusion review requires --native-cosine=<capture.json>.");
        string[] parts = value.Split(',');
        if (parts.Length != 2 ||
            !ushort.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out ushort id) ||
            !ushort.TryParse(parts[1], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort descriptor))
            throw new InvalidDataException("Expected card resource ID,hex field descriptor.");
        fusionReviewCard = cards.FirstOrDefault(c => c.Id == id && c.IsMonster) ??
            throw new InvalidDataException("Unknown monster resource ID.");
        try { fusionReviewTarget = new NativeDisabledChainTarget(descriptor); }
        catch (ArgumentException ex) { throw new InvalidDataException(ex.Message, ex); }
        if (fusionReviewTarget.Region != 0)
            throw new InvalidDataException("Attached review supports monster field slots only.");
        if (Option(args, "--review-perspective=") is string perspective &&
            (!int.TryParse(perspective, out fusionReviewPerspective) || fusionReviewPerspective is < 0 or > 1))
            throw new InvalidDataException("Review perspective must be 0 or 1.");
        if (graphics.Artwork.MiniFor(fusionReviewCard) is null)
            throw new InvalidDataException("Selected monster has no original miniature.");
        fusionReviewResources = NativeFusionResources.Parse(
            Encoding.Latin1.GetString(archive.Read(NativeFusionResources.Manifest)),
            archive.Entries.Select(e => e.Name));
        ResetFusionReview();
    }

    static void ResetFusionReview()
    {
        // Constructors 004874C0 and 004520E0 initialize +14/+18 to zero.
        fusionReviewTimeline = new(0);
        fusionReviewReleased = false;
        fusionReviewCardActive = true;
        fusionReviewPass = null;
    }

    static void DrawFusionReview()
    {
        if (fusionReviewCard is not { } card) return;
        if (Raylib.IsKeyPressed(KeyboardKey.F8)) ResetFusionReview();
        else
        {
            // Separate native Update and Draw: F7 updates then draws once;
            // F9 draws without advancing the waiting counter. Redrawing the
            // GUI itself must not consume extra native Draw mutations.
            if (Raylib.IsKeyPressed(KeyboardKey.F5)) fusionReviewReleased = !fusionReviewReleased;
            if (Raylib.IsKeyPressed(KeyboardKey.F6))
            {
                fusionReviewCardActive = !fusionReviewCardActive;
                if (!fusionReviewCardActive) fusionReviewPass = null;
            }
            if (Raylib.IsKeyPressed(KeyboardKey.F7))
            {
                fusionReviewTimeline.AdvanceNativeUpdate(fusionReviewReleased);
                fusionReviewPass = fusionReviewTimeline.Draw(fusionReviewCardActive, effectCosine);
            }
            else if (Raylib.IsKeyPressed(KeyboardKey.F9))
                fusionReviewPass = fusionReviewTimeline.Draw(fusionReviewCardActive, effectCosine);
        }

        Raylib.DrawTexture(graphics.Texture(DatGraphics.DuelDetailBackground), 0, 0, Color.White);
        if (duelTheme.Board is Texture2D board)
            Raylib.DrawTexturePro(board, new Rectangle(0, 0, board.Width, board.Height),
                ToRectangle(DuelSurfaceLayout.Board), Vector2.Zero, 0, Color.White);
        if (fusionReviewCardActive)
        {
            var center = fusionReviewTarget.Center(fusionReviewPerspective);
            DrawDuelCard(card, DuelCardLayout.OnBoard(DuelCardZone.Monster,
                fusionReviewTarget.Side == fusionReviewPerspective, fusionReviewTarget.Slot,
                fusionReviewTarget.Rotated, (fusionReviewTarget.Descriptor & 0x8000) == 0));
            if (fusionReviewPass is { } pass)
                DrawAttachedFusionReviewPass(center, pass);
        }
    }

    static void DrawAttachedFusionReviewPass(CardPoint center, NativeFusionSprite pass)
    {
        if (pass.ScaleFixed16 <= 0 || pass.Intensity <= 0) return;
        var texture = graphics.Texture(fusionReviewResources[pass.ResourceIndex]);
        // Existing client transform policy, only for isolated review.
        // Native 00422C00 uses integer MulDiv/quad and blend paths, so this
        // raylib adapter is intentionally not a pixel-parity assertion.
        float scale = pass.ScaleFixed16 / 65536f;
        float width = texture.Width * scale, height = texture.Height * scale;
        Raylib.DrawTexturePro(texture, new Rectangle(0, 0, texture.Width, texture.Height),
            new Rectangle(center.X, center.Y, width, height), new Vector2(width / 2, height / 2),
            (pass.AngleIndex & 511) * (360f / 512f),
            new Color(255, 255, 255, Math.Clamp(pass.Intensity, 0, 255)));
    }

    static void CaptureFusionReview(RenderTexture2D canvas)
    {
        string basename = SaveVisualReviewImage(canvas, "attached-fusion");
        File.WriteAllText(basename + ".json", JsonSerializer.Serialize(new {
            schemaVersion = 1, source = "YugiRE V11 isolated attached-fusion review",
            routine = "004877F0", resource = fusionReviewResources[0],
            cardResourceId = fusionReviewCard?.Id,
            descriptor = fusionReviewTarget.Descriptor, perspective = fusionReviewPerspective,
            center = fusionReviewTarget.Center(fusionReviewPerspective),
            counter = fusionReviewTimeline.Counter, angle = fusionReviewTimeline.Angle,
            active = fusionReviewTimeline.Active, releaseFlag = fusionReviewReleased,
            associatedCardActive = fusionReviewCardActive, pass = fusionReviewPass,
            cosineSource = "native-runtime-capture",
            compositor = "existing raylib transform; native 00422C00 pixel parity pending"
        }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        Console.WriteLine("[visual-review] " + Path.GetFullPath(basename + ".json"));
    }
}
