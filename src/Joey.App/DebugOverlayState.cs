using Joey.LookAndFeel;

internal enum DebugOverlayControl { None, Hide, Menu, Reset, Prompts }

/// <summary>App-local diagnostic UI state. No duel state, rule or clock.
/// Captures only pointer presses on visible, interactive overlay regions.
/// The capture survives hiding until the next frame to prevent click-through.</summary>
internal sealed class DebugOverlayState
{
    public static readonly CardBounds HideBounds = new(262, 8, 142, 30);
    public static readonly CardBounds MenuBounds = new(411, 8, 85, 30);
    public static readonly CardBounds ResetBounds = new(501, 8, 75, 30);
    public static readonly CardBounds PromptsBounds = new(583, 8, 172, 30);
    public static readonly CardBounds MessageBounds = new(262, 42, 502, 26);

    public bool Enabled { get; private set; }
    public bool CapturesPointerPress { get; private set; }
    public DebugOverlayControl PressedControl { get; private set; }

    public void Toggle() => Enabled = !Enabled;

    public void ResetFrame()
    {
        CapturesPointerPress = false;
        PressedControl = DebugOverlayControl.None;
    }

    public void BeginDuelFrame(bool toggle, bool left, bool right,
        float x, float y, bool showPrompts)
    {
        ResetFrame();
        if (toggle) Toggle();
        if (!Enabled || (!left && !right)) return;

        DebugOverlayControl control = Contains(HideBounds,x,y) ? DebugOverlayControl.Hide :
            Contains(MenuBounds,x,y) ? DebugOverlayControl.Menu :
            Contains(ResetBounds,x,y) ? DebugOverlayControl.Reset :
            showPrompts && Contains(PromptsBounds,x,y) ? DebugOverlayControl.Prompts :
            DebugOverlayControl.None;

        CapturesPointerPress = control != DebugOverlayControl.None || Contains(MessageBounds,x,y);
        if (!left) return;
        PressedControl = control;
        if (control == DebugOverlayControl.Hide) Enabled = false;
    }

    private static bool Contains(CardBounds r,float x,float y) =>
        x >= r.X && x < r.X+r.Width && y >= r.Y && y < r.Y+r.Height;
}
