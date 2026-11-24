namespace Joey.LookAndFeel;

public enum NativeEffectCommandFamily { Break, CoinBank, DiceBank }

/// <summary>
/// Known dispatch paths of 005B9470, not gameplay semantics. In particular
/// CoinBank/DiceBank identify DAT constructors, NOT evidence of random results.
/// </summary>
public static class NativeEffectCommandRouting
{
    public static NativeEffectCommandFamily? Family(ushort opcode) => opcode switch
    {
        0x4f => NativeEffectCommandFamily.Break,
        0x8e or 0x8f => NativeEffectCommandFamily.CoinBank,
        0x90 or 0x91 or 0x92 => NativeEffectCommandFamily.DiceBank,
        _ => null
    };

    /// <summary>
    /// 0040A6A0 / 0040A7C0 / 0040A900 form a 16-bit descriptor:
    /// ((0xC000 | region) << 1) | side, truncated to ushort.
    /// Only side and region survive alongside bit 15. The source of the
    /// region differs: low byte of word C for dice, high byte for coin.
    /// This is not necessarily an ordinary monster-zone descriptor.
    /// </summary>
    public static ushort BankAnchorDescriptor(ushort opcode, ushort wordA, ushort wordC)
    {
        int region = Family(opcode) switch
        {
            NativeEffectCommandFamily.CoinBank => (wordC >> 8) & 0x1f,
            NativeEffectCommandFamily.DiceBank => wordC & 0x1f,
            _ => throw new ArgumentOutOfRangeException(nameof(opcode))
        };
        return (ushort)(0x8000 | region << 1 | wordA >> 15);
    }

    /// <summary>00450C30 sorts region 11 separately; all other regions use 0x110.</summary>
    public static int BreakLayer(ushort descriptor) =>
        (descriptor & 0x3e) == 0x16
            ? 0x201 + ((descriptor >> 6) & 0xff) * 0x10 : 0x110;

    /// <summary>
    /// 0040A6A0/0040A7C0/0040A900 start once at low stage zero and wait
    /// for active visual objects. Clearing 0x4000 only when none remain
    /// models their observed handler contract, not a new scheduler.
    /// </summary>
    public static bool StartsBankObject(ushort schedulerWord) => (schedulerWord & 0x7f) == 0;
    public static bool CanReleaseBankCommand(bool anyActiveVisual) => !anyActiveVisual;
}
