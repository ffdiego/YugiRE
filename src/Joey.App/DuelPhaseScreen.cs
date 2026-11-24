using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;

static partial class Game
{
    enum DuelPhaseDisplay { Draw, Standby, Main1, Battle, Main2, End }

    static readonly PresentationTickClock phaseClock = new();
    static DuelPhaseDisplay displayedPhase;
    static int displayedTurn, phaseUpdates;
    static int turnFlipElapsed;
    static bool drawNoticePending;
    static bool RedPhaseSide => displayedTurn != 0 && (displayedTurn & 1) == 0;
    static bool TurnFlipping => drawNoticePending && turnFlipElapsed < NativePhaseTurnFlip.Updates;
    static bool PhaseBusy => !duel.GameOver && (phaseUpdates > 0 ||
        drawNoticePending || displayedTurn != duel.Turn || displayedPhase is DuelPhaseDisplay.Draw or DuelPhaseDisplay.Standby);

    static void ResetDuelPhases()
    {
        displayedTurn = 0;
        displayedPhase = DuelPhaseDisplay.Draw;
        phaseUpdates = 0;
        turnFlipElapsed = NativePhaseTurnFlip.Updates;
        drawNoticePending = false;
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

    static DuelPhaseDisplay LogicalPhase() => duel.Phase switch
    {
        DemoPhase.Battle => DuelPhaseDisplay.Battle,
        DemoPhase.End => DuelPhaseDisplay.End,
        _ => duel.IsMainPhase2 ? DuelPhaseDisplay.Main2 : DuelPhaseDisplay.Main1
    };

    static void AdvanceDuelPhases(TimeSpan elapsed)
    {
        // A laboratory starts from an explicit checkpoint, often with an empty
        // deck. Presentation must not become a second command producer.
        // Until A07 owns phase transitions, mirror the logical phase only.
        if (labSession is not null)
        {
            displayedTurn = duel.Turn;
            displayedPhase = LogicalPhase();
            phaseUpdates = 0;
            drawNoticePending = false;
            turnFlipElapsed = NativePhaseTurnFlip.Updates;
            return;
        }
        if (TurnFlipping)
        {
            int updates = phaseClock.Advance(elapsed, presentation.Style);
            turnFlipElapsed += Math.Min(updates, NativePhaseTurnFlip.Updates - turnFlipElapsed);
            if (TurnFlipping) return;
        }
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
            bool animate = displayedTurn != 0 && presentation.Style.PlayAnimations;
            displayedTurn = duel.Turn;
            drawNoticePending = true;
            if (animate)
            {
                turnFlipElapsed = 0;
                phaseClock.Reset();
                return;
            }
        }
        if (drawNoticePending)
        {
            drawNoticePending = false;
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
        var phase = LogicalPhase();
        if (displayedPhase != phase) ShowPhase(phase);
    }

    static void DrawPhasePanel(bool canChoosePhase)
    {
        for (int i = 0; i < DatGraphics.PhaseButtons.Length; i++)
        {
            var phase = (DuelPhaseDisplay)i;
            bool enabled = canChoosePhase && (phase switch
            {
                DuelPhaseDisplay.Battle => duel.CanBeginBattle,
                DuelPhaseDisplay.Main2 => duel.Phase == DemoPhase.Battle,
                DuelPhaseDisplay.End => duel.Phase is DemoPhase.Main or DemoPhase.Battle,
                _ => false
            });
            var bounds = ToRectangle(DuelSurfaceLayout.PhaseButton(i));
            bool active = phase == displayedPhase;
            bool red = RedPhaseSide;
            const int frameWidth = DuelSurfaceLayout.PhaseWidth, frameHeight = DuelSurfaceLayout.PhaseHeight;
            string spriteName = DatGraphics.PhaseButtons[i];
            int sourceX = active || enabled && Hit(bounds) ? frameWidth : 0;
            int sourceY = red ? frameHeight : 0;
            if (TurnFlipping)
            {
                if (NativePhaseTurnFlip.Frame(i, turnFlipElapsed) is int frame)
                {
                    spriteName = (red ? DatGraphics.PhaseFlipToRed : DatGraphics.PhaseFlipToBlue)[i];
                    sourceX = frame * frameWidth;
                    sourceY = 0;
                }
                else
                {
                    sourceX = 0;
                    sourceY = red ? 0 : frameHeight;
                }
            }
            Raylib.DrawTexturePro(graphics.Texture(spriteName), new Rectangle(sourceX, sourceY, frameWidth, frameHeight),
                bounds, System.Numerics.Vector2.Zero, 0, Color.White);
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
        }
    }

    static void DrawPhaseNotice()
    {
        if (phaseUpdates <= 0) return;
        var banners = RedPhaseSide ? DatGraphics.PhaseBannersRed : DatGraphics.PhaseBannersBlue;
        string asset = banners[(int)displayedPhase];
        bool battle = displayedPhase == DuelPhaseDisplay.Battle;
        int elapsed = (battle ? NativePhaseNotice.BattleUpdates : NativePhaseNotice.StandardUpdates) - phaseUpdates;
        if (battle && NativePhaseNotice.BattleFrame(elapsed) is int frame)
            asset = (RedPhaseSide ? DatGraphics.BattleBannersRed : DatGraphics.BattleBannersBlue)[frame];
        var banner = graphics.Texture(asset);
        var (offset, opacity) = NativePhaseNotice.Motion(elapsed, battle, effectCosine);
        if (opacity == 0) return;
        int x = 525 - banner.Width / 2, y = 300 - banner.Height / 2;
        var tint = new Color(255, 255, 255, opacity);
        Raylib.DrawTexture(banner, x - offset, y, tint);
        if (offset != 0) Raylib.DrawTexture(banner, x + offset, y, tint);
    }
}
