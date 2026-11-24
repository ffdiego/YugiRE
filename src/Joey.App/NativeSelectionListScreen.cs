using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;

static partial class Game
{
    static bool useLegacySelection;

    // A partial renderer of the stable branch of 0043B280, integrated
    // into live Core selections. The existing controls still submit the
    // same requests; no synthetic card effects or second rules queue.
    sealed class SelectionListView(NativeSelectionManifest manifest, int count)
    {
        public NativeSelectionManifest Manifest { get; } = manifest;
        public NativeSelectionWindow Window { get; } = new(count);
        public NativeSelectionBackground Background => Manifest.Background(Window.TotalCards);

        public void EnterStableBranch()
        {
            // Skip, rather than approximate, the unresolved transition.
            // The clock and origin will be wired once evidenced.
            Window.AdvanceOpening(8);
        }
    }

    static SelectionListView? CreateSelectionList(int mode, int owner, IReadOnlyList<Card> candidates)
    {
        if (useLegacySelection) return null;
        var manifest = graphics.SelectionManifest(mode, owner, ViewPlayer);
        if (manifest is null) return null;
        if (candidates.Any(card => graphics.Artwork.MiniFor(card) is null))
        {
            Console.Error.WriteLine("[selection] legacy fallback: missing original miniature");
            return null;
        }
        var view = new SelectionListView(manifest, candidates.Count);
        view.EnterStableBranch();
        return view;
    }

    static void DrawNativeSelectionBackground(SelectionListView view)
    {
        var texture = graphics.Texture(view.Background.Resource);
        var bounds = NativeSelectionManifest.BackgroundBounds(texture.Width, texture.Height);
        Raylib.DrawTexture(texture, bounds.X, bounds.Y, Color.White);
        if (Raylib.IsKeyPressed(KeyboardKey.Right)) view.Window.MoveNext();
        else if (Raylib.IsKeyPressed(KeyboardKey.Left)) view.Window.MovePrevious();
    }

    static bool DrawNativeSelectionCard(SelectionListView view, Card card, int slot,
        bool chosen, bool selectable = true)
    {
        var bg = graphics.Texture(view.Background.Resource);
        var texture = graphics.Texture(graphics.Artwork.MiniFor(card)!);
        var bounds = NativeSelectionManifest.SlotBounds(slot, bg.Height,
            view.Background.SlotYOffset, texture.Width, texture.Height);
        var rect = ToRectangle(bounds);
        Raylib.DrawTexture(texture, bounds.X, bounds.Y, Color.White);
        bool focused = view.Window.SelectedSlot == slot;
        // Reuse the already integrated original mini focus sprite. The
        // selection-specific variant/disabled marker and selected count
        // are not claimed as recovered; retain existing client feedback.
        if (focused)
            DrawFieldSelectionFrame(new CardRenderGeometry(bounds, 0, false, null), true);
        if (chosen || !selectable)
            Raylib.DrawRectangleLinesEx(rect, chosen ? 3 : 1, chosen ? Color.Lime : Color.Gray);
        if (!selectable) return false;
        if (Hit(rect) && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            view.Window.FocusVisibleSlot(slot);
            return true;
        }
        return focused && Raylib.IsKeyPressed(KeyboardKey.Enter);
    }

    static void DrawNativeSelectionNavigation(SelectionListView view)
    {
        // Client adapters for the native event 3/4 navigation. Reuse the
        // provisional buttons until the scroll submanifest geometry is known.
        var window = view.Window;
        if (Button("< Prev", 339, 531, 90, 30, window.SelectedCardIndex > 0))
            window.MovePrevious();
        Label(window.TotalCards == 0 ? "0/0" : $"{window.SelectedCardIndex + 1}/{window.TotalCards}",
            474, 537, 17, Gold);
        if (Button("Next >", 614, 531, 105, 30,
            window.SelectedCardIndex >= 0 && window.SelectedCardIndex < window.TotalCards - 1))
            window.MoveNext();
    }
}
