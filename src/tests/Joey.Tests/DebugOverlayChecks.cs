using Joey.LookAndFeel;

internal static class DebugOverlayChecks
{
    public static void Run(Action<bool,string> check)
    {
        var state = new DebugOverlayState();
        check(!state.Enabled && !state.CapturesPointerPress,"V13: faithful mode starts clean");
        state.BeginDuelFrame(false,true,false,270,10,true);
        check(!state.Enabled && !state.CapturesPointerPress &&
            state.PressedControl==DebugOverlayControl.None,"V13: hidden controls have no hitboxes");
        state.BeginDuelFrame(true,false,false,270,10,true);
        check(state.Enabled && !state.CapturesPointerPress,"V13: F3 enables overlay");
        state.BeginDuelFrame(false,true,false,270,10,true);
        check(!state.Enabled && state.CapturesPointerPress &&
            state.PressedControl==DebugOverlayControl.Hide,"V13: hide immediately, retain capture until next frame");
        state.ResetFrame();
        check(!state.Enabled && !state.CapturesPointerPress &&
            state.PressedControl==DebugOverlayControl.None,"V13: capture expires next frame");
        state.Toggle();
        check(state.Enabled,"V13: menu restores visibility");

        var controls = new[] {
            (DebugOverlayState.MenuBounds, DebugOverlayControl.Menu),
            (DebugOverlayState.ResetBounds, DebugOverlayControl.Reset),
            (DebugOverlayState.PromptsBounds, DebugOverlayControl.Prompts)
        };
        foreach(var (bounds,control) in controls)
        {
            state.BeginDuelFrame(false,true,false,bounds.X,bounds.Y,true);
            check(state.Enabled && state.CapturesPointerPress &&
                state.PressedControl==control,"V13: visible diagnostic control captures click");
            state.BeginDuelFrame(false,false,true,bounds.X,bounds.Y,true);
            check(state.Enabled && state.CapturesPointerPress &&
                state.PressedControl==DebugOverlayControl.None,"V13: right click does not activate diagnostic control");
        }
        state.BeginDuelFrame(false,true,false,584,9,false);
        check(!state.CapturesPointerPress,"V13: absent CPU-mode prompt button has no hitbox");
        state.BeginDuelFrame(false,true,false,300,50,true);
        check(state.CapturesPointerPress && state.PressedControl==DebugOverlayControl.None,
            "V13: opaque diagnostic message captures pointer without command");
        state.BeginDuelFrame(false,true,false,404,10,true);
        check(!state.CapturesPointerPress,"V13: gap and half-open edge do not block input");
        state.BeginDuelFrame(false,true,false,525,300,true);
        check(!state.CapturesPointerPress,"V13: ordinary field click stays available");
        state.BeginDuelFrame(true,true,false,270,10,true);
        check(!state.Enabled && !state.CapturesPointerPress,"V13: F3 hides before hit testing");
    }
}
