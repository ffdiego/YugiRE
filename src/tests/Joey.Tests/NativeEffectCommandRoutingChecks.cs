using Joey.LookAndFeel;

internal static class NativeEffectCommandRoutingChecks
{
    public static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        check(NativeEffectCommandRouting.Family(0x4f) == NativeEffectCommandFamily.Break &&
              NativeEffectCommandRouting.Family(0x50) is null, "V22: break not explosion");
        foreach (ushort opcode in new ushort[] {0x8e,0x8f,0x90,0x91,0x92})
        {
            bool coin = opcode < 0x90;
            check(NativeEffectCommandRouting.Family(opcode) == (coin
                ? NativeEffectCommandFamily.CoinBank : NativeEffectCommandFamily.DiceBank),
                "V20/V21: distinct bank routes");
            for (int side = 0; side <= 1; side++)
                for (int region = 0; region < 32; region++)
                {
                    ushort word = (ushort)(coin ? (region << 8) | 0xe01f : 0x1fe0 | region);
                    check(NativeEffectCommandRouting.BankAnchorDescriptor(opcode,
                        (ushort)(side << 15 | 0x1234), word) == (0x8000 | region << 1 | side),
                        "V20/V21: packed region and side");
                }
        }
        reject(() => NativeEffectCommandRouting.BankAnchorDescriptor(0x4f,0,0),
            "V22: break descriptor comes from its own producer");
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
