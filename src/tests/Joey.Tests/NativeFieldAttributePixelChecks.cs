using Joey.LookAndFeel;

internal static class NativeFieldAttributePixelChecks
{
    public static void Run(Action<bool, string> check)
    {
        // RGB565 corner, a distinct RGB888 value with the same packed key,
        // white, pure green (not key), and a color which becomes black at 160.
        byte[] input = [141,179,40,255, 136,176,47,255, 255,255,255,255,
            0,255,0,255, 8,4,8,255];
        check(NativeFieldAttributePixels.Pack(141,179,40) == 0x8D85,
            "V05 observed atlas key");
        var full = input.ToArray();
        NativeFieldAttributePixels.Prepare(full, 256);
        check(full.SequenceEqual(new byte[] {0,0,0,0, 0,0,0,0, 255,255,255,255,
            0,255,0,255, 8,4,8,255}), "V05 packed key before opaque copy");
        var dim = input.ToArray();
        NativeFieldAttributePixels.Prepare(dim, 160);
        check(dim.SequenceEqual(new byte[] {0,0,0,0, 0,0,0,0, 165,170,165,255,
            0,170,0,255, 0,0,0,255}), "V05 independent packed modulation and binary alpha");
        // Every packed RGB565 color round-trips through the expanded bytes.
        byte[] atlas = new byte[4 * (65536 + 1)];
        atlas[0]=141; atlas[1]=179; atlas[2]=40; atlas[3]=255;
        for (int i=0; i<65536; i++)
        {
            int offset=4*(i+1);
            atlas[offset]=(byte)((i>>11)<<3);
            atlas[offset+1]=(byte)(((i>>5)&63)<<2);
            atlas[offset+2]=(byte)((i&31)<<3);
            atlas[offset+3]=255;
        }
        foreach (int intensity in new[] {160,256})
        {
            var prepared=atlas.ToArray();
            NativeFieldAttributePixels.Prepare(prepared,intensity);
            bool all=true;
            for(int i=0;i<65536;i++)
            {
                int offset=4*(i+1);
                ushort expected=intensity==160
                    ? (ushort)((((i>>11)*10/15)<<11)|(((((i>>5)&63)*10)/15)<<5)|((i&31)*10/15))
                    : (ushort)i;
                all &= i==NativeFieldAttributePixels.ObservedKey
                    ? prepared[offset+3]==0
                    : prepared[offset+3]==255 && NativeFieldAttributePixels.Pack(
                        prepared[offset],prepared[offset+1],prepared[offset+2])==expected;
            }
            check(all, $"V05 exhaustive packed atlas projection intensity {intensity}");
        }
        bool rejected=false;
        try { NativeFieldAttributePixels.Prepare(new byte[] {0,255,0,255},160); }
        catch (InvalidDataException) { rejected=true; }
        check(rejected, "V05 unexpected atlas key is not silently guessed");
    }
}
