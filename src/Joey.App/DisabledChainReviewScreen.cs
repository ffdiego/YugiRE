using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

static partial class Game
{
    static NativeDisabledChainTarget? disabledChainReviewTarget;
    static string[] disabledChainReviewResources = [];
    static int disabledChainReviewPerspective;
    static readonly NativeDisabledChainTimeline disabledChainReviewTimeline = new();

    // Explicit visual lab only; does not read or write the rules engine, does
    // not auto-play a duel, and does not imply a mapping to generic negation.
    static void ConfigureDisabledChainReview(string[] args, Archive archive)
    {
        if (Option(args, "--review-disabled-chain=") is not string value) return;
        if (!ushort.TryParse(value, NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture, out ushort descriptor))
            throw new InvalidDataException("Expected hexadecimal descriptor without 0x prefix.");
        try { disabledChainReviewTarget = new NativeDisabledChainTarget(descriptor); }
        catch (ArgumentException ex) { throw new InvalidDataException(ex.Message, ex); }
        if (Option(args, "--review-perspective=") is string perspective &&
            (!int.TryParse(perspective, out disabledChainReviewPerspective) ||
             disabledChainReviewPerspective is < 0 or > 1))
            throw new InvalidDataException("Review perspective must be 0 or 1.");

        string manifest = Encoding.Latin1.GetString(archive.Read(NativeDisabledChainResources.Manifest));
        disabledChainReviewResources = NativeDisabledChainResources.Parse(manifest,
            archive.Entries.Select(entry => entry.Name));
        disabledChainReviewTimeline.Reset();
    }

    static void DrawDisabledChainReview()
    {
        if (disabledChainReviewTarget is not { } target) return;
        // No invented real-time cadence. Each explicit step is one native
        // Update. Counter 24 displays the inactive object (board only).
        if (Raylib.IsKeyPressed(KeyboardKey.F8)) disabledChainReviewTimeline.Reset();
        else if (Raylib.IsKeyPressed(KeyboardKey.F7)) disabledChainReviewTimeline.AdvanceNativeUpdate();

        Raylib.DrawTexture(graphics.Texture(DatGraphics.DuelDetailBackground), 0, 0, Color.White);
        if (duelTheme.Board is Texture2D board)
            Raylib.DrawTexturePro(board, new Rectangle(0, 0, board.Width, board.Height),
                ToRectangle(DuelSurfaceLayout.Board), Vector2.Zero, 0, Color.White);

        if (disabledChainReviewTimeline.Sample(target.Descriptor) is { } frame)
            DrawDisabledChainIndicator(target.Center(disabledChainReviewPerspective), frame);
    }

    // Whole image, no atlas crop. Integer destination bounds are defined by
    // 004531C0; for 90 degrees position the unrotated quad's top-left at
    // the rotated rectangle's upper-right boundary. The GPU blend and
    // decoder policies are the existing client ones, not proven native.
    static void DrawDisabledChainIndicator(CardPoint center, NativeDisabledChainFrame frame,string[]? resources=null)
    {
        var texture = graphics.Texture((resources??disabledChainReviewResources)[frame.FrameIndex]);
        var bounds = frame.BoundsAt(center, texture.Width, texture.Height);
        if (!frame.DescriptorBit4000)
            Raylib.DrawTexture(texture, bounds.X, bounds.Y, Color.White);
        else
            Raylib.DrawTexturePro(texture, new Rectangle(0, 0, texture.Width, texture.Height),
                new Rectangle(bounds.X + bounds.Width, bounds.Y, texture.Width, texture.Height),
                Vector2.Zero, frame.Degrees, Color.White);
    }

    static void CaptureDisabledChainReview(RenderTexture2D canvas)
    {
        if (disabledChainReviewTarget is not { } target) return;
        string basename = SaveVisualReviewImage(canvas, "disabled-chain");
        var frame = disabledChainReviewTimeline.Sample(target.Descriptor);
        string? resource = null;
        CardBounds? bounds = null;
        var center = target.Center(disabledChainReviewPerspective);
        if (frame is { } active)
        {
            resource = disabledChainReviewResources[active.FrameIndex];
            var texture = graphics.Texture(resource);
            bounds = active.BoundsAt(center, texture.Width, texture.Height);
        }
        File.WriteAllText(basename + ".json", JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            source = "YugiRE isolated V12 review (not a gameplay trigger or native capture)",
            routine = "004531C0",
            descriptor = target.Descriptor,
            perspective = disabledChainReviewPerspective,
            counter = disabledChainReviewTimeline.Counter,
            active = disabledChainReviewTimeline.Active,
            frameIndex = frame?.FrameIndex,
            rotation = frame?.Degrees,
            resource,
            center,
            bounds,
            compositor = "existing raylib texture policy; native blend not verified"
        }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        Console.WriteLine("[visual-review] " + Path.GetFullPath(basename + ".json"));
    }
}
