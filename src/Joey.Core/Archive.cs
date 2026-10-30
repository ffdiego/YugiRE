using System.Buffers.Binary;
using System.Text;
namespace Joey.Core;

// RE evidence: docs/02-file-formats.md, KCEJYUGI index and 0x0054A000.
public sealed class Archive : IDisposable
{
    public record Entry(string Name, uint Offset, uint Size, uint Stored);
    private readonly FileStream stream;
    private readonly Dictionary<string, Entry> entries = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<Entry> Entries => entries.Values;
    public Archive(string path)
    {
        stream = File.OpenRead(path);
        try
        {
            using var reader = new BinaryReader(stream, Encoding.ASCII, true);
            if (Encoding.ASCII.GetString(reader.ReadBytes(8)) != "KCEJYUGI") throw new InvalidDataException("Invalid DAT signature");
            uint count = reader.ReadUInt32();
            long end = 12L + count * 268L;
            if (end > stream.Length) throw new InvalidDataException("Truncated DAT index");
            for (uint i = 0; i < count; i++)
            {
                byte[] name = reader.ReadBytes(256);
                for (int j = 0; j < name.Length; j++) name[j] = (byte)((name[j] >> 4) | (name[j] << 4));
                int terminator = Array.IndexOf(name, (byte)0);
                if (terminator < 0) throw new InvalidDataException("Unterminated DAT name");
                var entry = new Entry(Encoding.ASCII.GetString(name, 0, terminator), reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32());
                if (entry.Offset < end || (long)entry.Offset + entry.Stored > stream.Length) throw new InvalidDataException("DAT entry outside payload");
                if (!entries.TryAdd(entry.Name, entry)) throw new InvalidDataException("Duplicate DAT name");
            }
        }
        catch { stream.Dispose(); throw; }
    }
    public byte[] Read(string name)
    {
        var e = entries[name];
        if (e.Size > 16 * 1024 * 1024 || e.Stored > 16 * 1024 * 1024) throw new InvalidDataException("Resource exceeds application limit");
        stream.Position = e.Offset;
        byte[] data = new byte[(int)e.Stored]; stream.ReadExactly(data);
        return e.Size == e.Stored ? data : Decode(data, checked((int)e.Size));
    }
    public static byte[] Decode(ReadOnlySpan<byte> input, int size)
    {
        if (size < 0 || size > 16 * 1024 * 1024) throw new InvalidDataException("Invalid output size");
        byte[] ring = new byte[4096], output = new byte[size];
        int source = 0, dest = 0, pos = 0xfee, flags = 0;
        while (dest < size)
        {
            flags >>= 1;
            if ((flags & 256) == 0) flags = Next(input, ref source) | 0xff00;
            if ((flags & 1) != 0)
            {
                byte b = Next(input, ref source); output[dest++] = b; ring[pos] = b; pos = (pos + 1) & 4095;
            }
            else
            {
                int a = Next(input, ref source), b = Next(input, ref source), start = a | ((b & 240) << 4);
                for (int j = 0; j < (b & 15) + 3 && dest < size; j++)
                {
                    byte v = ring[(start + j) & 4095]; output[dest++] = v; ring[pos] = v; pos = (pos + 1) & 4095;
                }
            }
        }
        if (source != input.Length) throw new InvalidDataException("Unexpected compressed trailing bytes");
        return output;
    }
    private static byte Next(ReadOnlySpan<byte> input, ref int source) => source < input.Length ? input[source++] : throw new InvalidDataException("Truncated LZSS");
    public void Dispose() => stream.Dispose();
}

public record Card(int Index, ushort Id, string Name, string Description, uint Properties)
{
    public int ClassCode => (int)((Properties >> 20) & 31);
    // Native 0x00402690 accepts classes 1 through 20, excluding the empty class 0.
    public bool IsMonster => ClassCode is >= 1 and <= 20;
    public int Attack => (int)((Properties >> 9) & 511) * 10;
    public int Defense => (int)(Properties & 511) * 10;
    public int Level => (int)((Properties >> 25) & 15);
}

public static class Catalog
{
    public static List<Card> Load(Archive archive)
    {
        var ids = archive.Read(@"bin#\card_id.bin");
        var names = archive.Read(@"bin#\card_nameeng.bin");
        var properties = archive.Read(@"bin#\card_prop.bin");
        var descriptions = archive.Read(@"bin#\card_desceng.bin");
        var offsets = archive.Read(@"bin#\card_indxeng.bin");
        int count = ids.Length / 2;
        if (ids.Length % 2 != 0 || names.Length != count * 64 || properties.Length != count * 4 || offsets.Length < count * 4) throw new InvalidDataException("Inconsistent card resource lengths");
        var cards = new List<Card>();
        for (int i = 0; i < count; i++)
        {
            ushort id = BinaryPrimitives.ReadUInt16LittleEndian(ids.AsSpan(i * 2));
            if (id == ushort.MaxValue) continue;
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(offsets.AsSpan(i * 4));
            if (offset >= descriptions.Length) throw new InvalidDataException("Invalid description offset");
            cards.Add(new(i, id, Text(names.AsSpan(i * 64, 64)), Text(descriptions.AsSpan((int)offset)), BinaryPrimitives.ReadUInt32LittleEndian(properties.AsSpan(i * 4))));
        }
        return cards;
    }
    private static string Text(ReadOnlySpan<byte> bytes)
    {
        int end = bytes.IndexOf((byte)0);
        if (end < 0) throw new InvalidDataException("Unterminated card text");
        return Encoding.ASCII.GetString(bytes[..end]);
    }
}
