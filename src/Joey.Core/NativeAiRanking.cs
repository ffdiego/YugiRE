namespace Joey.Core;

/// <summary>Pure final gate of 005B1360 at VA 005B154B..005B15AF.
/// Inputs are metrics ALREADY evaluated by the engine, not an AI or legality model.
/// Static transcription only; live selector26 lacks these metrics for replay.</summary>
public static class NativeAiRanking
{
    public static bool AcceptNormalCandidate(int bestScore, int bestOpponentLosses,
        int candidateScore, int calculatedAttack, int slot, ushort actionMask, ushort summary)
    {
        if (slot is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(slot));
        if (calculatedAttack < 900 || (actionMask & (1 << slot)) == 0) return false;
        if (candidateScore > bestScore) return true;
        return candidateScore == bestScore &&
            ((summary >> 3) & 7) > bestOpponentLosses && (summary & 0x1c0) != 0;
    }
}
