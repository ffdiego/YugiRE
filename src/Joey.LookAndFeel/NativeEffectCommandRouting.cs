namespace Joey.LookAndFeel;

public enum NativeEffectCommandFamily { Break, CoinBank, DiceBank, Explosion }

/// <summary>
/// Raw command words, not compressed switch indices of 005B9470.
/// Bundle 29 confirms [005B9AEC + opcode - 1] and [005B9838 + index*4].
/// Only the low 12 bits select the route; the other bits are not part of it.
/// In particular
/// CoinBank/DiceBank identify DAT constructors, NOT evidence of random results.
/// </summary>
public static class NativeEffectCommandRouting
{
    public static NativeEffectCommandFamily? Family(ushort opcode) => (opcode & 0xfff) switch
    {
        0x79 => NativeEffectCommandFamily.Break,
        0x7a or 0x7c => NativeEffectCommandFamily.Explosion,
        0xe0 or 0xe1 => NativeEffectCommandFamily.CoinBank,
        0xe2 or 0xe3 or 0xe4 => NativeEffectCommandFamily.DiceBank,
        _ => null
    };

    /// <summary>
    /// 0040A6A0 / 0040A7C0 / 0040A900 form a 16-bit descriptor:
    /// ((0xC000 | region) << 1) | side, truncated to ushort.
    /// Only side and region survive alongside bit 15. The source of the
    /// region differs: low byte of word C for dice, low byte of word D for coin (A550A6).
    /// This is not necessarily an ordinary monster-zone descriptor.
    /// </summary>
    public static ushort BankAnchorDescriptor(ushort opcode, ushort wordA, uint packedCD)
    {
        int region = Family(opcode) switch
        {
            NativeEffectCommandFamily.CoinBank => (int)((packedCD >> 16) & 0x1f),
            NativeEffectCommandFamily.DiceBank => (int)(packedCD & 0x1f),
            _ => throw new ArgumentOutOfRangeException(nameof(opcode))
        };
        return (ushort)(0x8000 | region << 1 | wordA >> 15);
    }

    // Handler arguments after conversion, not gameplay RNG semantics.
    public static (int Count,uint Bits,int Highlight,ushort Descriptor) CoinArguments(
        ushort wordA,ushort wordB,ushort wordC,ushort wordD)
    {
        int count = (wordA & 0xfff) switch
        {
            0xe0 => 1, 0xe1 => 3, _ => throw new ArgumentOutOfRangeException(nameof(wordA))
        };
        uint bits = count == 1 ? (wordC == 0 ? 1u : 0u) : (~(uint)wordC & 7u);
        return (count,bits,wordB == 0 ? 1 : 0,
            BankAnchorDescriptor(wordA,wordA,(uint)wordC | ((uint)wordD << 16)));
    }

    public static (int Index,ushort Descriptor) DiceArguments(ushort wordA,ushort wordB,ushort wordC,ushort wordD)
    {
        if (Family(wordA) != NativeEffectCommandFamily.DiceBank)
            throw new ArgumentOutOfRangeException(nameof(wordA));
        return (wordB,BankAnchorDescriptor(wordA,wordA,(uint)wordC | ((uint)wordD << 16)));
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
