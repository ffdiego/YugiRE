using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Numerics;

static partial class Game
{
    enum ActivationStage { Travel, Glow }

    static readonly PresentationSequence<ActivationStage> activation = new();
    static readonly PresentationTickClock activationClock = new();
    static readonly int[] activationCosine = ActivationEffectPlayback.ApproximateCosineTable();
    static Card? activationCard;
    static CardBounds activationBounds, activationOrigin;
    static int activationCategory;
    static bool ActivationBusy => activation.Current is not null;

    static CardBounds ToCardBounds(Rectangle rect) =>
        new((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);

    static void BeginCardActivation(Card card, Rectangle zone, Rectangle? origin = null)
    {
        ShowActivatedDetail(card);
        BeginActivation(ToCardBounds(zone), 0, card, origin);
    }

    static void BeginSpellActivation(int centerX, int centerY, int category)
    {
        const int width = 66, height = 88;
        BeginActivation(new(centerX - width / 2, centerY - height / 2, width, height), category);
    }

    static void BeginActivation(CardBounds zone, int category, Card? card = null, Rectangle? origin = null)
    {
        StopSpellActivation();
        if (!presentation.Style.PlayAnimations) return;
        activationCard = card;
        activationBounds = zone;
        activationCategory = category;
        if (origin is { } from)
        {
            activationOrigin = ToCardBounds(from);
            activation.Enqueue(ActivationStage.Travel, CardTravel.Updates);
        }
        activation.Enqueue(ActivationStage.Glow, ActivationEffectPlayback.MaxUpdates);
    }

    static void StopSpellActivation()
    {
        activation.Clear();
        activationClock.Reset();
        activationCard = null;
    }

    static void AdvanceSpellActivation(TimeSpan elapsed)
    {
        if (!ActivationBusy) return;
        activation.Advance(activationClock.Advance(elapsed, presentation.Style));
        if (!ActivationBusy) activationCard = null;
    }

    static bool ActivationInFlight(Rectangle zone) =>
        activation.Current?.Kind == ActivationStage.Travel && activationBounds == ToCardBounds(zone);

    static void DrawSpellActivation()
    {
        if (activation.Current is not { } step) return;

        var scratch = activationBounds;
        if (activationCard is { } card)
        {
            // A spent spell can already be in the graveyard. This is only its visual copy.
            var bounds = step.Kind == ActivationStage.Travel
                ? CardTravel.Sample(activationOrigin, scratch, activation.ElapsedUpdates)
                : scratch;
            DrawFieldCard(card, new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height), false);
        }
        if (step.Kind != ActivationStage.Glow) return;

        int counter = activation.ElapsedUpdates * NativeActivationTimeline.UpdateIncrement;
        var frame = NativeActivationFrame.FromCounter(counter, activationCategory, activationCosine);
        DrawActivationSprite(frame.Lower);
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
            new Rectangle(activationBounds.X + activationBounds.Width / 2f,
                activationBounds.Y + activationBounds.Height / 2f, width, height),
            new Vector2(width / 2, height / 2),
            pass.RotationIndex * (360.0f / 512.0f),
            new Color(255, 255, 255, Math.Clamp(pass.Opacity, 0, 255)));
        // Raylib's alpha blend differs from the original CPU raster's two
        // integer divisions and color-key semantics. Visual parity unverified.
    }
}
