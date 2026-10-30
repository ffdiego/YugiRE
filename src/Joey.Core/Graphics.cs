using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;

namespace Joey.Core;

// Resource facts, not a reconstruction of the original renderer. See docs/24-dat-graphics.md.
public readonly record struct BitmapInfo(int Width, int Height)
{
    public static BitmapInfo Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 54 || bytes[0] != 'B' || bytes[1] != 'M')
            throw new InvalidDataException("Invalid BMP header");
        uint offset = BinaryPrimitives.ReadUInt32LittleEndian(bytes[10..]);
        uint header = BinaryPrimitives.ReadUInt32LittleEndian(bytes[14..]);
        int width = BinaryPrimitives.ReadInt32LittleEndian(bytes[18..]);
        int height = BinaryPrimitives.ReadInt32LittleEndian(bytes[22..]);
        int planes = BinaryPrimitives.ReadUInt16LittleEndian(bytes[26..]);
        int bits = BinaryPrimitives.ReadUInt16LittleEndian(bytes[28..]);
        uint compression = BinaryPrimitives.ReadUInt32LittleEndian(bytes[30..]);
        if (header < 40 || 14L + header > bytes.Length || offset < 14L + header ||
            width <= 0 || width > 4096 || height == 0 || height < -4096 || height > 4096 ||
            planes != 1 || bits != 24 || compression != 0)
            throw new InvalidDataException("Unsupported BMP layout (expected uncompressed 24-bit)");
        long stride = (width * 3L + 3) & ~3L;
        if (offset + stride * Math.Abs(height) > bytes.Length)
            throw new InvalidDataException("Truncated BMP pixels");
        return new(width, Math.Abs(height));
    }
}

public readonly record struct SceneSprite(string Name, int X, int Y)
{
    // Only explicit xxxYYY positions are interpreted; other numeric parameters are not coordinates.
    public static SceneSprite Find(byte[] text, string name, int occurrence = 0)
    {
        foreach (string line in Encoding.Latin1.GetString(text).Split('\n'))
        {
            string[] words = line.Split("//")[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2 || !words[0].Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            string position = words[1];
            if (position.Length == 6 && position.All(char.IsAsciiDigit) && occurrence-- == 0)
                return new(name, int.Parse(position[..3]), int.Parse(position[3..]));
        }
        throw new InvalidDataException($"Missing position for scene sprite {name}");
    }
}

public sealed record MenuResources(SceneSprite Buttons)
{
    public const int Width = 800, Height = 600, ButtonWidth = 400, ButtonHeight = 50;
    public const string Background = @"j\title\title_1_e.bmp";
    public const string Normal = @"j\title\menu_e_ani0.bmp";
    public const string Highlight = @"j\title\menu_e_ani6.bmp";
    public static MenuResources Load(Archive archive) => new(SceneSprite.Find(archive.Read(@"j\title\title.txt"), "menu_?_ani0.bmp"));
    public int Hit(float x, float y) => x >= Buttons.X && x < Buttons.X + ButtonWidth &&
        y >= Buttons.Y && y < Buttons.Y + 5 * ButtonHeight ? (int)((y - Buttons.Y) / ButtonHeight) : -1;
}

public sealed class CardArtwork
{
    readonly Dictionary<int, string> names = [];
    public int Count => names.Count;
    public string? For(Card card) => names.GetValueOrDefault(card.Index);
    public static CardArtwork Load(Archive archive, IReadOnlyCollection<Card> cards)
    {
        var result = new CardArtwork();
        var byIndex = cards.ToDictionary(c => c.Index);
        var entries = archive.Entries.Select(e => e.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string text = Encoding.Latin1.GetString(archive.Read(@"card\list_card.txt"));
        // The shipped manifest explicitly supplies both table index and public card ID.
        foreach (Match match in Regex.Matches(text, @"(?m)^//\s*(\d{4}):\[(\d{4})\][^\r\n]*\r?\n([A-Za-z0-9_]+\.bmp)\r?$", RegexOptions.IgnoreCase))
        {
            int index = int.Parse(match.Groups[1].Value), id = int.Parse(match.Groups[2].Value);
            if (!byIndex.TryGetValue(index, out var card)) continue; // Includes the back/sentinel entry.
            if (card.Id != id) throw new InvalidDataException($"Artwork manifest ID mismatch at index {index}");
            string name = @"card\" + match.Groups[3].Value;
            if (!entries.Contains(name)) throw new InvalidDataException($"Missing artwork resource {name}");
            if (!result.names.TryAdd(index, name)) throw new InvalidDataException($"Duplicate artwork index {index}");
        }
        return result;
    }
}
