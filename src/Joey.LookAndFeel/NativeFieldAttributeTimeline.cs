namespace Joey.LookAndFeel;

/// <summary>Numbers displayed beside a field monster, not its logical Core attributes.</summary>
public readonly record struct NativeFieldDisplayedAttributes(int Attack, int Defense);

/// <summary>
/// One per occupied monster slot. FUN_00418B10 seeds +0x4c/+0x50 with the
/// base CARD_Prop ATK/DEF whenever a card is assigned to the native object.
/// FUN_004194D0 then eases them toward the current calculated ATK/DEF inside
/// Draw. The raylib window renders at 60 FPS even in Classic (30 Hz); use
/// whole presentation updates supplied by PresentationTickClock to avoid
/// doubling the easing speed or advancing during F6 pause.
///
/// Caller owns visibility and should Reset when the slot is vacated, the
/// card is moving, or an opponent's hidden attributes are not displayed.
/// Reference identity distinguishes replacement by another card with the
/// same ATK/DEF (and allows a summoned card with a buff to ease from base).
/// </summary>
public sealed class NativeFieldAttributeTimeline
{
    private object? cardIdentity;
    private int displayedAttack;
    private int displayedDefense;

    public bool HasCard => cardIdentity is not null;
    public NativeFieldDisplayedAttributes Displayed => new(displayedAttack, displayedDefense);

    public NativeFieldDisplayedAttributes Sample(
        object identity, int baseAttack, int baseDefense,
        int targetAttack, int targetDefense, int presentationUpdates, bool animate)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentOutOfRangeException.ThrowIfNegative(baseAttack);
        ArgumentOutOfRangeException.ThrowIfNegative(baseDefense);
        ArgumentOutOfRangeException.ThrowIfNegative(targetAttack);
        ArgumentOutOfRangeException.ThrowIfNegative(targetDefense);
        ArgumentOutOfRangeException.ThrowIfNegative(presentationUpdates);

        if (!ReferenceEquals(cardIdentity, identity))
        {
            cardIdentity = identity;
            displayedAttack = baseAttack;
            displayedDefense = baseDefense;
        }

        if (!animate)
        {
            // Instant presentation is a deliberately non-native accessibility mode.
            displayedAttack = targetAttack;
            displayedDefense = targetDefense;
        }
        else
        {
            for (int update = 0; update < presentationUpdates; update++)
            {
                displayedAttack = NativeFieldAttributeFrame.SmoothDisplayed(
                    displayedAttack, targetAttack);
                displayedDefense = NativeFieldAttributeFrame.SmoothDisplayed(
                    displayedDefense, targetDefense);
            }
        }
        return Displayed;
    }

    public void Reset()
    {
        cardIdentity = null;
        displayedAttack = displayedDefense = 0;
    }
}
