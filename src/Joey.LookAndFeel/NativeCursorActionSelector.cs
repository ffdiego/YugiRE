namespace Joey.LookAndFeel;

/// <summary>Logical action selector recovered from 005B8910/005BF490/005BF4B0
/// and the recovered priority table at 005DD980. The caller provides the
/// available-actions mask.</summary>
public sealed class NativeCursorActionSelector
{
    private readonly int[] priority;

    // 005DD980, 13 signed dwords from roteiro 26.
    public NativeCursorActionSelector() : this([2, 10, 11, 3, 0, 1, 4, 5, 6, 7, 12, 8, 9]) { }

    public NativeCursorActionSelector(IEnumerable<int> priority)
    {
        this.priority = priority.ToArray();
        if (this.priority.Length != 13 ||
            this.priority.Any(x => x is < 0 or > 12) ||
            this.priority.Distinct().Count() != 13)
            throw new InvalidDataException("Expected a permutation of action codes 0..12.");
    }

    public int? First(ushort available)
    {
        foreach (int action in priority)
            if ((available & (1 << action)) != 0) return action;
        return null;
    }

    // 005B8910 walks the table circularly, skipping unavailable actions.
    // If the current action is absent, restart as for a new hover.
    // This defensive fallback is an adapter policy, not a native claim.
    public int? Next(ushort available, int? current)
    {
        if (current is not int code) return First(available);
        int start = Array.IndexOf(priority, code);
        if (start < 0 || (available & (1 << code)) == 0)
            return First(available);
        for (int i = 1; i <= priority.Length; i++)
        {
            int action = priority[(start + i) % priority.Length];
            if ((available & (1 << action)) != 0) return action;
        }
        return null;
    }

    /// <summary>005BF490 writes action+1 to object+108h. 005BF4B0
    /// writes zero; the special action 12 also uses that reset path.
    /// These are object field values, not texture indices.</summary>
    public static int ObjectRequest(int? action) => action switch
    {
        null or 12 => 0,
        >= 0 and < 12 => action.Value + 1,
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };
}
