using Joey.LookAndFeel;
using Joey.Core;
using Raylib_cs;
using System.Numerics;

static partial class Game
{
    /// <summary>
    /// Select EXACTLY ONE original cursor sprite for the current frame.
    /// Action-cloud YGAs are variants of the pointer, not additional
    /// card-anchored overlays.
    /// </summary>
    static string ActiveWindowCursorSprite()
    {
        bool pressed = Raylib.IsMouseButtonDown(MouseButton.Left);
        if (screen == "duel" && !ConsoleCapturesInput && !visualReviewPaused &&
            !MotionBusy && !PhaseBusy && !alphaBlocked &&
            CanPlayTurn && CanPlayMainOrBattle &&
            CpuDuelController.PendingOwner(duel) is null &&
            !choosingTributes && !choosingEquip && banishSummonChoice is null && ritualBoardChoice is null &&
            mouse.X >= 0 && mouse.X < NativeDuelLayout.CanvasWidth &&
            mouse.Y >= 0 && mouse.Y < NativeDuelLayout.CanvasHeight &&
            !duel.GameOver)
        {
            if (hoverHand >= 0 && hoverHand < ViewHand.Count)
                return NativeDuelCursor.CursorSprite(CurrentHandAction(hoverHand), pressed);

            // The Attack balloon is a variant of the SAME native pointer,
            // not an overlay on the card or the on-board sword marker.
            // Gameplay eligibility comes from Core (phase, attack used,
            // face-down/defense and pending actions); no speculative
            // offsets, sprite images or temporary cursors are introduced.
            for (int zone = 0; zone < NativeDuelLayout.MonsterSlots; zone++)
            {
                if (!Hit(ZoneBounds(ViewPlayer, zone, spell: false))) continue;
                bool own = duel.Monsters[ViewPlayer, zone] is not null;
                // Do not advertise a position change over a pending target
                // selection: the click would be consumed by that selection.
                bool choosingTarget = attacker >= 0 ||
                    duel.PendingFlipTarget is not null || duel.PendingSpellTrapTarget is not null;
                var action = duel.Phase == DemoPhase.Battle
                    ? NativeDuelCursor.FieldMonsterAttackAction(
                        own, duel.CanAttack(zone), choosingTarget)
                    : NativeDuelCursor.FieldMonsterPositionAction(
                        own, duel.CanChangePosition(zone),
                        duel.MonsterFaceDown[ViewPlayer, zone],
                        duel.DefensePosition[ViewPlayer, zone], choosingTarget);
                return NativeDuelCursor.CursorSprite(action, pressed);
            }
        }

        return NativeDuelCursor.CursorSprite(null, pressed);
    }

    /// <summary>
    /// Draw in WINDOW coordinates, after composing the scaled 800x600
    /// scene. This remains visible on menu, cards, deck and duel screens,
    /// including the letterbox area while the window is resized.
    /// </summary>
    static void DrawWindowCursor(float canvasScale)
    {
        var texture = graphics.Texture(ActiveWindowCursorSprite());
        Vector2 mousePosition = Raylib.GetMousePosition();
        var origin = NativeDuelCursor.PointerOrigin(
            new CardPoint((int)mousePosition.X, (int)mousePosition.Y));
        Raylib.DrawTextureEx(texture, new Vector2(origin.X, origin.Y),
            0f, canvasScale, Color.White);
    }
}
