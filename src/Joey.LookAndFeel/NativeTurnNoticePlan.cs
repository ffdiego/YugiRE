namespace Joey.LookAndFeel;

/// <summary>
/// 00481230/00481370: presentation plan for ONE phase change, supplied
/// by the caller. The phase-buttons and new-player notice start together.
/// No queue, wall clock, duel mutation or implementation of IPresentationGate.
/// The 48-update banner interval is confirmed by 00480C20/00480C60
/// in the received turn-notice bundle. Draw releases the notice at t>=384.
/// </summary>
public readonly record struct NativeTurnNoticePlan
{
    public const int CounterIncrement = 8;
    public const int TurnNoticeEndCounter = 0x180;
    public const int TurnNoticeUpdates = TurnNoticeEndCounter / CounterIncrement;
    public const uint ChangePlayerSound = 0x10000005;
    public const int NoticeLayer = 0x700;
    public const int PanelLayer = 0xf0;
    public int PreviousSide { get; }
    public int NewSide { get; }
    public int Perspective { get; }
    public int NextPhase { get; }

    public NativeTurnNoticePlan(int previousSide, int newSide, int perspective, int nextPhase)
    {
        if (previousSide is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(previousSide));
        if (newSide is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(newSide));
        if (perspective is not (0 or 1)) throw new ArgumentOutOfRangeException(nameof(perspective));
        if (nextPhase is < -1 or > 5) throw new ArgumentOutOfRangeException(nameof(nextPhase));
        PreviousSide = previousSide;
        NewSide = newSide;
        Perspective = perspective;
        NextPhase = nextPhase;
    }

    public bool ChangesPlayer => PreviousSide != NewSide;
    public int RelativeSide => NewSide == Perspective ? 0 : 1;
    public int NoticeKind => ChangesPlayer ? 6 : NextPhase;
    public uint? SoundRequest => ChangesPlayer ? ChangePlayerSound : null;
    // 00481370 starts the next phase banner only at button counter 0x200.
    // -1 suppresses the next phase. Same-side phase starts immediately.
    public int? PhaseStartsAtUpdate => NextPhase == -1 ? null :
        ChangesPlayer ? NativePhaseTurnFlip.Updates : 0;

    // 00480BC0 preloads these five resource keys via virtual +8.
    // Do not interpret them as the complete animation's frame sequence.
    // 00480B40 similarly preloads three keys for Battle Phase.
    public int[] PreloadKeys()
    {
        int relative = RelativeSide;
        return ChangesPlayer
            ? Enumerable.Range(0,5).Select(i => 27 + relative*10 + 2*i).ToArray()
            : NextPhase == 3
                ? Enumerable.Range(0,3).Select(i => 13 + relative*7 + 2*i).ToArray()
                : [];
    }

    /// <summary>00480F35..00481024: exact logical lookup key, signed
    /// angle for 004223F0, and the optional prior-key release request.
    /// Preload and Draw use DIFFERENT side offsets in the original.
    /// Do not equate logical keys with physical texture indices until
    /// the manifest/lookup contract is validated.</summary>
    public NativeTurnNoticeDraw? DrawAtCounter(int counter)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(counter);
        if (!ChangesPlayer || counter >= TurnNoticeEndCounter) return null;
        int first = 36 - 10 * RelativeSide;
        int key, angle;
        if (counter < 64) { key = first; angle = (64-counter)*2; }
        else if (counter <= 320) { key = first+1+Math.Min((counter-64)/16,8); angle = 0; }
        else { key = first+9; angle = (320-counter)*2; }
        return new(key,angle,new CardPoint(525,300),key>first ? key-1 : null);
    }

    public NativeTurnNoticeSample Sample(int elapsedUpdates)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elapsedUpdates);
        int? turnCounter = ChangesPlayer && elapsedUpdates < TurnNoticeUpdates
            ? elapsedUpdates * CounterIncrement : null;
        int? flipUpdate = ChangesPlayer && elapsedUpdates < NativePhaseTurnFlip.Updates
            ? elapsedUpdates : null;
        int? phaseElapsed = PhaseStartsAtUpdate is int start && elapsedUpdates >= start
            ? elapsedUpdates - start : null;
        // Phase interval is already represented by NativePhaseNotice.
        // No claim about the native virtual +4 completion gate.
        return new(turnCounter, flipUpdate, phaseElapsed);
    }
}

