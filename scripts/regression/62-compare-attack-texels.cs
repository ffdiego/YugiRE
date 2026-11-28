#:project ../../src/Joey.LookAndFeel/Joey.LookAndFeel.csproj
#:property PublishAot=false
using System.Buffers.Binary;
using System.Text.Json;
using Joey.LookAndFeel;

const string Prefix = "attack-texel-samples ";
if (args.Length != 1)
    throw new ArgumentException("Specify the private GDB log path.");

var lines = File.ReadAllLines(args[0]);
if (!lines.Any(line => line.Contains("detached", StringComparison.Ordinal)))
    throw new InvalidDataException("Incomplete capture.");

var record = lines.Single(line => line.StartsWith(Prefix, StringComparison.Ordinal));
using var document = JsonDocument.Parse(record[Prefix.Length..]);
var data = document.RootElement;
bool extraDivisor = data.GetProperty("extraDivisor").GetBoolean();
bool keyedCopy = data.TryGetProperty("keyedCopy", out var keyedCopyFlag) && keyedCopyFlag.GetBoolean();
var vertices = ReadPoints("destinationVertices").Zip(ReadPoints("sourceVertices"),
    (destination, source) => new NativeQuadVertex(destination, source)).ToArray();
var prediction = NativeQuadScanlines.Samples(vertices, new CardBounds(0, 0, 800, 600), extraDivisor)
    .ToDictionary(sample => (sample.X, sample.Y));

var samples = data.GetProperty("samples");
if (samples.GetArrayLength() != 144)
    throw new InvalidDataException("Expected 144 samples from a single impact.");

var coordinates = new HashSet<(int X, int Y)>();
Span<byte> sourcePixel = stackalloc byte[2];
Span<byte> destinationPixel = stackalloc byte[2];
int keyed = 0;
int changed = 0;
foreach (var item in samples.EnumerateArray())
{
    var observed = new NativeQuadSample(item.GetProperty("x").GetInt32(), item.GetProperty("y").GetInt32(),
        item.GetProperty("u").GetInt32(), item.GetProperty("v").GetInt32());
    var position = (observed.X, observed.Y);
    if (!item.GetProperty("aligned").GetBoolean() || !coordinates.Add(position))
        throw new InvalidDataException($"Invalid or repeated sample at {position}.");
    if (!prediction.TryGetValue(position, out var expected))
        throw new InvalidDataException($"No predicted texel at {position}.");
    if (observed != expected)
        throw new InvalidDataException($"At {position}, observed ({observed.U}, {observed.V}) " +
            $"and predicted ({expected.U}, {expected.V}).");
    if (!keyedCopy)
        continue;

    ushort source = item.GetProperty("source").GetUInt16();
    ushort key = item.GetProperty("key").GetUInt16();
    ushort before = item.GetProperty("before").GetUInt16();
    ushort after = item.GetProperty("after").GetUInt16();
    BinaryPrimitives.WriteUInt16LittleEndian(destinationPixel, before);
    if (source == key)
        keyed++;
    else
    {
        BinaryPrimitives.WriteUInt16LittleEndian(sourcePixel, source);
        NativeSurfaceCopy.CopyPixel(sourcePixel, NativeSurfaceFormat.Rgb565,
            destinationPixel, NativeSurfaceFormat.Rgb565);
    }
    ushort expectedPixel = BinaryPrimitives.ReadUInt16LittleEndian(destinationPixel);
    if (after != expectedPixel)
        throw new InvalidDataException($"At {position}, observed RGB565 {after:x4} and predicted {expectedPixel:x4}.");
    if (after != before)
        changed++;
}

Console.WriteLine($"Intensity {data.GetProperty("intensity").GetInt32()}, " +
    $"extra divisor {extraDivisor}: {coordinates.Count} matching texels.");
if (keyedCopy)
    Console.WriteLine($"{coordinates.Count} matching RGB565 pixels, {keyed} key matches and {changed} changed.");

CardPoint[] ReadPoints(string property) => data.GetProperty(property).EnumerateArray()
    .Select(point => new CardPoint(point[0].GetInt32(), point[1].GetInt32())).ToArray();
