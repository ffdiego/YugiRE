using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Numerics;

static partial class Game
{
    sealed record DestructionCue(DemoMonsterDestruction Monster, NativeExplosionTimeline Timeline);

    static readonly List<DestructionCue> destructions = [];
    static readonly PresentationTickClock destructionClock = new();
    static bool DestructionBusy => destructions.Count > 0;

    static void ResetDestructions()
    {
        destructions.Clear();
        destructionClock.Reset();
        duel.MonsterDestroyed += QueueDestruction;
    }

    static void QueueDestruction(DemoMonsterDestruction monster)
    {
        if (!presentation.Style.PlayAnimations) return;
        if (!DestructionBusy) destructionClock.Reset();
        destructions.Add(new(monster, new NativeExplosionTimeline()));
    }

    static void AdvanceDestructions(TimeSpan elapsed)
    {
        if (!DestructionBusy) return;

        int updates = Math.Min(26, destructionClock.Advance(elapsed, presentation.Style));
        foreach (var cue in destructions)
        {
            for (int i = 0; i < updates && cue.Timeline.Active; i++)
            {
                cue.Timeline.AdvanceNativeUpdate();
                // Complete even after a long render stall skips the final samples.
                cue.Timeline.AfterNativeDraw();
            }
        }
        destructions.RemoveAll(cue => !cue.Timeline.Active);
    }

    static int VisibleGraveCount(int player) => Math.Max(0, duel.Graveyard(player).Count -
        destructions.Count(cue => cue.Monster.Player == player));

    static void DrawGraveyards()
    {
        for (int player = 0; player < 2; player++)
        {
            bool near = player == ViewPlayer;
            int y = near ? 416 : 96;
            Raylib.DrawRectangleLines(727, y, 57, 88, Color.Gray);
            if (VisibleGraveCount(player) > 0) DrawCardBack(730, y + 3, 51, 70);
            Label("GRAVE", 733, y + 74, 10, Gold);
        }
    }

    static void DrawDestructions()
    {
        foreach (var cue in destructions)
        {
            var monster = cue.Monster;
            var rect = ZoneBounds(monster.Player, monster.Zone, false);
            var center = new Vector2(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            int counter = cue.Timeline.Counter;
            var passes = cue.Timeline.VisiblePasses();

            if ((passes & NativeExplosionPasses.Initial) != 0)
                DrawFieldCard(monster.Card, rect, monster.FaceDown, monster.Defense);
            if ((passes & NativeExplosionPasses.Flare) != 0)
            {
                int alpha = Math.Clamp(235 - 3 * Math.Abs(counter - 88), 0, 235);
                Raylib.DrawCircleV(center, 8 + counter / 6f, new Color(255, 220, 151, alpha));
                Raylib.DrawCircleLines((int)center.X, (int)center.Y, counter / 3f, new Color(255, 246, 218, alpha));
            }
            if ((passes & NativeExplosionPasses.Fragments) == 0) continue;
            // Procedural fragments use the native draw window, with client-selected shapes and trajectories.
            float progress = (counter - 128) / 80f;
            int opacity = (int)(255 * (1 - progress));
            for (int i = 0; i < 24; i++)
            {
                float angle = i * 2.399963f;
                float distance = (9 + progress * 48) * (0.5f + (i % 7) / 10f);
                Vector2 point = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
                Raylib.DrawRectangle((int)point.X, (int)point.Y, 2 + i % 4, 2 + i % 3,
                    new Color(255, 185 + i % 65, 125 + i % 100, opacity));
            }
        }
    }
}
