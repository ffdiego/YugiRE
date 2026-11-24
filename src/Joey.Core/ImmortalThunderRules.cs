namespace Joey.Core;

/// <summary>
/// P065 — The Immortal of Thunder (resource ID 461, DAT index 164).
/// Bounded historical-text policy; native availability, preparation and
/// primary callbacks are catalogued but not decompiled in our SQLite.
/// </summary>
public static class ImmortalThunderRules
{
    public const ushort ResourceId = 461;  // 0x1CD
    public const int FlipLifeGain = 3000;
    public const int GraveyardLifeLoss = 5000;

    /// <summary>
    /// The historical effect text gates its second clause on this card
    /// having been flipped. Never infer prior flip from being currently
    /// face-up, which is also true after a normal/special summon.
    /// </summary>
    public static bool LosesLifeOnFieldToGraveyard(
        ushort resourceId, bool occurrenceWasFlipped) =>
        resourceId == ResourceId && occurrenceWasFlipped;
}
