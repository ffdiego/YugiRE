using Joey.LookAndFeel;
using Raylib_cs;

static partial class Game
{
    private static bool duelSystemCursorHidden;

    static void RestoreSystemCursor()
    {
        if (!duelSystemCursorHidden) return;
        Raylib.ShowCursor();
        duelSystemCursorHidden = false;
    }

    /// <summary>
    /// Draw original DAT cursor00 at the logical 800x600 mouse position,
    /// after the duel overlays. Raylib's OS cursor is hidden only when a
    /// pointer-sized original sprite can be used inside the game canvas.
    /// Cursor01, hotspot, alternate state and native cadence await a
    /// paired original-game screenshot / cursor_e.txt inspection.
    /// </summary>
    static void DrawNativeDuelCursor()
    {
        if (mouse.X < 0 || mouse.X >= NativeDuelLayout.CanvasWidth ||
            mouse.Y < 0 || mouse.Y >= NativeDuelLayout.CanvasHeight)
        {
            RestoreSystemCursor();
            return;
        }

        var texture = graphics.Texture(NativeDuelCursor.DefaultPointer);
        if (!NativeDuelCursor.CanRenderPointer(texture.Width, texture.Height))
        {
            RestoreSystemCursor();
            return;
        }

        if (!duelSystemCursorHidden)
        {
            Raylib.HideCursor();
            duelSystemCursorHidden = true;
        }
        // Provisional top-left hotspot; this changes presentation only.
        Raylib.DrawTexture(texture, (int)mouse.X, (int)mouse.Y, Color.White);
    }
}
