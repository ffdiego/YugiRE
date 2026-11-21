using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;

static partial class Game
{
    enum DuelPhaseDisplay { Draw, Standby, Main1, Battle, Main2, End }

    static readonly string[] phaseLabels = ["DP", "SP", "M1", "BP", "M2", "EP"];
    static readonly PresentationTickClock phaseClock = new();
    static DuelPhaseDisplay displayedPhase;
    static int displayedTurn, phaseUpdates;
    static bool PhaseBusy => !duel.GameOver && (phaseUpdates > 0 ||
        displayedTurn != duel.Turn || displayedPhase is DuelPhaseDisplay.Draw or DuelPhaseDisplay.Standby);

    static void ResetDuelPhases()
    {
        displayedTurn = 0;
        displayedPhase = DuelPhaseDisplay.Draw;
        phaseUpdates = 0;
        phaseClock.Reset();
    }

    static void ShowPhase(DuelPhaseDisplay phase)
    {
        displayedPhase = phase;
        phaseUpdates = presentation.Style.PlayAnimations
            ? phase == DuelPhaseDisplay.Battle ? NativePhaseNotice.BattleUpdates : NativePhaseNotice.StandardUpdates
            : 0;
        phaseClock.Reset();
    }

    static void AdvanceDuelPhases(TimeSpan elapsed)
    {
        if (phaseUpdates > 0)
            phaseUpdates = Math.Max(0, phaseUpdates - phaseClock.Advance(elapsed, presentation.Style));
        if (MotionBusy || phaseUpdates > 0 || duel.GameOver) return;

        if (displayedTurn != duel.Turn)
        {
            if (displayedTurn != 0 && displayedPhase != DuelPhaseDisplay.End)
            {
                ShowPhase(DuelPhaseDisplay.End);
                if (phaseUpdates > 0) return;
            }
            displayedTurn = duel.Turn;
            ShowPhase(DuelPhaseDisplay.Draw);
            if (duel.CanDraw)
            {
                duel.Draw(out message);
                ClearDuelSelection();
            }
            if (presentation.Style.PlayAnimations) return;
        }
        if (displayedPhase == DuelPhaseDisplay.Draw)
        {
            ShowPhase(DuelPhaseDisplay.Standby);
            if (phaseUpdates > 0) return;
        }
        var phase = duel.Phase switch
        {
            DemoPhase.Battle => DuelPhaseDisplay.Battle,
            DemoPhase.End => DuelPhaseDisplay.End,
            _ => duel.IsMainPhase2 ? DuelPhaseDisplay.Main2 : DuelPhaseDisplay.Main1
        };
        if (displayedPhase != phase) ShowPhase(phase);
    }

    static void DrawPhasePanel(bool canChoosePhase)
    {
        for (int i = 0; i < phaseLabels.Length; i++)
        {
            var phase = (DuelPhaseDisplay)i;
            bool enabled = canChoosePhase && (phase switch
            {
                DuelPhaseDisplay.Battle => duel.CanBeginBattle,
                DuelPhaseDisplay.Main2 => duel.Phase == DemoPhase.Battle,
                DuelPhaseDisplay.End => duel.Phase == DemoPhase.Main,
                _ => false
            });
            var bounds = new Rectangle(218, 90 + i * 75, 28, 50);
            bool active = phase == displayedPhase;
            Raylib.DrawRectangleRec(bounds, active ? Gold : enabled && Hit(bounds) ? Color.DarkBlue : Panel);
            Raylib.DrawRectangleLinesEx(bounds, 1, active || enabled ? Gold : Color.Gray);
            Label(phaseLabels[i], (int)bounds.X + 4, (int)bounds.Y + 17, 14,
                active ? Color.Black : enabled ? Color.White : Color.LightGray);
            if (!enabled || !Hit(bounds) || !Raylib.IsMouseButtonPressed(MouseButton.Left)) continue;

            bool accepted = phase switch
            {
                DuelPhaseDisplay.Battle => duel.BeginBattle(out message),
                DuelPhaseDisplay.Main2 => duel.EndBattle(out message),
                _ => duel.EndTurn(out message)
            };
            if (!accepted) continue;
            ShowPhase(phase);
            ClearDuelSelection();
            if (phase == DuelPhaseDisplay.End) handPage = 0;
        }
    }

    static void DrawPhaseNotice()
    {
        if (phaseUpdates <= 0) return;
        Raylib.DrawRectangle(350, 278, 350, 44, Panel);
        Raylib.DrawRectangleLines(350, 278, 350, 44, Gold);
        var banner = graphics.Texture(DatGraphics.PhaseBanners[(int)displayedPhase]);
        Raylib.DrawTexture(banner, 525 - banner.Width / 2, 300 - banner.Height / 2, Color.White);
    }
}
