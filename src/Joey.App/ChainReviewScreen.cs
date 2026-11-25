using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Numerics;
using System.Text;
using System.Text.Json;

static partial class Game
{
    static NativeChainTimeline? chainReview;
    static string[] chainReviewResources = [];
    static CardPoint chainReviewCenter;
    static ushort chainReviewDescriptor;

    static void ConfigureChainReview(string[] args, Archive archive)
    {
        if (Option(args, "--review-chain=") is not string value) return;
        string[] fields = value.Split(',');
        if (fields.Length != 3 || !ushort.TryParse(fields[0], out ushort number) ||
            !int.TryParse(fields[1], out int side) || side is < 0 or > 1 ||
            !int.TryParse(fields[2], out int zone) || zone is < 0 or > 10)
            throw new InvalidDataException("Chain review expects --review-chain=NUMBER,SIDE,FIELD_ZONE (0–10).");
        int perspective = 0;
        if (Option(args, "--review-perspective=") is string setting &&
            (!int.TryParse(setting, out perspective) || perspective is < 0 or > 1))
            throw new InvalidDataException("Review perspective must be 0 or 1.");

        chainReviewDescriptor = NativeChainTimeline.Descriptor(side, zone);
        chainReviewCenter = NativeDisabledChainTarget.FromField(side, zone, false).Center(perspective);
        chainReviewResources = NativeChainDraw.Resources(
            Encoding.Latin1.GetString(archive.Read(NativeChainDraw.Manifest)), archive.Entries.Select(e => e.Name), 'e');
        foreach (string resource in chainReviewResources)
            _ = BitmapInfo.Read(archive.Read(resource));
        chainReview = new(number);
    }

    static void DrawChainReview()
    {
        if (chainReview is not { } chain) return;
        if (Raylib.IsKeyPressed(KeyboardKey.F8)) chainReview = chain = new(chain.Number);
        if (Raylib.IsKeyPressed(KeyboardKey.F5)) chain.Release();
        if (Raylib.IsKeyPressed(KeyboardKey.F7)) chain.AdvanceNativeUpdate();

        Raylib.DrawTexture(graphics.Texture(DatGraphics.DuelDetailBackground), 0, 0, Color.White);
        if (duelTheme.Board is Texture2D board)
            Raylib.DrawTexturePro(board, new Rectangle(0, 0, board.Width, board.Height),
                ToRectangle(DuelSurfaceLayout.Board), Vector2.Zero, 0, Color.White);

        if (chain.SpriteSelector is not int selector) return;
        var sprite = graphics.Texture(chainReviewResources[selector]);
        var bounds = NativeChainDraw.Sprite(chainReviewCenter, sprite.Width, sprite.Height);
        Raylib.DrawTexture(sprite, bounds.X, bounds.Y, Color.White);

        var atlas = graphics.Texture(chainReviewResources[0]);
        foreach (var pass in NativeChainDraw.Digits(chain, chainReviewCenter, atlas.Width, atlas.Height))
            Raylib.DrawTexturePro(atlas, ToRectangle(pass.Source), ToRectangle(pass.Destination),
                Vector2.Zero, 0, new Color(255, 255, 255, Math.Clamp(pass.Intensity, 0, 255)));
    }

    static void CaptureChainReview(RenderTexture2D canvas)
    {
        if (chainReview is not { } chain) return;
        string basename = SaveVisualReviewImage(canvas, "chain");
        var atlas = graphics.Texture(chainReviewResources[0]);
        string? resource = null;
        CardBounds? bounds = null;
        if (chain.SpriteSelector is int selector)
        {
            resource = chainReviewResources[selector];
            var texture = graphics.Texture(resource);
            bounds = NativeChainDraw.Sprite(chainReviewCenter, texture.Width, texture.Height);
        }

        File.WriteAllText(basename + ".json", JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            source = "YugiRE isolated V23 review",
            routine = "0041F870",
            descriptor = chainReviewDescriptor,
            center = chainReviewCenter,
            chain.Counter,
            chain.Number,
            chain.Released,
            chain.Layer,
            chain.SpriteSelector,
            resource,
            bounds,
            atlas = chainReviewResources[0],
            digits = NativeChainDraw.Digits(chain, chainReviewCenter, atlas.Width, atlas.Height),
            compositor = "diagnostic raylib green key and linear alpha"
        }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        Console.WriteLine("[visual-review] " + Path.GetFullPath(basename + ".json"));
    }
}
