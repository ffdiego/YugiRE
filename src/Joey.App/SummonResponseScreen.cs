using Joey.Core;
using Raylib_cs;

static partial class Game
{
    // The game core owns all legal options; this deliberately simple overlay
    // is not the original Joey priority/chain UI.
    static void DrawSummonResponseOverlay()
    {
        if (duel.PendingSummonResponse is not DemoSummonResponse response) return;
        Raylib.DrawRectangle(238, 110, 535, 400, new Color(7, 15, 28, 250));
        Raylib.DrawRectangleLines(238, 110, 535, 400, Gold);
        Label("SUMMON RESPONSE", 258, 128, 22, Gold);
        Label($"P{response.Summon.Player + 1}: {response.Summon.Monster.Name} ({response.Summon.Kind})",
            258, 160, 15);
        Label("Select a previously Set Trap, or Pass.", 258, 186, 15);
        for (int i = 0; i < response.EligibleTraps.Count && i < 10; i++)
        {
            var option = response.EligibleTraps[i];
            int x = 258 + (i % 2) * 250, y = 222 + (i / 2) * 44;
            if (Button($"P{option.Owner + 1} Z{option.Zone + 1}: {option.Trap.Name}",
                x, y, 238, 36))
            {
                if (duel.ActivateSummonTrap(option.Owner, option.Zone, out message))
                {
                    BeginCardActivation(option.Trap, ZoneBounds(option.Owner, option.Zone, true), nearSide: option.Owner == ViewPlayer);
                    ClearDuelSelection();
                }
                return;
            }
        }
        if (Button("Pass response", 565, 460, 180, 34))
        {
            if (duel.PassSummonResponse(out message))
                ClearDuelSelection();
        }
    }
}