public readonly record struct NativeTurnNoticeDraw(int LookupKey, int AngleIndex,
    CardPoint Center, int? ReleasePriorKey);

public readonly record struct NativeTurnNoticeSample(
    int? TurnNoticeCounter, int? ButtonFlipUpdate, int? PhaseElapsedUpdates);

/// <summary>
/// Strict manifest-order lookup for the preload keys. Keep actual resource
/// records and their integer parameters; never derive animation order from
/// names or preload calls. Language selection is the client's existing
/// English-first policy, not a port of native locale fallback.
/// </summary>
public readonly record struct NativePhaseNoticeResource(string Path, int? Parameter);

public static class NativePhaseNoticeResources
{
    public const string Prefix = @"j\duel\phase_name\";
    public const string Manifest = Prefix + "phase_name.txt";

    public static NativePhaseNoticeResource[] Parse(string text, IEnumerable<string> archiveNames,
        char language = 'e')
    {
        if (language is not ('e' or 'f' or 'g' or 'i' or 's'))
            throw new ArgumentOutOfRangeException(nameof(language));
        var available = new HashSet<string>(archiveNames,StringComparer.OrdinalIgnoreCase);
        List<NativePhaseNoticeResource> records = [];
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Split("//",2)[0].Trim();
            if (line.Length == 0) continue;
            string[] fields = line.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
            string name = fields[0].Replace('/','\\').Replace('?',language);
            if (fields.Length > 2 ||
                (!name.EndsWith(".bmp",StringComparison.OrdinalIgnoreCase) &&
                 !name.EndsWith(".yga",StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Unsupported phase_name record: " + line);
            string path = name.StartsWith(Prefix,StringComparison.OrdinalIgnoreCase) ? name :
                !name.Contains('\\') ? Prefix + name : throw new InvalidDataException("Unexpected phase_name path: " + name);
            if (!available.Contains(path) || path.Split('\\').Any(p=>p is "." or ".." or ""))
                throw new InvalidDataException("Missing or invalid phase_name resource: " + path);
            int? parameter = null;
            if (fields.Length == 2)
            {
                if (!int.TryParse(fields[1], System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,out int value))
                    throw new InvalidDataException("Unsupported phase_name parameter: " + line);
                parameter = value;
            }
            records.Add(new(path,parameter));
        }
        // 00480BC0 preloads key 45 for the far side. Require the slot,
        // but do not claim a fixed final manifest length.
        if (records.Count < 46) throw new InvalidDataException("Incomplete phase_name manifest for native preload keys.");
        return records.ToArray();
    }

    // SQLite v3: the original manifest has 46 records, with two
    // ten-entry turn-change sequences and repeated endpoint resources.
    // Check the real order rather than sort distinct texture filenames.
    public static void ValidateTurnNoticeBank(IReadOnlyList<NativePhaseNoticeResource> manifest)
    {
        if(manifest.Count!=46)throw new InvalidDataException("Unexpected phase_name bank size.");
        for(int group=0;group<2;group++)
        for(int frame=0;frame<10;frame++)
        {
            char side=group==0?'b':'r';
            char last=group==0?'r':'b';
            string name=frame==9?$"nex_e_{last}.yga":
                $"nex_e_{side}{(frame==0?"":frame.ToString(System.Globalization.CultureInfo.InvariantCulture))}.yga";
            if(!manifest[26+group*10+frame].Path.Equals(Prefix+name,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Unexpected turn notice manifest order.");
        }
    }

    public static NativePhaseNoticeResource[] Preloads(NativeTurnNoticePlan plan,
        IReadOnlyList<NativePhaseNoticeResource> manifest) =>
        plan.PreloadKeys().Select(key => key < manifest.Count ? manifest[key] :
            throw new InvalidDataException("Missing native preload key " + key)).ToArray();
}
