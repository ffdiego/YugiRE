namespace Joey.Core;

/// <summary>
/// Exact producer-to-queue representation for the verified DEFAULT branch
/// of Joey's FUN_005B9170, for opcodes 0x37, 0x61 and 0x86 only.
///
/// Ghidra: four 16-bit words are copied to the eight-byte command record.
/// This does not decode/replay the entire command protocol, nor apply the
/// special transformations used by other opcodes (e.g. 0x64 / 0x65).
/// </summary>
public readonly record struct NativeDefaultQueuePacket(
    ushort OpcodeWord, ushort Parameter1, ushort Parameter2, ushort Parameter3)
{
    public int Opcode => OpcodeWord & 0x7FFF;
    public bool SideBit15 => (OpcodeWord & 0x8000) != 0;

    /// <summary>
    /// Decode a call to FUN_005B9170 only when its switch branch is known
    /// to store the original arguments as four 16-bit words.
    /// Retain the original uint arguments in the trace separately.
    /// </summary>
    public static bool TryFromProducerArguments(
        uint opcodeRaw, uint argument1, uint argument2, uint argument3,
        out NativeDefaultQueuePacket packet)
    {
        int opcode = (int)(opcodeRaw & 0x7FFF);
        if (opcode is not (0x37 or 0x61 or 0x86))
        {
            packet = default;
            return false;
        }

        packet = new NativeDefaultQueuePacket(
            unchecked((ushort)opcodeRaw),
            unchecked((ushort)argument1),
            unchecked((ushort)argument2),
            unchecked((ushort)argument3));
        return true;
    }
}
