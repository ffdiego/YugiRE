using System.Buffers.Binary;

namespace Joey.Core;

// Static evidence: docs/02-file-formats.md, 0x0044A550 / 0x00449EF0.
// The eight header bytes are preserved without assuming a checksum or timestamp.
public sealed class DeckFile
{
    public const int MaximumBytes = 8 + 3 * 2 + (80 + 15 + 15) * 2;
    public string HeaderHex { get; }
    public IReadOnlyList<IReadOnlyList<ushort>> Groups { get; }
    public bool HasDuelSize => Groups[0].Count is >= 40 and <= 80;
    private DeckFile(byte[] header, IReadOnlyList<ushort>[] groups)
    {
        HeaderHex = Convert.ToHexString(header);
        Groups = Array.AsReadOnly(groups);
    }
    public static DeckFile Load(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > MaximumBytes) throw new InvalidDataException("YDC exceeds the recognized deck layout");
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return Read(bytes);
    }
    public static DeckFile Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 14 || bytes.Length > MaximumBytes) throw new InvalidDataException("Invalid YDC length");
        int position = 8;
        IReadOnlyList<ushort>[] groups = new IReadOnlyList<ushort>[3];
        for (int group = 0; group < groups.Length; group++)
        {
            if (position + 2 > bytes.Length) throw new InvalidDataException("Truncated YDC list count");
            int count = BinaryPrimitives.ReadUInt16LittleEndian(bytes[position..]);
            position += 2;
            int limit = group == 0 ? 80 : 15;
            if (count > limit) throw new InvalidDataException($"YDC list {group} exceeds capacity {limit}");
            if (position + count * 2 > bytes.Length) throw new InvalidDataException("Truncated YDC card IDs");
            ushort[] ids = new ushort[count];
            for (int i = 0; i < count; i++, position += 2)
                ids[i] = BinaryPrimitives.ReadUInt16LittleEndian(bytes[position..]);
            groups[group] = Array.AsReadOnly(ids);
        }
        if (position != bytes.Length) throw new InvalidDataException("Unexpected YDC trailing bytes");
        return new(bytes[..8].ToArray(), groups);
    }
    public IReadOnlyList<Card> Resolve(int group, IEnumerable<Card> catalog)
    {
        if (group < 0 || group >= Groups.Count) throw new ArgumentOutOfRangeException(nameof(group));
        var byId = catalog.ToDictionary(c => c.Id);
        return Array.AsReadOnly(Groups[group].Select(id => byId.TryGetValue(id, out var card) ? card :
            throw new InvalidDataException($"Unknown YDC card ID {id} in list {group}")).ToArray());
    }
}
