#:project ../../src/Joey.LookAndFeel/Joey.LookAndFeel.csproj
#:property PublishAot=false
using System.Buffers.Binary;
using System.Text.Json;
using Joey.LookAndFeel;

const string Prefix = "attack-packed-pixels ";
const string BasePrefix = "attack-packed-base-pixels ";
if (args.Length != 1)
    throw new ArgumentException("Specify the private GDB log path.");

string log = File.ReadAllText(args[0]);
if (!log.Contains("detached")) throw new InvalidDataException("Incomplete capture");
string record = log.Split('\n').Single(row => row.StartsWith(Prefix) || row.StartsWith(BasePrefix));
bool basePath = record.StartsWith(BasePrefix);
string json = record[(basePath ? BasePrefix.Length : Prefix.Length)..];
using var document = JsonDocument.Parse(json);
var root = document.RootElement;
int sourceFormat = root.GetProperty("sourceFormat").GetInt32();
int intensity = root.GetProperty("intensity").GetInt32();
var destinationFormat = (sourceFormat, root.GetProperty("destinationFormat").GetInt32()) switch
{
    (10, 3) => NativeSurfaceFormat.Rgb565,
    (11, 4) => NativeSurfaceFormat.Rgb555,
    _ => throw new InvalidDataException("Unexpected packed alpha format pair")
};
if (basePath && (intensity < 256 || destinationFormat != NativeSurfaceFormat.Rgb565))
    throw new InvalidDataException("Unexpected base composition request");

var coordinates = new HashSet<(int, int)>();
var alphas = new HashSet<byte>();
Span<byte> source = stackalloc byte[4];
Span<byte> destination = stackalloc byte[2];
int keySkips = 0, changes = 0;
foreach (var row in root.GetProperty("samples").EnumerateArray())
{
    int x = row.GetProperty("x").GetInt32();
    int y = row.GetProperty("y").GetInt32();
    if (!coordinates.Add((x, y)) || row.GetProperty("intensity").GetInt32() != intensity)
        throw new InvalidDataException("Repeated coordinates or mismatched intensity");
    uint input = row.GetProperty("source").GetUInt32();
    ushort before = row.GetProperty("before").GetUInt16();
    ushort after = row.GetProperty("after").GetUInt16();
    BinaryPrimitives.WriteUInt32LittleEndian(source, input);
    BinaryPrimitives.WriteUInt16LittleEndian(destination, before);
    bool applied = true;
    if (basePath)
        NativePackedAlphaPixel.ApplyBaseToRgb565(source, destination);
    else
        applied = NativePackedAlphaPixel.ApplyToOpaque(source, destination, sourceFormat,
            destinationFormat, row.GetProperty("key").GetUInt16(), intensity);
    ushort computed = BinaryPrimitives.ReadUInt16LittleEndian(destination);
    if (computed != after)
        throw new InvalidDataException($"({x}, {y}): expected {computed:X4}, observed {after:X4}");
    if (applied)
        alphas.Add(source[2]);
    else
        keySkips += 1;
    if (before != after) changes += 1;
}
if (coordinates.Count == 0)
    throw new InvalidDataException("The capture contains no pixel samples");
Console.WriteLine($"{coordinates.Count} matched, {keySkips} keyed, {changes} changed. Alpha values: {string.Join(", ", alphas.Order())}");
