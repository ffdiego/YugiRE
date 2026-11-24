namespace Joey.LookAndFeel;

/// <summary>00403900 indexes a native damage reaction table, NOT the
/// reactionXX manifest directly. Preserve the missing indirection.</summary>
public readonly record struct NativeJoeyDamageReaction(int TableIndex, uint SoundRequest);

public static class NativeJoeyReaction
{
    // The side argument is relative to the native (~00A54E5C)&1 gate.
    // No assumption that this always means CPU in another duel mode.
    public static NativeJoeyDamageReaction? Damage(bool matchesNativeSide,
        int lifeBefore, int signedDelta)
    {
        if (lifeBefore is < 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(lifeBefore));
        if (!matchesNativeSide || signedDelta >= 0 || lifeBefore == 0)
            return null;
        long loss = -(long)signedDelta;
        int index;
        if (loss >= lifeBefore) index = 5;
        else if (lifeBefore < 4001)
            // Preserve the seemingly reversed 1000/2000 cases.
            index = loss <= 2000 ? (loss <= 1000 ? 3 : 2) : 4;
        else index = (int)Math.Clamp((loss-1)/500-1,0,3);
        return new(index,0x10000045u+(uint)index);
    }

    // 00402E00: zero sound => no object, zero reaction => sound only.
    public static NativeJoeyReactionKind Kind(int manifestNumber,uint soundRequest)
    {
        if (manifestNumber is < 0 or > 13)
            throw new ArgumentOutOfRangeException(nameof(manifestNumber));
        return soundRequest == 0 ? NativeJoeyReactionKind.None :
            manifestNumber == 0 ? NativeJoeyReactionKind.SoundOnly :
            NativeJoeyReactionKind.BustUp;
    }

    /// <summary>00416B80 divides the raw 005C04E0 duration by 33.
    /// The native signed -1 sentinel, zero and sub-33 values use 32.
    /// This is a duration parameter, not a proven total scene lifetime.</summary>
    public static int DurationParameter(int nativeDuration)
    {
        int result = nativeDuration / 33;
        return result < 1 ? 32 : result;
    }

    /// <summary>005C0F00 resolves family 1 using the low twelve bits
    /// against the native sound descriptor count. Not a DAT entry index.</summary>
    public static int? SoundDescriptorIndex(uint request,int descriptorCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(descriptorCount);
        int index = (int)(request & 0xfff);
        return (request & 0xf0000000u)==0x10000000u && index<descriptorCount
            ? index : null;
    }
}

public enum NativeJoeyReactionKind { None, SoundOnly, BustUp }
