using Joey.LookAndFeel;
using Raylib_cs;

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
        if (!duel.GameOver || (duel.ExodiaWinner is not null && ExodiaOpeningQueued)) return;

        bool liveAlphaResult = alphaCpu is not null && labSession is null;
        if (liveAlphaResult && (MotionBusy || queuedDuel?.IsBusy == true)) return;

        const int fullBanner = NativeDuelEndFrame.MaxCounter / NativeDuelEndFrame.UpdateIncrement;
        bool bannerWasReady = duelResultUpdates >= fullBanner;
        // Native 00451E40 advances by four while 00451E60 draws.
        // We hold the fully visible result until the screen is left:
        // the native decompilation is ambiguous beyond counter 0x200.
        if (!presentation.Style.PlayAnimations)
            duelResultUpdates = fullBanner;
        else if (!bannerWasReady)
            duelResultUpdates = Math.Min(fullBanner,
                duelResultUpdates + duelResultClock.Advance(elapsed, presentation.Style));

        var frame = NativeDuelEndFrame.FromUpdate(duelResultUpdates, effectCosine);
        if (frame.Opacity == 0) return;
        string resource = duel.IsDraw ? DatGraphics.DuelDrawEnglish
            : duel.Winner == ViewPlayer ? DatGraphics.DuelWinEnglish
            : DatGraphics.DuelLoseEnglish;
        DrawEffectStamp(resource, new CardPoint(400, 300), 0, 0x10000, frame.Opacity);

        // Alpha replaces the replay decision with acknowledgement of the result.
        if (liveAlphaResult && bannerWasReady && !ConsoleCapturesInput && !visualReviewPaused &&
            (Raylib.IsKeyPressed(KeyboardKey.Enter) || PointerPressed(MouseButton.Left)))
            BeginLiveResultBust(duel.IsDraw ? 3 : duel.Winner == ViewPlayer ? 1 : 2);
    }
}
