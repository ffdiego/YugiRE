using Joey.Core;
using Raylib_cs;

static partial class Game
{
    // Provisional hot-seat UI: attack declaration, one bounded response,
    // no chains/negation/priority. The Core supplies validated candidates.
    static void DrawAttackResponseOverlay()
    {
        if (duel.PendingAttackResponse is not DemoAttackResponse response) return;
        Raylib.DrawRectangle(238, 110, 535, 400, new Color(7, 15, 28, 250));
        Raylib.DrawRectangleLines(238, 110, 535, 400, Gold);
        Label("ATTACK RESPONSE", 258, 128, 22, Gold);
        Label($"P{response.Attack.AttackingPlayer + 1}: {response.Attack.Attacker.Name}",
            258, 160, 15);
        Label(response.Attack.DirectAttack
                ? "Direct attack declared (damage not applied yet)."
                : "Monster attack declared (defender not revealed yet).",
            258, 186, 15);
        for (int i = 0; i < response.EligibleTraps.Count && i < 10; i++)
        {
            var option = response.EligibleTraps[i];
            int x = 258 + (i % 2) * 250, y = 232 + (i / 2) * 44;
            if (Button($"P{option.Owner + 1} Z{option.Zone + 1}: {option.Trap.Name}",
                x, y, 238, 36))
            {
                if (duel.TryQueueAttackResponse(response, option, out message))
                {
                    ClearDuelSelection();
                }
                return;
            }
        }
        if (Button("Pass", 540, 460, 205, 34))
        {
            if (duel.TryQueueAttackResponse(response, null, out message))
            {
                ClearDuelSelection();
            }
        }
    }
}
