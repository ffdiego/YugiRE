using System.Globalization;

namespace Joey.LookAndFeel;

/// <summary>Two separate draw-driven objects, 00455FF0 and 00456360.
/// Update 004481F0 is empty. Sample is pure; only AdvanceNativeDraw
/// mutates the native phase. Never call it from a scheduler gate query.</summary>
public sealed class NativeLayerFontA
{
    public const int ResourceKey = 0;
    public int Counter { get; private set; }
    public static bool CreatedForMode(int mode) => mode is 5 or 7;
    public void AdvanceNativeDraw() => Counter = (Counter + 4) & 255;
    public int Intensity(ReadOnlySpan<int> cosine)
    {
        NativeFusionMath.Validate(cosine);
        return Counter < 64 ? NativeFusionMath.C(cosine, 2*Counter-128) >> 8 :
            Counter <= 192 ? 256 : NativeFusionMath.C(cosine, 2*Counter+128) >> 8;
    }
    public static CardBounds Bounds(int width, int height) =>
        new(788-width,588-height,width,height);
}

public sealed class NativeLayerFontB
{
    public const int ResourceKey = 1;
    public int Value { get; private set; }
    public bool Visible => Value > 224;
    public static bool CreatedForMode(int mode) => mode == 3;
    public static bool WantsDisplay(bool native005BDAD0, bool native0044BA10) =>
        native005BDAD0 && !native0044BA10;
    public void AdvanceNativeDraw(bool wantsDisplay) =>
        Value = Math.Clamp(Value + (wantsDisplay ? 4 : -4), 0, 256);
    // 0045640C loads the center from 005DB570 (DWORD 525 in bundle 01).
    public const int CenterX = 525;
    public CardBounds? Bounds(int width,int height,ReadOnlySpan<int> cosine) =>
        Top(height,cosine) is int y ? new(CenterX-width/2,y,width,height) : null;

    public int? Top(int height, ReadOnlySpan<int> cosine)
    {
        NativeFusionMath.Validate(cosine);
        if (!Visible) return null;
        return unchecked(((height+16)*NativeFusionMath.C(cosine,4*(Value-224)-128) >> 16)-height);
    }

    // 005BDAD0 reads a signed four-bit field from bits 21..24.
    // Keep raw predicates separate from interpretation of duel modes.
    public static bool NativeDecisionPredicate(uint scheduler, byte turnByte,
        uint perspectiveWord, uint decisionWord)
    {
        if ((scheduler & 0x4000) != 0) return false;
        int extra = (turnByte & 0x10) != 0 && ((~perspectiveWord ^ turnByte) & 1) == 0 ? 1 : 0;
        int signedField = unchecked((int)(decisionWord << 7)) >> 28;
        return signedField + extra > 0;
    }
}

/// <summary>Strict two-entry manifest. The English language choice is the
/// existing client's policy, not a reconstruction of native locale fallback.</summary>
public static class NativeLayerFontResources
{
    public const string Prefix = @"j\duel\effect\";
    public const string Manifest = Prefix + "layerfont.txt";
    // All ten observed BMPs in the v7 DAT inventory. No inferred
    // fallback to English dimensions for other locales.
    public static CardBounds ExpectedSize(int key,char language='e')
    {
        if(language is not ('e' or 'f' or 'g' or 'i' or 's'))
            throw new ArgumentOutOfRangeException(nameof(language));
        return key switch
        {
            0 => language switch
            {
                'f' => new(0,0,233,37),
                'g' => new(0,0,72,30),
                's' => new(0,0,215,36),
                _ => new(0,0,233,30)
            },
            1 => new(0,0,469,34),
            _ => throw new ArgumentOutOfRangeException(nameof(key))
        };
    }

    public static void ValidateSize(int key,int width,int height,char language='e')
    {
        var expected=ExpectedSize(key,language);
        if(expected.Width!=width || expected.Height!=height)
            throw new InvalidDataException("Unexpected Layerfont texture dimensions.");
    }

    public static string[] Parse(string text, IEnumerable<string> archiveNames, char language = 'e')
    {
        if (language is not ('e' or 'f' or 'g' or 'i' or 's'))
            throw new ArgumentOutOfRangeException(nameof(language));
        var available = new HashSet<string>(archiveNames,StringComparer.OrdinalIgnoreCase);
        List<string> paths = [];
        foreach(string raw in text.Split('\n'))
        {
            string line = raw.Split("//",2)[0].Trim();
            if (line.Length == 0) continue;
            string[] fields = line.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
            string name = fields[0].Replace('/','\\').Replace('?',language);
            if (!name.EndsWith(".bmp",StringComparison.OrdinalIgnoreCase) ||
                fields.Skip(1).Any(f=>!int.TryParse(f,NumberStyles.Integer,CultureInfo.InvariantCulture,out _)))
                throw new InvalidDataException("Unexpected layerfont record: "+line);
            string path = name.StartsWith(Prefix,StringComparison.OrdinalIgnoreCase) ? name :
                !name.Contains('\\') ? Prefix+name : throw new InvalidDataException("Unexpected layerfont path: "+name);
            if (path.Split('\\').Any(p=>p is "." or ".." or "") || !available.Contains(path))
                throw new InvalidDataException("Missing layerfont resource: "+path);
            paths.Add(path);
        }
        if (paths.Count != 2) throw new InvalidDataException("Expected two layerfont records.");
        return paths.ToArray();
    }
}
