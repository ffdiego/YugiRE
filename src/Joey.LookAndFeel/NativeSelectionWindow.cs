namespace Joey.LookAndFeel;

/// <summary>
/// The reusable native list WINDOW contract, not the gameplay selection
/// rules. All state transitions here are reconstructed from 0043A060,
/// 0043ABA0, 0043B0F0, 0043B280 and 0043E280 in joey_pc.exe.
/// The UI renderer and the exact DAT_00A52EE8 cosine table remain separate.
/// </summary>
public sealed class NativeSelectionWindow
{
    public const int VisibleSlots = 5;
    public const int SlotXSpacing = 0x40;
    public const int EntranceStartCounter = 0x100;
    public const int CounterIncrement = 0x20;

    public int TotalCards { get; }
    public int VisibleCount => Math.Min(VisibleSlots, TotalCards);
    public int MaximumScroll => Math.Max(0, TotalCards - VisibleSlots);
    public int SelectedSlot { get; private set; }
    public int ScrollOffset { get; private set; }
    public int SelectedCardIndex => TotalCards == 0 ? -1 : SelectedSlot + ScrollOffset;
    public int OpeningCounter { get; private set; } = EntranceStartCounter;
    public bool EntranceComplete => OpeningCounter == 0;

    public NativeSelectionWindow(int totalCards)
    {
        if (totalCards < 0)
            throw new ArgumentOutOfRangeException(nameof(totalCards));
        TotalCards = totalCards;
    }

    /// <summary>
    /// The native 0043E280 subtracts 0x20 from obj+0x14 each Update,
    /// starting at 0x100 in the constructor. At zero the native object
    /// runs a virtual callback; this class only tracks the counter.
    /// Closing transitions (negative counters) await their call-site map.
    /// </summary>
    public void AdvanceOpening(int updates = 1)
    {
        if (updates < 0)
            throw new ArgumentOutOfRangeException(nameof(updates));
        if (updates == 0 || EntranceComplete) return;
        OpeningCounter = (int)Math.Max(0L, OpeningCounter - (long)CounterIncrement * updates);
    }

    /// <summary>
    /// Native input dispatch in 0043B0F0: event code 4 goes forward,
    /// event code 3 goes backward. The meaning of physical keys remains
    /// outside this model. A selected edge slot scrolls the five-card
    /// window instead of moving onto a sixth visible slot.
    /// </summary>
    public bool MoveNext()
    {
        if (!EntranceComplete || SelectedCardIndex >= TotalCards - 1)
            return false;
        if (SelectedSlot < VisibleSlots - 1)
            SelectedSlot++;
        else if (ScrollOffset < MaximumScroll)
            ScrollOffset++;
        else return false;
        return true;
    }

    public bool MovePrevious()
    {
        if (!EntranceComplete || SelectedCardIndex <= 0)
            return false;
        if (SelectedSlot > 0)
            SelectedSlot--;
        else if (ScrollOffset > 0)
            ScrollOffset--;
        else return false;
        return true;
    }

    public int CardIndexAtSlot(int slot) =>
        slot >= 0 && slot < VisibleCount ? ScrollOffset + slot : -1;

    /// <summary>
    /// Exact 0043ABA0 formula. The meaning and values of the Y resource
    /// fields are supplied by the DAT consumer, not invented here.
    /// Returned coordinates are native card anchors, NOT top-left pixels.
    /// </summary>
    public static CardPoint SlotAnchor(
        int anchorX, int anchorY, int resourceYOffset, int resourceHeight,
        int slot)
    {
        if (slot is < 0 or >= VisibleSlots)
            throw new ArgumentOutOfRangeException(nameof(slot));
        if (resourceHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(resourceHeight));
        return new(
            anchorX + (slot - 2) * SlotXSpacing,
            anchorY + resourceYOffset - resourceHeight / 2);
    }

    /// <summary>
    /// The scale factor used by the Draw's nonzero obj+0x14 branch:
    /// DAT_00A52EE8[((abs(counter)+0x100)/2-0x80)&511] >> 8.
    /// Requires a supplied 512-value native fixed-point cosine table.
    /// A guessed Math.Cos approximation is intentionally not used here.
    /// </summary>
    public static int TransitionScaleFixed8(
        int counter, ReadOnlySpan<int> nativeCosineTable)
    {
        if (counter is < -0x100 or > 0x100)
            throw new ArgumentOutOfRangeException(nameof(counter));
        if (nativeCosineTable.Length != 512)
            throw new ArgumentException("Expected 512 native fixed-point cosine samples",
                                        nameof(nativeCosineTable));
        int index = ((Math.Abs(counter) + 0x100) / 2 - 0x80) & 0x1ff;
        return nativeCosineTable[index] >> 8;
    }

    /// <summary>
    /// The constructor's mode switch; the 1/2 suffix is passed explicitly
    /// because the native perspective/side rule must not be guessed.
    /// </summary>
    public static string ManifestPath(int mode, int sideVariant)
    {
        if (sideVariant is not (1 or 2))
            throw new ArgumentOutOfRangeException(nameof(sideVariant));
        string prefix = mode switch
        {
            1 => "fusionlist",
            3 => "gravelist",
            4 => "excludelist",
            _ => "selectlist"
        };
        return @"j\duel\list\" + prefix + sideVariant + ".txt";
    }
}
