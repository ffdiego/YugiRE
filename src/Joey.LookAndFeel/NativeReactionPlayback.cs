using System.Globalization;

namespace Joey.LookAndFeel;

/// <summary>Observed V31 Draw epilogue. A caller supplies the audio predicate;
/// the reaction counter is not a wall clock or a rule-engine timeout.</summary>
public readonly record struct NativeReactionStep(int Counter, uint PrimarySound,
    uint SecondarySound, uint? RequestedSound, bool Active);

public sealed class NativeReactionPlayback
{
    public int Counter { get; private set; }
    public uint PrimarySound { get; private set; }
    public uint SecondarySound { get; private set; }
    public bool Active { get; private set; }

    public NativeReactionPlayback(uint primary, uint secondary = 0) => Reset(primary, secondary);

    public void Reset(uint primary, uint secondary = 0)
    {
        if (primary == 0 || (primary & 0xf0000000u) != 0x10000000u ||
            (secondary != 0 && (secondary & 0xf0000000u) != 0x10000000u))
            throw new ArgumentException("Expected Internal sound requests.");
        Counter = 0;
        PrimarySound = primary;
        SecondarySound = secondary;
        Active = true;
    }

    // Review steps represent one native Update followed by one Draw.
    // Audio completion is evaluated for the current primary, before any
    // promotion. A promoted second sound cannot complete in the same Draw.
    public NativeReactionStep Step(bool currentSoundComplete)
    {
        uint? request = null;
        if (Active)
        {
            Counter = checked(Counter + 8);
            if (Counter == 8)
                request = PrimarySound;
            else if (SecondarySound != 0)
            {
                if (currentSoundComplete)
                {
                    PrimarySound = SecondarySound;
                    SecondarySound = 0;
                    request = PrimarySound;
                }
            }
            else if (Counter >= 256 && currentSoundComplete)
                Active = false;
        }
        return new(Counter, PrimarySound, SecondarySound, request, Active);
    }
}

public readonly record struct NativeReactionLayer(int Key, string Resource, CardPoint Position);

/// <summary>V31.2/V31.3: only the observed reaction07 rectangular base
/// and header-1/header-2 groups. Do not generalize to other manifests.</summary>
public sealed class NativeReaction07
{
    public const string Manifest = NativeJoeyCinematicRouting.Prefix + "reaction07.txt";
    public const uint ObservedPrimarySound = 0x1000003a;
    // Internal descriptor 005F00E8 points at 005F069C.
    public const string ActivationVoice = DuelSoundAssets.InternalPrefix + "joey_22.wav";
    public const int Width = 327, Height = 509;

    // 00402BE0: selected side, generic Spell branch, 00402700 subtype.
    // Explicit IDs take an earlier branch and do not request this voice.
    public static bool IsGenericSpellActivation(ushort resourceId, int cardClass, uint properties) =>
        cardClass == 22 && ((properties >> 17) & 7) != 2 &&
        resourceId is not (0x28a or 0x3c7 or 0x3de or 0x3f0 or 0x420 or 0x438 or
            0x443 or 0x447 or 0x487 or 0x4b3 or 0x4b4 or 0x4de or 0x76e);
    public CardPoint Origin { get; }
    public string BaseResource { get; }
    private readonly NativeReactionLayer[] layers;
    private NativeReaction07(CardPoint origin, string baseResource, NativeReactionLayer[] layers)
        => (Origin, BaseResource, this.layers) = (origin, baseResource, layers);

    public IEnumerable<string> Resources => new[] { BaseResource }.Concat(layers.Select(x => x.Resource));

    public static NativeReaction07 Parse(string text, IEnumerable<string> archiveNames)
    {
        var rows = text.Split('\n')
            .Select(line => line.Split("//", 2)[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Where(words => words.Length > 0).ToArray();
        if (rows.Length < 11) throw new InvalidDataException("Incomplete reaction07 manifest.");
        var names = new HashSet<string>(archiveNames, StringComparer.OrdinalIgnoreCase);
        int Packed(int key)
        {
            if (rows[key].Length != 2 || !int.TryParse(rows[key][1],
                NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value))
                throw new InvalidDataException($"Invalid reaction07 row {key}.");
            return value;
        }
        CardPoint Point(int key)
        {
            int packed = Packed(key);
            return new(packed / 1000, packed % 1000);
        }
        string Resource(int key)
        {
            _ = Packed(key);
            string path = NativeJoeyCinematicRouting.Prefix + rows[key][0].Replace('/', '\\');
            if (!path.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) || !names.Contains(path))
                throw new InvalidDataException($"Missing reaction07 bitmap at row {key}: {path}");
            return path;
        }
        CardPoint origin = Point(0);
        if (origin != new CardPoint(450,40) ||
            Point(1) != new CardPoint(0,0) || Point(2) != new CardPoint(326,0) ||
            Point(4) != new CardPoint(326,508) || Point(3) != new CardPoint(0,508) ||
            Packed(5) != 1 || Packed(8) != 2)
            throw new InvalidDataException("Reaction07 differs from observed geometry/selectors.");
        NativeReactionLayer Layer(int key, CardPoint expected)
        {
            CardPoint p = Point(key);
            var position = new CardPoint(checked(origin.X+p.X), checked(origin.Y+p.Y));
            if (position != expected)
                throw new InvalidDataException($"Unexpected reaction07 layer position at row {key}.");
            return new(key, Resource(key), position);
        }
        return new(origin, Resource(0),
            [Layer(6,new(572,264)),Layer(7,new(572,264)),
             Layer(9,new(541,219)),Layer(10,new(541,219))]);
    }

    // 00416D8E..00416DE3: global scene state, NOT the reaction counter.
    // Both groups select [-1,0,1,0] from bits 1..2; header 2 also
    // suppresses its pass if either bit in mask 0x18 is set.
    public NativeReactionLayer[] Sample(uint sceneState)
    {
        int offset = ((sceneState >> 1) & 3) switch { 0 => -1, 2 => 1, _ => 0 };
        if (offset < 0) return [];
        return (sceneState & 0x18) != 0 ? [layers[offset]]
            : [layers[offset], layers[2+offset]];
    }
}
