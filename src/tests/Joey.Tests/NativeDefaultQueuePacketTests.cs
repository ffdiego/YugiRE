using Joey.Core;

internal static class NativeDefaultQueuePacketTests
{
    public static void Run(Action<bool, string> check)
    {
        // The raw arguments come from global duel 0 in two independent
        // Windows captures, both using master seed 12345.
        bool a37 = NativeDefaultQueuePacket.TryFromProducerArguments(
            0x06350037u, 0, 0, 0, out var p37a);
        bool b37 = NativeDefaultQueuePacket.TryFromProducerArguments(
            0x06470037u, 0, 0, 0, out var p37b);
        check(a37 && b37 && p37a == p37b &&
              p37a.Opcode == 0x37 && !p37a.SideBit15,
              "0x37 native command ignores stale EAX upper word");

        bool a86 = NativeDefaultQueuePacket.TryFromProducerArguments(
            0x86u, 0x06350445u, 0x0101u, 12u, out var p86a);
        bool b86 = NativeDefaultQueuePacket.TryFromProducerArguments(
            0x86u, 0x06470445u, 0x0101u, 12u, out var p86b);
        check(a86 && b86 && p86a == p86b &&
              p86a.Parameter1 == 0x0445 && p86a.Parameter2 == 0x0101,
              "0x86 native queue stores low 16 bits of all args");

        bool a61 = NativeDefaultQueuePacket.TryFromProducerArguments(
            0x05370061u, 5u, 1u, 0u, out var p61a);
        bool b61 = NativeDefaultQueuePacket.TryFromProducerArguments(
            0x05360061u, 5u, 1u, 0u, out var p61b);
        check(a61 && b61 && p61a == p61b,
              "0x61 native queue ignores stale EDX upper word");

        bool side = NativeDefaultQueuePacket.TryFromProducerArguments(
            0x80000086u, 1093u, 257u, 12u, out var p86Side);
        check(side && p86Side.SideBit15 && p86Side.Opcode == 0x86,
              "Native command word preserves bit15 while masking opcode");

        check(!NativeDefaultQueuePacket.TryFromProducerArguments(
                  0x00CEu, 100u, 0, 0, out _),
              "Native queue decoder refuses opcodes requiring separate reconstruction");
    }
}
