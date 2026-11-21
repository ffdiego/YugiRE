using Joey.LookAndFeel;
using Raylib_cs;
using System.Numerics;

static partial class Game
{
    // Render the presentation counter. Cosine lookup and blending are approximations.
    static readonly ActivationEffectPlayback spellActivation = new();

    static void BeginSpellActivation(int centerX, int centerY, int cardWidth = 66, int cardHeight = 88,
                                     int category = 0)
    {
        var center = new CardPoint(centerX, centerY);
        spellActivation.Start(center,
            new CardBounds(centerX - cardWidth / 2, centerY - cardHeight / 2,
                           cardWidth, cardHeight),
            category, presentation.Style);
    }

    static void DrawSpellActivation()
    {
        if (!spellActivation.IsPlaying) return;

        var frame = spellActivation.Frame;
        DrawActivationSprite(frame.Lower);

        // The middle pass is generated black scratch, NOT a third texture.
        // Its size is currently a declared zone-size approximation.
        var scratch = spellActivation.ScratchBounds;
        Raylib.DrawRectangle(scratch.X, scratch.Y, scratch.Width, scratch.Height,
            new Color(0, 0, 0, Math.Clamp(frame.Scratch.Opacity, 0, 255)));

        DrawActivationSprite(frame.Upper);
    }

    static void DrawActivationSprite(NativeActivationPass pass)
    {
        if (pass.Opacity <= 0 || pass.ScaleFixed16 <= 0) return;
        var texture = graphics.Texture(ActivationSpriteNames.At(pass.ResourceIndex));
        float scale = pass.ScaleFixed16 / 65536.0f;
        float width = texture.Width * scale, height = texture.Height * scale;
        Raylib.DrawTexturePro(texture,
            new Rectangle(0, 0, texture.Width, texture.Height),
            new Rectangle(spellActivation.Center.X, spellActivation.Center.Y, width, height),
            new Vector2(width / 2, height / 2),
            pass.RotationIndex * (360.0f / 512.0f),
            new Color(255, 255, 255, Math.Clamp(pass.Opacity, 0, 255)));
        // Raylib's alpha blend differs from the original CPU raster's two
        // integer divisions and color-key semantics. Visual parity unverified.
    }
}
