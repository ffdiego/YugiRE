using Joey.LookAndFeel;

static partial class Game
{
    static readonly PresentationTickClock duelResultClock = new();
    static int duelResultUpdates;

    static void ResetDuelResultNotice()
    {
        duelResultUpdates = 0;
        duelResultClock.Reset();
    }

    static void DrawDuelResultNotice(TimeSpan elapsed)
    {
        if (!duel.GameOver) return;

        // Native 00451E40 advances by four while 00451E60 draws.
        // We hold the fully visible result until the screen is left:
        // the native decompilation is ambiguous beyond counter 0x200.
        if (!presentation.Style.PlayAnimations)
            duelResultUpdates = NativeDuelEndFrame.MaxCounter /
                                NativeDuelEndFrame.UpdateIncrement;
        else if (duelResultUpdates * NativeDuelEndFrame.UpdateIncrement <
                 NativeDuelEndFrame.MaxCounter)
            duelResultUpdates = Math.Min(NativeDuelEndFrame.MaxCounter /
                NativeDuelEndFrame.UpdateIncrement,
                duelResultUpdates + duelResultClock.Advance(elapsed, presentation.Style));

        var frame = NativeDuelEndFrame.FromUpdate(duelResultUpdates, effectCosine);
        if (frame.Opacity == 0) return;
        string resource = duel.IsDraw ? DatGraphics.DuelDrawEnglish
            : duel.Winner == ViewPlayer ? DatGraphics.DuelWinEnglish
            : DatGraphics.DuelLoseEnglish;
        DrawEffectStamp(resource, new CardPoint(400, 300), 0, 0x10000, frame.Opacity);
    }
}
