using System.Globalization;

namespace Joey.LookAndFeel;

/// <summary>Opening bust 00415580 / 00415840. Unlike ordinary reactions,
/// the voice predicate jumps into a separate terminal counter range.</summary>
public sealed class NativeOpeningPlayback
{
    public const int VoiceCounter = 128, FirstPollCounter = 388;
    public const int ExitStart = 0x10000, ExitEnd = 0x10100;
    public int Variant { get; }
    public uint VoiceRequest => 0x10000025u + (uint)Variant;
    public int Counter { get; private set; }
    public bool Active { get; private set; } = true;
    public bool VoiceRequested { get; private set; }

    public NativeOpeningPlayback(int variant)
    {
        if (variant is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(variant));
        Variant = variant;
    }

    // The request occurs on INPUT 128. The successful poll returns
    // immediately, without the ordinary +4 in that Update.
    public uint? AdvanceNativeUpdate(bool audioComplete)
    {
        if (!Active || Counter >= ExitEnd) return null;
        uint? request = null;
        if (Counter == VoiceCounter)
        {
            VoiceRequested = true;
            request = VoiceRequest;
        }
        Counter = Counter >= FirstPollCounter && Counter < ExitStart && audioComplete
            ? ExitStart : checked(Counter + 4);
        return request;
    }

    // 00415ACB clears the active byte in Draw, not Update.
    public void CompleteNativeDraw()
    {
        if (Counter >= ExitEnd) Active = false;
    }
}

public readonly record struct NativeOpeningResource(int Key, string Path, CardPoint Position,
    int Width, int Height);
public readonly record struct NativeOpeningLayer(int Key, string Resource, CardPoint Position);

/// <summary>DAT contract from start.txt in cumulative v14. Draw is expressed
/// as a layer plan; the original temporary-surface compositor is not ported.</summary>
public sealed class NativeOpeningBank
{
    public const string Manifest = NativeJoeyCinematicRouting.StartManifest;
    private readonly NativeOpeningResource?[] resources;
    private NativeOpeningBank(NativeOpeningResource?[] resources) => this.resources = resources;
    public IEnumerable<NativeOpeningResource> Resources => resources.OfType<NativeOpeningResource>();

    // The 28/11 client capture shows opaque pure-green on opening sprites.
    // Apply the existing BMP diagnostic color-key path only to the known
    // character, mouth and dialogue resources, never to the backdrop/YGA.
    public static bool UsesGreenKey(string path)
    {
        const string prefix = NativeJoeyCinematicRouting.Prefix + @"start\";
        if (!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)) return false;
        string name=path[prefix.Length..];
        return new[]{"reaction16.bmp","reaction16_kuti1.bmp","reaction16_kuti2.bmp",
            "stato_e_01.bmp","stato_e_02.bmp","stato_e_03.bmp"}
            .Contains(name,StringComparer.OrdinalIgnoreCase);
    }

    public static NativeOpeningBank Parse(string manifest, IEnumerable<string> archiveNames)
    {
        string[] expected = ["start/statobg_?.bmp","start/kage.yga","start/reaction16.bmp",
            "null.bmp","null.bmp","start/reaction16_kuti1.bmp","start/reaction16_kuti2.bmp",
            "start/stato_?_01.bmp","start/stato_?_02.bmp","start/stato_?_03.bmp"];
        int[] packed = [0,47,0,0,0,206208,206208,320022,320022,320022];
        int[] widths = [800,684,672,0,0,94,94,480,480,480];
        int[] heights = [600,553,600,0,0,92,92,283,283,283];
        var rows = manifest.Split('\n')
            .Select(line => line.Split("//",2)[0].Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries))
            .Where(row => row.Length != 0).ToArray();
        if (rows.Length != expected.Length) throw new InvalidDataException("Unexpected opening manifest size.");
        var names = new HashSet<string>(archiveNames,StringComparer.OrdinalIgnoreCase);
        NativeOpeningResource?[] bank = new NativeOpeningResource?[rows.Length];
        for (int key=0; key<rows.Length; key++)
        {
            var row = rows[key];
            if (row.Length != (key == 0 ? 1 : 2) ||
                !row[0].Equals(expected[key],StringComparison.OrdinalIgnoreCase) ||
                (key != 0 && (!int.TryParse(row[1],NumberStyles.None,CultureInfo.InvariantCulture,out int value) ||
                    value != packed[key])))
                throw new InvalidDataException($"Unexpected opening manifest row {key}.");
            if (key is 3 or 4) continue; // native null eyes, not substitute artwork
            string path = NativeJoeyCinematicRouting.Prefix + row[0].Replace('?','e').Replace('/','\\');
            if (!names.Contains(path)) throw new InvalidDataException("Missing opening resource: "+path);
            bank[key] = new NativeOpeningResource(key,path,new(packed[key]/1000,packed[key]%1000),
                widths[key],heights[key]);
        }
        return new(bank);
    }

    // 004158D3..00415914: signed SAR 5, upper clamp only.
    public static int CharacterX(int counter, int width = 672) =>
        (Math.Min(counter - 64,32)*width >> 5) - width;

    public NativeOpeningLayer[] Sample(int counter, int variant, IReadOnlyList<int>? mouthKeys = null)
    {
        if (variant is < 0 or > 2 || counter < 0 || counter > NativeOpeningPlayback.ExitEnd)
            throw new ArgumentOutOfRangeException();
        if (mouthKeys is not null && (mouthKeys.Count != 3 || mouthKeys.Any(key => key is not (5 or 6))))
            throw new ArgumentException("Expected three captured mouth keys (5 or 6).",nameof(mouthKeys));
        var layers = new List<NativeOpeningLayer>();
        void Add(int key,int offsetX=0,int offsetY=0)
        {
            var resource = resources[key]!.Value;
            layers.Add(new(key,resource.Path,new(resource.Position.X+offsetX,resource.Position.Y+offsetY)));
        }
        Add(0);
        // The background surface is recomposed on the Update whose input is
        // 128. The Draw at exactly 128 still draws the sliding character.
        if (counter <= 128)
            Add(2,CharacterX(counter));
        else
        {
            Add(1);
            Add(2);
            Add(7+variant);
        }

        int phase = (counter >> 3) & 3;
        // The three nonzero phases read 005DA798[phase]. No table bytes
        // were captured in v14. A diagnostic may supply observed keys;
        // never synthesize a mouth sequence or count this as parity.
        if (counter < NativeOpeningPlayback.ExitStart && phase != 0 && mouthKeys is not null)
            Add(mouthKeys[phase-1],CharacterX(counter));
        return layers.ToArray();
    }
}
