using Joey.LookAndFeel;
using Raylib_cs;
using System.Numerics;

static partial class Game
{
    /// <summary>
    /// Select EXACTLY ONE original cursor sprite for the current frame.
    /// Action-cloud YGAs are variants of the pointer, not additional
    /// card-anchored overlays.
    /// </summary>
    static string ActiveWindowCursorSprite()
    {
        if (screen == "duel" && !visualReviewPaused &&
            !MotionBusy && !PhaseBusy && !alphaBlocked &&
            CanPlayTurn && CanPlayMainOrBattle &&
            CpuDuelController.PendingOwner(duel) is null &&
            !choosingTributes && !choosingEquip &&
            mouse.X >= 0 && mouse.X < NativeDuelLayout.CanvasWidth &&
            mouse.Y >= 0 && mouse.Y < NativeDuelLayout.CanvasHeight &&
            hoverHand >= 0 && hoverHand < ViewHand.Count)
            return NativeDuelCursor.CursorSprite(CurrentHandAction(hoverHand));

        return NativeDuelCursor.DefaultPointer;
    }

    /// <summary>
    /// Draw in WINDOW coordinates, after composing the scaled 800x600
    /// scene. This remains visible on menu, cards, deck and duel screens,
    /// including the letterbox area while the window is resized.
    /// </summary>
    static void DrawWindowCursor(float canvasScale)
    {
        var texture = graphics.Texture(ActiveWindowCursorSprite());
        Vector2 mousePosition = Raylib.GetMousePosition();
        var origin = NativeDuelCursor.PointerOrigin(
            new CardPoint((int)mousePosition.X, (int)mousePosition.Y));
        Raylib.DrawTextureEx(texture, new Vector2(origin.X, origin.Y),
            0f, canvasScale, Color.White);
    }
}
