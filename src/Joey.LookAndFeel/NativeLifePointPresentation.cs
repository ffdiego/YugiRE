namespace Joey.LookAndFeel;

/// <summary>
/// Presentation-only LP display from the native 0047C4F0 (event setup)
/// and 0047C560 (Update) contracts. Core LP and battle results remain immediate.
/// Native 0047C310 initializes visible LP to 8000 and ramp to 0x100.
/// </summary>
public sealed class NativeLifePointPresentation
{
    public const int MaximumDisplayed = 0xFFFF;
    public const int RampIncrement = 8;
    public const int RampEnd = 0x100;
    public const int RampUpdates = RampEnd / RampIncrement;

    public int Displayed { get; private set; }
    public int PendingDelta { get; private set; }
    public int RampCounter { get; private set; } = RampEnd;
    public int FlashCounter { get; private set; }
    public int PulseCounter { get; private set; }
    public int AnchorSlot { get; private set; } = -1;
    public bool HoldConsumption { get; private set; }
    public bool Animating => PendingDelta != 0 || RampCounter < RampEnd || FlashCounter > 0;

    public NativeLifePointPresentation(int currentLife) => Reset(currentLife);

    public void Reset(int currentLife)
    {
        Displayed = ClampToNativeDisplay(currentLife);
        PendingDelta = 0;
        RampCounter = RampEnd;
        FlashCounter = 0;
        PulseCounter = 0;
        AnchorSlot = -1;
        HoldConsumption = false;
    }

    public static int ClampToNativeDisplay(int value) =>
        Math.Clamp(value, 0, MaximumDisplayed);

    /// <summary>
    /// 0047C4F0 receives a signed LP change, sets the remaining delta and
    /// resets the 0x34 animation ramp. If another logical change arrives
    /// mid-animation, retarget from the currently displayed value rather
    /// than letting old events overwrite the latest Core state.
    /// </summary>
    public void SetLogicalLife(int currentLife, bool animate = true, int anchorSlot = -1)
    {
        if (anchorSlot is < -1 or >= NativeDuelLayout.MonsterSlots)
            throw new ArgumentOutOfRangeException(nameof(anchorSlot));
        int target = ClampToNativeDisplay(currentLife);
        if (!animate)
        {
            Reset(target);
            return;
        }

        // This goal includes the delta not yet shown on screen.
        if ((long)Displayed + PendingDelta == target) return;

        PendingDelta = target - Displayed;
        RampCounter = 0;
        AnchorSlot = anchorSlot;
        HoldConsumption = false;
    }

    /// <summary>
    /// 0047C4F0: if +30 is set, this invocation only clears it
    /// (0047C540); it does not replace delta, slot or ramp or request
    /// another sound. Otherwise store the new request. Unlike the
    /// legacy SetLogicalLife adapter, this does not retarget to a
    /// logical LP total. Do not mix both input paths for one effect.
    /// </summary>
    public uint? RequestNativeChange(NativeLifeDeltaRequest request)
    {
        if (HoldConsumption)
        {
            HoldConsumption = false;
            return null;
        }
        PendingDelta = request.Delta;
        AnchorSlot = request.AnchorSlot;
        HoldConsumption = request.HoldConsumption;
        RampCounter = 0;
        return request.Delta < 0 ? 0x10000009u : 0x1000001fu;
    }

    // 0047C550 clears remaining delta and flag, not displayed LP,
    // the current ramp, anchor or flash. No rule mutation.
    public void CancelNativeChange()
    {
        PendingDelta = 0;
        HoldConsumption = false;
    }

    /// <summary>
    /// One native Update: ramp += 8 until 256, then consume +/-100,
    /// +/-10 or +/-1 LP from the signed pending delta per Update.
    /// 0047C560 also increments a 7-bit pulse and applies a transient
    /// flash counter when a number changes. The latter are retained as
    /// presentation parameters; the native digit sprites are not ported.
    /// </summary>
    public void AdvanceNativeUpdate()
    {
        PulseCounter = (PulseCounter + 1 + (FlashCounter > 0 ? 1 : 0)) & 0x7F;

        if (RampCounter < RampEnd)
        {
            RampCounter += RampIncrement;
        }
        else if (!HoldConsumption && PendingDelta != 0)
        {
            long magnitude = Math.Abs((long)PendingDelta);
            int step = magnitude >= 100 ? 100 : magnitude >= 10 ? 10 : 1;
            int signedStep = PendingDelta < 0 ? -step : step;
            Displayed = Math.Clamp(Displayed + signedStep, 0, MaximumDisplayed);
            PendingDelta -= signedStep;
            FlashCounter = 31;
        }

        if (FlashCounter > 0) FlashCounter--;
    }
}
