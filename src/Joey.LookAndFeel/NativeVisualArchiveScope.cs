namespace Joey.LookAndFeel;

/// <summary>
/// Visual archive regions used by the duel compositor and the opponent's
/// reaction/celebration cut-ins. The native EXE keeps these resources in
/// separate directories but queues their effects in related presentation
/// systems. This is only an asset discovery scope, not a timing model.
/// </summary>
public static class NativeVisualArchiveScope
{
    public static bool Includes(string? entryName) =>
        entryName is not null &&
        (entryName.StartsWith(@"j\duel\", StringComparison.OrdinalIgnoreCase) ||
         entryName.StartsWith(@"j\bust_up\", StringComparison.OrdinalIgnoreCase) ||
         entryName.StartsWith(@"cursor\", StringComparison.OrdinalIgnoreCase));
}
