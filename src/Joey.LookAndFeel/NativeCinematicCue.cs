namespace Joey.LookAndFeel;

/// <summary>
/// A typed, presentation-only intent for the original game's bust-up,
/// signature and special-victory objects. Its lifetime is owned by the
/// existing PresentationSequence; Joey.Core never waits for this type.
/// </summary>
public enum NativeCinematicKind
{
    Reaction,
    SignatureSummon,
    ExodiaOpening,
    DestinyBoard,
    DuelOutcome
}

public readonly record struct NativeCinematicCue(
    NativeCinematicKind Kind, int Player, int? CardResourceId, string Manifest)
{
    public bool HasPlayableDraw => NativeCinematicRouting.ImplementedUpdates(this) is > 0;
}

/// <summary>
/// Static route evidence, not a claim that every candidate always triggers
/// a cut-in. 00403310 checks the duel/perspective flags before its 81/744
/// branches. Until these gates and the bust-up renderer are reconstructed,
/// signature routes are report-only and may NEVER reserve visual time.
/// </summary>
public static class NativeCinematicRouting
{
    public const int RedEyesResourceId = 0x51;      // Red-Eyes B. Dragon
    public const int AlternateSignatureResourceId = 0x2E8; // identity unconfirmed

    public static NativeCinematicCue ExodiaOpening(int player)
    {
        ValidatePlayer(player);
        return new(NativeCinematicKind.ExodiaOpening, player, null,
            @"j\duel\exodia\exodia.txt");
    }

    public static NativeCinematicCue DestinyBoardVictory(int player)
    {
        ValidatePlayer(player);
        return new(NativeCinematicKind.DestinyBoard, player, 1528,
            @"j\duel\effect\vija.txt");
    }

    public static NativeCinematicCue? SignatureCandidate(
        int player, int cardResourceId, bool faceDown)
    {
        ValidatePlayer(player);
        if (faceDown) return null;
        string? manifest = cardResourceId switch
        {
            RedEyesResourceId => @"j\bust_up\summon.txt",
            AlternateSignatureResourceId => @"j\bust_up\summon2.txt",
            _ => null
        };
        return manifest is null ? null :
            new NativeCinematicCue(NativeCinematicKind.SignatureSummon,
                player, cardResourceId, manifest);
    }

    /// <summary>
    /// Null explicitly means no safe playable Draw/timing contract.
    /// No invisible delays are permitted for such candidates. The first
    /// Exodia stage is currently the ONLY implemented cinematic scene.
    /// </summary>
    public static int? ImplementedUpdates(NativeCinematicCue cue) =>
        cue.Kind == NativeCinematicKind.ExodiaOpening &&
        cue.Manifest.Equals(@"j\duel\exodia\exodia.txt",
            StringComparison.OrdinalIgnoreCase) &&
        cue.Player is 0 or 1 && cue.CardResourceId is null
            ? NativeExodiaOpeningFrame.OpeningUpdates
            : null;

    private static void ValidatePlayer(int player)
    {
        if (player is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(player));
    }
}
