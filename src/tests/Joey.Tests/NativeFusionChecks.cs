using System.Buffers.Binary;
using System.Security.Cryptography;
using Joey.LookAndFeel;
using System.Text.Json;

internal static class NativeFusionChecks
{
    public static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        // SHA-256 of 512 little-endian Int32 values ordered by index
        // in the private v3 runtime capture (00A52EE8).
        int[] generated = NativeCosineCapture.GenerateTable();
        byte[] packed = new byte[generated.Length * sizeof(int)];
        for (int i = 0; i < generated.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(packed.AsSpan(i * sizeof(int)), generated[i]);
        check(generated.Length == 512 && Convert.ToHexStringLower(SHA256.HashData(packed)) ==
              "c3c61d7556976734a882e61e82fe27a7e76fb897a9b7e2a1908ed07d21f3c1e9",
            "V01: all generated samples match the v3 runtime capture");

        // Artificial asymmetric table to test indices and signed arithmetic,
        // never a native runtime capture.
        int[] c = new int[512];
        c[384] = 32768; c[0] = 65536; c[128] = -32768;
        string capture = JsonSerializer.Serialize(new {
            schemaVersion = 1, exeSha256 = NativeCosineCapture.ExecutableSha256,
            tableVa = "00A52EE8", initializedVa = "00A536E8", initialized = 1, values = c
        });
        check(NativeCosineCapture.Parse(capture).SequenceEqual(c),
            "V01: runtime table preserves all signed samples without regeneration");
        reject(() => NativeCosineCapture.Parse(capture.Replace("00A52EE8", "00A52EE9")),
            "V01: wrong table address rejected");
        reject(() => NativeCosineCapture.Parse(capture.Replace(NativeCosineCapture.ExecutableSha256, "wrong")),
            "V01: wrong executable fingerprint rejected");
        reject(() => NativeCosineCapture.Parse("{}"), "V01: incomplete capture rejected");
        var attached = new NativeAttachedFusionTimeline(0);
        check(attached.Draw(false, c) is null && attached.Angle == 0,
            "V11 attached: inactive card suppresses Draw and angle mutation");
        check(attached.Draw(true, c) == new NativeFusionSprite(0, 0, 80, -2),
            "V11 attached: native signed entrance formula");
        for (int i = 0; i < 16; i++) attached.AdvanceNativeUpdate(false);
        for (int i = 0; i < 5; i++) attached.AdvanceNativeUpdate(false);
        check(attached.Counter == 128 && attached.Active, "V11 attached: gate holds at 128");
        check(attached.Draw(true, c) == new NativeFusionSprite(0, 65536, 160, -6) &&
              attached.Draw(true, c) == new NativeFusionSprite(0, 65536, 160, -10),
            "V11 attached: Draw mutates angle independently of waiting Update");
        for (int i = 0; i < 16; i++) attached.AdvanceNativeUpdate(true);
        check(attached.Counter == 256 && attached.Draw(true, c)?.ScaleFixed16 == 65536,
            "V11 attached: growth starts at 256");
        for (int i = 0; i < 16; i++) attached.AdvanceNativeUpdate(true);
        check(!attached.Active && attached.Counter == 384 && attached.Draw(true, c) is null,
            "V11 attached: deactivation at 384");
        var general = new NativeGeneralFusionTimeline(0);
        check(general.Draw(c) == (1, 0, 128, (int?)null, (int?)null) && general.Angle == -4,
            "V11 general: entrance pass and Draw accumulator");
        for (int i = 0; i < 32; i++) general.AdvanceNativeUpdate();
        check(general.Draw(c) == (1, 65536, 256, (int?)null, (int?)null),
            "V11 general: plateau at 128");
        for (int i = 0; i < 64; i++) general.AdvanceNativeUpdate();
        check(general.Draw(c) == (1, 65536, 256, 98304, 256),
            "V11 general: distinct second resource at 384 with signed lookup");
        for (int i = 0; i < 16; i++) general.AdvanceNativeUpdate();
        check(general.Draw(c) == (1, 0, 0, (int?)null, (int?)null),
            "V11 general: primary pass ends at 448, not the entire object");
    }
}
