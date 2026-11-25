namespace Joey.LookAndFeel;

/// <summary>Native phase panel counters at +150 and +14C. The virtual +4
/// at 004812B0 returns busy while either threshold is unmet, not while
/// the phase notice is visible. Value-only model; no scheduler or Core gate.</summary>
public readonly record struct NativePhasePanelState(int FlipCounter, int PhaseCounter)
{
    public static NativePhasePanelState Initial => new(0x200, 0x100);
    public bool IsBusy => FlipCounter < 0x200 || PhaseCounter < 0x80;

    /// <summary>00481230 resets only the counter corresponding to the
    /// requested transition; the previous phase counter survives a side change.</summary>
    public NativePhasePanelState Begin(NativeTurnNoticePlan plan) =>
        plan.ChangesPlayer ? this with { FlipCounter = 0 } : this with { PhaseCounter = 0 };

    /// <summary>00481370. On reaching 512, release panel bank resources
    /// and start the stored phase unless it is -1. Do not advance the
    /// phase counter in that same update.</summary>
    public NativePhasePanelStep Advance(int phase)
    {
        if (phase is < -1 or > 5) throw new ArgumentOutOfRangeException(nameof(phase));
        if (FlipCounter < 0x200)
        {
            int flip = FlipCounter + 8;
            bool release = flip == 0x200;
            bool start = release && phase != -1;
            return new(new(flip, start ? 0 : PhaseCounter), release, start);
        }
        int next = PhaseCounter + (phase == 3 && PhaseCounter >= 0x40 &&
            PhaseCounter < 0x80 ? 4 : 8);
        if (PhaseCounter >= 0x100 && next >= 0x400) next = 0x100;
        return new(this with { PhaseCounter = next }, false, false);
    }
}

public readonly record struct NativePhasePanelStep(NativePhasePanelState State,
    bool ReleasePanelResources, bool StartsPhaseNotice);
