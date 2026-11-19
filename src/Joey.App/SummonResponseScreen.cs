using Joey.Core;
using Raylib_cs;

static partial class Game
{
    private static PendingSummonResponse? renderedSummonResponse;
    private static int summonResponsePage;

    /// <summary>
    /// Provisional hot-seat control: a single summon response decision is
    /// offered to every owner with a legally Set Trap, not a native chain UI.
    /// No trap can activate from the hand or outside this response window.
    /// </summary>
    static void DrawSummonResponseOverlay()
    {
        var pending = duel.PendingSummonResponse;
        if (pending is null)
        {
            renderedSummonResponse = null;
            return;
        }
        if (!ReferenceEquals(pending, renderedSummonResponse))
        {
            renderedSummonResponse = pending;
            summonResponsePage = 0;
            ClearDuelSelection();
        }

        var ev = pending.Summon;
        Raylib.DrawRectangle(227, 94, 550, 454, new Color(20, 24, 32, 246));
        Raylib.DrawRectangleLines(227, 94, 550, 454, Color.Gold);
        Label("Summon response — hot-seat sandbox", 248, 110, 19, Gold);
        Label($"P{ev.Summoner + 1}: {ev.Monster.Name} ({ev.Kind})", 248, 140, 16);
        Label("Choose ONE Set Trap to activate, or Pass.", 248, 166, 14);
        int start = summonResponsePage * 5;
        int end = Math.Min(start + 5, pending.Options.Count);
        for (int i = start; i < end; i++)
        {
            var option = pending.Options[i];
            int y = 199 + 53 * (i - start);
            Label($"P{option.Owner + 1} · Zone {option.SpellZone + 1} · {option.Card.Name}",
                248, y + 7, 15, Gold);
            if (Button("Activate", 650, y, 104, 32))
            {
                if (duel.ActivateSummonTrap(option.Owner, option.SpellZone, out message))
                {
                    ClearDuelSelection();
                    summonResponsePage = 0;
                }
                return;
            }
        }
        if (Button("< Prev", 251, 491, 90, 29, summonResponsePage > 0))
            summonResponsePage--;
        if (Button("Next >", 353, 491, 90, 29, end < pending.Options.Count))
            summonResponsePage++;
        if (Button("Pass", 644, 489, 110, 33))
        {
            if (duel.PassSummonResponse(out message))
            {
                summonResponsePage = 0;
                ClearDuelSelection();
            }
        }
    }
}
