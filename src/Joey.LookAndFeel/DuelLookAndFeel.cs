namespace Joey.LookAndFeel;

/// <summary>
/// Presentation-only timing policy. Joey.Core never depends on these values.
/// Subclass and override timings or voice preferences without changing rules.
/// </summary>
public enum DuelPresentationCue
{
    CardMove,
    FlipReveal,
    FlipEffect,
    Battle,
    LifePoints,
    JoeySpeech
}

public class DuelLookAndFeel
{
    // Nominal loop setting at 005EB618. Fast uses the same counters at twice this rate.
    public const int ClassicUpdatesPerSecond = 30;

    public double UpdatesPerSecond
    {
        get
        {
            double rate = ClassicUpdatesPerSecond * Speed;
            if (!double.IsFinite(rate) || rate <= 0)
                throw new InvalidOperationException("Presentation speed must be positive and finite.");
            return rate;
        }
    }

    public virtual string Name => "Classic";
    public virtual double Speed => 1.0;
    public virtual bool PlayAnimations => true;
    public virtual bool PlayJoeyVoice => true;

    /// <summary>
    /// One native hand-motion step. The presentation clock decides when to call it.
    /// </summary>
    public virtual int MoveCardAxis(int current, int target) =>
        HandPresentation.Approach(current, target);

    /// <summary>Card travel uses the native tick count. Other cue durations are client policy.</summary>
    protected virtual TimeSpan BaseDuration(DuelPresentationCue cue) => cue switch
    {
        DuelPresentationCue.CardMove => TimeSpan.FromSeconds(CardTravel.Updates / (double)ClassicUpdatesPerSecond),
        DuelPresentationCue.FlipReveal => TimeSpan.FromMilliseconds(450),
        DuelPresentationCue.FlipEffect => TimeSpan.FromMilliseconds(500),
        DuelPresentationCue.Battle => TimeSpan.FromMilliseconds(650),
        DuelPresentationCue.LifePoints => TimeSpan.FromMilliseconds(350),
        DuelPresentationCue.JoeySpeech => TimeSpan.FromMilliseconds(1200),
        _ => TimeSpan.Zero
    };

    public virtual TimeSpan Duration(DuelPresentationCue cue)
    {
        if (cue == DuelPresentationCue.JoeySpeech && !PlayJoeyVoice)
            return TimeSpan.Zero;
        if (!PlayAnimations && cue != DuelPresentationCue.JoeySpeech)
            return TimeSpan.Zero;
        if (!double.IsFinite(Speed) || Speed <= 0)
            throw new InvalidOperationException("Presentation speed must be positive and finite.");
        return TimeSpan.FromTicks((long)(BaseDuration(cue).Ticks / Speed));
    }
}

/// <summary>Twice as fast as Classic; the default App profile. Extend to customize individual cues.</summary>
public class FastLookAndFeel : DuelLookAndFeel
{
    public override string Name => "Fast";
    public override double Speed => 2.0;

}

/// <summary>Visual effects without character voice cues (voice playback is future UI integration).</summary>
public class SilentLookAndFeel : DuelLookAndFeel
{
    public override string Name => "Silent";
    public override bool PlayJoeyVoice => false;
}

/// <summary>No animation or speech delays. Rules and state mutations stay identical.</summary>
public class InstantLookAndFeel : DuelLookAndFeel
{
    public override string Name => "Instant";
    public override bool PlayAnimations => false;
    public override bool PlayJoeyVoice => false;
    public override int MoveCardAxis(int current, int target) => target;
}

/// <summary>
/// Non-blocking cue clock, driven by the client's frame delta. It never sleeps,
/// invokes callbacks or mutates duel state.
/// </summary>
public sealed class DuelPresentationClock
{
    public DuelLookAndFeel Style { get; private set; }
    public DuelPresentationCue? ActiveCue { get; private set; }
    public TimeSpan Remaining { get; private set; }
    public bool Busy => Remaining > TimeSpan.Zero;

    public DuelPresentationClock(DuelLookAndFeel style) =>
        Style = style ?? throw new ArgumentNullException(nameof(style));

    public void SetStyle(DuelLookAndFeel style)
    {
        Style = style ?? throw new ArgumentNullException(nameof(style));
        Clear();
    }

    public void Show(DuelPresentationCue cue)
    {
        Remaining = Style.Duration(cue);
        ActiveCue = Busy ? cue : null;
    }

    public void Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (!Busy) return;
        Remaining = elapsed >= Remaining ? TimeSpan.Zero : Remaining - elapsed;
        if (!Busy) ActiveCue = null;
    }

    public void Clear()
    {
        Remaining = TimeSpan.Zero;
        ActiveCue = null;
    }
}
