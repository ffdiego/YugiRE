namespace Joey.LookAndFeel;

public readonly record struct NativeSelectionTransitionFrame(
    CardPoint Center, int Width, int Height);

/// <summary>
/// The reusable native list WINDOW contract, not the gameplay selection
/// rules. All state transitions here are reconstructed from 0043A060,
/// 0043ABA0, 0043B0F0, 0043B280, 0043E0A0 and 0043E280 in joey_pc.exe.
/// The UI renderer and the exact DAT_00A52EE8 cosine table remain separate.
/// </summary>
public sealed class NativeSelectionWindow
{
    // 0043A060: DAT_005DB570 = 525 (bundle bytes 0D 02 00 00), Y = 300.
    public static readonly CardPoint TargetCenter = new(525, 300);
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
    public int ClosingCounter { get; private set; }
    public bool IsClosing { get; private set; }
    public bool IsClosed { get; private set; }
    public int TransitionCounter => IsClosing ? ClosingCounter : OpeningCounter;
    public bool EntranceComplete => OpeningCounter == 0 && !IsClosing && !IsClosed;

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
    /// A separate native handler 0043E0A0 begins closing at -1.
    /// </summary>
    public void AdvanceOpening(int updates = 1)
    {
        if (updates < 0)
            throw new ArgumentOutOfRangeException(nameof(updates));
        if (updates == 0 || OpeningCounter == 0 || IsClosing || IsClosed) return;
        OpeningCounter = (int)Math.Max(0L, OpeningCounter - (long)CounterIncrement * updates);
    }

    /// <summary>
    /// Native 0043E0A0 starts the closing branch by setting obj+0x14=-1.
    /// Its exact triggering condition is a caller/virtual callback contract
    /// and is deliberately left to the UI integration, not inferred here.
    /// </summary>
    public bool BeginClosing()
    {
        if (!EntranceComplete) return false;
        IsClosing = true;
        ClosingCounter = -1;
        return true;
    }

    /// <summary>
    /// Native 0043E280 decrements -1 by 0x20 each Update and disables the
    /// object when the counter falls below -0xFF. Thus the last visible
    /// closing counter is -225; update eight reaches -257 and deactivates.
    /// No draw is requested for the deactivated object.
    /// </summary>
    public void AdvanceClosing(int updates = 1)
    {
        if (updates < 0)
            throw new ArgumentOutOfRangeException(nameof(updates));
        if (!IsClosing || IsClosed || updates == 0) return;
        long counter = (long)ClosingCounter - (long)CounterIncrement * updates;
        ClosingCounter = (int)Math.Max(-257L, counter);
        IsClosed = ClosingCounter < -0xFF;
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

    /// <summary>Client mouse adapter. It changes focus, not scroll or rules.</summary>
    public bool FocusVisibleSlot(int slot)
    {
        if (!EntranceComplete || CardIndexAtSlot(slot) < 0) return false;
        SelectedSlot = slot;
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
    /// Exact Ghidra disassembly of 0043B280 at 0043B2E6..0043B31B:
    /// positive counter uses (256-counter); negative uses (256+counter).
    /// Signed division by two is rounded toward zero (CDQ/SUB/SAR), then
    /// the result is offset by -128 and masked to the 512-sample table.
    /// This is NOT abs(counter)/2; that would reverse the lookup direction
    /// and shift odd closing counters.
    /// </summary>
    public static int TransitionCosineIndex(int counter)
    {
        if (counter is < -0x100 or > 0x100)
            throw new ArgumentOutOfRangeException(nameof(counter));
        return ((0x100 - Math.Abs(counter)) / 2 - 0x80) & 0x1ff;
    }

    /// <summary>
    /// The 0043B280 Draw samples the original DAT_00A52EE8 signed
    /// 16.16 cosine table and shifts its result by eight. Never fabricate
    /// the table at runtime and present it as native.
    /// </summary>
    public static int TransitionScaleFixed8(
        int counter, ReadOnlySpan<int> nativeCosineTable)
    {
        if (nativeCosineTable.Length != 512)
            throw new ArgumentException("Expected 512 native fixed-point cosine samples",
                                        nameof(nativeCosineTable));
        return nativeCosineTable[TransitionCosineIndex(counter)] >> 8;
    }

    /// <summary>
    /// The native Draw's transition branch interpolates both center
    /// coordinates and bitmap dimensions using the same cosine factor.
    /// The start anchor is the pair returned by 0045C140 (sprite- and
    /// perspective-dependent); the target is object+1Ch/+20h, and source
    /// dimensions are object+2Ch/+30h. All four divisions truncate toward
    /// zero, as implemented by CDQ/AND 0xff/SAR in the x86 instructions.
    /// This returns CENTER and SIZE, not a guessed top-left Draw rectangle.
    /// </summary>
    public static NativeSelectionTransitionFrame TransitionFrame(
        int counter, CardPoint startCenter, CardPoint targetCenter,
        int bitmapWidth, int bitmapHeight, ReadOnlySpan<int> nativeCosineTable)
    {
        if (bitmapWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(bitmapWidth));
        if (bitmapHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(bitmapHeight));
        int scale = TransitionScaleFixed8(counter, nativeCosineTable);
        // In the documented original range, intermediate multiplication
        // fits 32 bits. C# integer division matches x86 signed truncation.
        return new(
            new CardPoint(
                startCenter.X + (int)(((long)targetCenter.X - startCenter.X) * scale / 256),
                startCenter.Y + (int)(((long)targetCenter.Y - startCenter.Y) * scale / 256)),
            (int)((long)bitmapWidth * scale / 256),
            (int)((long)bitmapHeight * scale / 256));
    }

    /// <summary>
    /// The nonzero Draw branch in 0043B280 (assembly 0043B2A9..0043B2DF)
    /// chooses descriptor 0xC/0xD/0xE/0xF for modes 1/2/3/4.
    /// It calls 0045C140(side, descriptor, 0) and gets a POSITION PAIR
    /// (EAX=X, EDX=Y) for the start of the opening/closing transformation.
    /// The value is a descriptor, not a DAT texture index or card slot.
    /// Other modes use -1, whose meaning remains unconfirmed.
    /// </summary>
    public static int TransitionOriginDescriptor(int mode) => mode switch
    {
        1 => 0xC,
        2 => 0xD,
        3 => 0xE,
        4 => 0xF,
        _ => -1
    };

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
