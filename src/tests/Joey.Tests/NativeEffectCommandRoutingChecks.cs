using Joey.LookAndFeel;

internal static class NativeEffectCommandRoutingChecks
{
    public static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        // Bundle 29's byte table + dword table + dispatcher call Xrefs.
        // Exhaust the low-12-bit domain, including the unhandled values.
        for (int raw = 0; raw < 0x1000; raw++)
        {
            NativeEffectCommandFamily? expected = raw switch
            {
                0x79 => NativeEffectCommandFamily.Break,
                0x7a or 0x7c => NativeEffectCommandFamily.Explosion,
                0xe0 or 0xe1 => NativeEffectCommandFamily.CoinBank,
                0xe2 or 0xe3 or 0xe4 => NativeEffectCommandFamily.DiceBank,
                _ => null
            };
            bool matches = true;
            for (int upper = 0; upper < 16; upper++)
                matches &= NativeEffectCommandRouting.Family((ushort)(raw | upper << 12)) == expected;
            check(matches, $"V20–V22: raw opcode 0x{raw:X3}, all upper bits");
        }
        foreach (ushort opcode in new ushort[] {0xe0,0xe1,0xe2,0xe3,0xe4})
        {
            bool coin = opcode < 0xe2;
            check(NativeEffectCommandRouting.Family(opcode) == (coin
                ? NativeEffectCommandFamily.CoinBank : NativeEffectCommandFamily.DiceBank),
                "V20/V21: distinct bank routes");
            for (int side = 0; side <= 1; side++)
                for (int region = 0; region < 32; region++)
                {
                    uint packed = coin ? (uint)(0xa5001f03 | (region << 16)) : (uint)(0xa51b1fe0 | region);
                    check(NativeEffectCommandRouting.BankAnchorDescriptor(opcode,
                        (ushort)(side << 15 | 0x1234), packed) == (0x8000 | region << 1 | side),
                        "V20/V21: packed region and side");
                }
        }
        bool rejected = false;
        try { _ = NativeEffectCommandRouting.BankAnchorDescriptor(0x79, 0, 0); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        check(rejected, "V22: break descriptor comes from its own producer");
        foreach (ushort notBank in new ushort[] {0x4f,0x50,0x52,0x79,0x7a,0x7c,0x8e,0x8f,0x90,0x91,0x92})
        {
            bool refused = false;
            try { _ = NativeEffectCommandRouting.BankAnchorDescriptor(notBank, 0, 0); }
            catch (ArgumentOutOfRangeException) { refused = true; }
            check(refused, $"V20–V22: non-bank 0x{notBank:X2} rejected");
        }
        for (int slot=0;slot<256;slot++)
            check(NativeEffectCommandRouting.BreakLayer((ushort)(0xc016 | slot<<6)) ==
                  0x201+slot*0x10, "V22: region 11 layer uses full slot byte");
        check(NativeEffectCommandRouting.BreakLayer(0x8000) == 0x110 &&
              NativeEffectCommandRouting.BreakLayer(0x8018) == 0x110, "V22: ordinary break layer");
        check(NativeEffectCommandRouting.StartsBankObject(0x4000) &&
              !NativeEffectCommandRouting.StartsBankObject(0x4001) &&
              NativeEffectCommandRouting.CanReleaseBankCommand(false) &&
              !NativeEffectCommandRouting.CanReleaseBankCommand(true), "V20/V21: native wait gate");
    }
}
