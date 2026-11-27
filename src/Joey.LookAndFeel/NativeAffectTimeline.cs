namespace Joey.LookAndFeel;

/// <summary>
/// 0040E010 / 0040E1F0: periodic contextual highlighting, driven by
/// visual-object focus identity, not by resource ID or a rules command.
/// The caller must supply already resolved, visibility-safe facts.
/// </summary>
public sealed class NativeAffectTimeline
{
    public const int Layer = 0x101;
    public int Counter { get; private set; }
    public long FocusIdentity { get; private set; }
    public bool HasFocus => FocusIdentity != 0;

    public void AdvanceNativeUpdate(long focusIdentity)
    {
        Counter = (Counter + 8) & 255;
        if (focusIdentity != 0 && FocusIdentity != focusIdentity)
            Counter = 0;
        FocusIdentity = focusIdentity;
    }

    public int Intensity(ReadOnlySpan<int> nativeCosine)
    {
        NativeFusionMath.Validate(nativeCosine);
        // 0040E1F0: SAR 8 before multiply; signed /8 truncates toward zero.
        // Do not clamp to a client alpha until the compositor is recovered.
        return ((NativeFusionMath.C(nativeCosine, Counter - 128) >> 8) * 7) / 8;
    }

    // 0040E435..0040E47F and 0040E852..0040E88E.
    // NativeRace is the effective race from 0056F6D0, not printed race.
    // The reverse branch tests bit 5 only, unlike the monster-focus branch.
    public static bool DragonJarHighlight(int resourceId,bool jarRevealed,uint jarFlags,
        bool monsterRevealed,int nativeRace,bool focusIsJar) =>
        resourceId==0x148 && jarRevealed && monsterRevealed && nativeRace==1 &&
        (jarFlags & (focusIsJar ? 0x20u : 0x22u))==0;

    // 0040E4B4..0040E4FE. The count comes from 005699C0.
    // This draws over the focused monster, not the source TER.
    public static bool RestrictSelfHighlight(int focusedResourceId,int nativeCount) =>
        nativeCount > (focusedResourceId==0x536 ? 1 : 0);

    // 0040E5E4..0040E627: in the stored-link direction, it is the
    // linked card's resource ID that chooses the monster-source icon.
    // Do not pretend to know the outbound jump-table kind mapping.
    public static int MonsterFocusLinkedCardSelector(int linkedResourceId) =>
        linkedResourceId switch { 0x52=>5,0x2d9 or 0x534=>4,_=>2 };

    /// <summary>
    /// Unambiguous inbound link-kind branch of 0040E260. The caller has
    /// established occupancy and exact matching link target. Null means
    /// this branch does not yield a selector; do not invent another.
    /// Not a rule evaluator, and not an asset index mapping.
    /// </summary>
    public static int? IncomingLinkSelector(int focusedZone, int focusedResourceId, int linkKind)
    {
        if (focusedZone is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(focusedZone));
        if (linkKind is not (1 or 2 or 4 or 5 or 6)) return null;
        if (focusedZone >= 5) return linkKind is 1 or 4 or 5 ? 1 : 2;
        return MonsterFocusLinkedCardSelector(focusedResourceId);
    }
}
