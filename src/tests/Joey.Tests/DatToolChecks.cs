using System.Text;
using Joey.Tools;

static class DatToolChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "joey-tools-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string dat = Path.Combine(root, "fixture.dat");
            WriteDat(dat, [
                (@"j\duel\field\test.bmp", new byte[] {1, 2, 3}, 3u),
                (@"j\duel\field\nested\test.bmp", new byte[] {4}, 1u),
                (@"j\duel\field2\other.bmp", new byte[] {5}, 1u),
                (@"j\duel\field\line.txt", new byte[] {7, 65, 66, 67}, 3u),
                (@"joey\english\dummy.txt", Array.Empty<byte>(), 0u)
            ]);
            byte[] original = File.ReadAllBytes(dat);
            (int code, string stdout, string stderr) Run(params string[] args)
            {
                using var stdout = new StringWriter();
                using var stderr = new StringWriter();
                int code = ToolCli.Run(args, stdout, stderr);
                return (code, stdout.ToString(), stderr.ToString());
            }
            var list = Run("dat", "list", dat, "--folder", "J/DUEL/FIELD", "--ext", ".BMP");
            check(list.code == 0 && list.stdout.Contains("2 entries") && !list.stdout.Contains("field2"),
                "Joey.Tools lists recursive folder with case-insensitive extension");
            var names = Run("dat", "list", dat, "--name", "test.bmp");
            check(names.code == 0 && names.stdout.Contains("2 entries"),
                "Joey.Tools basename selects all matching entries");
            var match = Run("dat", "list", dat, "--match", @"FIELD\nested");
            check(match.code == 0 && match.stdout.Contains("1 entries"), "Joey.Tools matches normalized path");

            string single = Path.Combine(root, "single");
            check(Run("dat", "extract", dat, "--name", "J/duel/field/test.bmp", "--output", single).code == 0 &&
                File.ReadAllBytes(Path.Combine(single, "j", "duel", "field", "test.bmp")).SequenceEqual(new byte[] {1,2,3}) &&
                Directory.GetFiles(single, "*", SearchOption.AllDirectories).Length == 1,
                "Joey.Tools extracts full name preserving bytes and hierarchy");
            string all = Path.Combine(root, "all");
            check(Run("dat", "extract", dat, "--all", "--output", all).code == 0 &&
                Directory.GetFiles(all, "*", SearchOption.AllDirectories).Length == 5 &&
                File.ReadAllText(Path.Combine(all, "j", "duel", "field", "line.txt")) == "ABC" &&
                new FileInfo(Path.Combine(all, "joey", "english", "dummy.txt")).Length == 0,
                "Joey.Tools extracts all entries including compressed and empty resources");
            string subset = Path.Combine(root, "subset");
            check(Run("dat", "extract", dat, "--folder", @"j\duel\field\", "--ext", "bmp", "--output", subset).code == 0 &&
                Directory.GetFiles(subset, "*", SearchOption.AllDirectories).Length == 2,
                "Joey.Tools combines extraction filters");
            check(Run("dat", "extract", dat, "--all", "--output", all).code != 0 &&
                File.ReadAllText(Path.Combine(all, "j", "duel", "field", "line.txt")) == "ABC",
                "Joey.Tools refuses overwrite");
            check(Run("dat", "extract", dat).code != 0 &&
                Run("dat", "extract", dat, "--all", "--ext", "bmp").code != 0 &&
                Run("dat", "list", dat, "--name", "missing").code != 0 &&
                Run("dat", "list", dat, "--folder").code != 0,
                "Joey.Tools rejects absent selection, conflicting options and unmatched entries");
            check(File.ReadAllBytes(dat).SequenceEqual(original), "Joey.Tools leaves source DAT unchanged");

            string malicious = Path.Combine(root, "unsafe.dat");
            WriteDat(malicious, [("good.bmp", new byte[] {1}, 1u), (@"..\escaped.bmp", new byte[] {2}, 1u)]);
            string unsafeOutput = Path.Combine(root, "unsafe-output");
            check(Run("dat", "extract", malicious, "--all", "--output", unsafeOutput).code != 0 &&
                !File.Exists(Path.Combine(root, "escaped.bmp")) && !Directory.Exists(unsafeOutput),
                "Joey.Tools rejects traversal before writing any files");
            WriteDat(malicious, [("a/b.bmp", new byte[] {1}, 1u), (@"a\b.bmp", new byte[] {2}, 1u)]);
            check(Run("dat", "extract", malicious, "--all", "--output", unsafeOutput).code != 0 &&
                !Directory.Exists(unsafeOutput), "Joey.Tools rejects normalized path collisions");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static void WriteDat(string path, (string name, byte[] stored, uint decoded)[] entries)
    {
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(Encoding.ASCII.GetBytes("KCEJYUGI"));
        writer.Write((uint)entries.Length);
        uint offset = (uint)(12 + 268 * entries.Length);
        foreach (var entry in entries)
        {
            byte[] name = new byte[256];
            byte[] ascii = Encoding.ASCII.GetBytes(entry.name);
            for (int i = 0; i < ascii.Length; i++)
                name[i] = (byte)((ascii[i] >> 4) | (ascii[i] << 4));
            writer.Write(name);
            writer.Write(offset);
            writer.Write(entry.decoded);
            writer.Write((uint)entry.stored.Length);
            offset += (uint)entry.stored.Length;
        }
        foreach (var entry in entries) writer.Write(entry.stored);
    }
}
