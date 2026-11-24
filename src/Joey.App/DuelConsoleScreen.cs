using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;

static partial class Game
{
    static DuelConsoleSession? labSession;
    static DuelConsoleScript? labScript;
    static string[]? labOriginalLines;
    static DuelScriptStatus labStatus;
    static bool labConsoleOpen;
    static string labInput="";
    static readonly List<string> labLog=[];
    static int labTraceCursor;
    static string labLastWaiting="";
    static readonly System.Diagnostics.Stopwatch labWallClock = new();
    static long labLastProgressMs, labLastStallMs;

    static void StartConsoleLab(string[]? lines)
    {
        duel=DuelConsoleSession.EmptyLabDuel();
        // Reuse the fixed near/far board perspective and both hands; the CPU
        // controller is never stepped while labSession exists.
        alphaCpu=new(1);
        alphaBlocked=false;
        ResetDuelPresentation();
        labSession=new DuelConsoleSession(duel,new AppActionPresentationGate(),cards);
        queuedDuel=labSession.Queue;
        labOriginalLines=lines;
        labScript=lines is null ? null : new(labSession,lines);
        labStatus=lines is null ? DuelScriptStatus.Completed : DuelScriptStatus.Advanced;
        labConsoleOpen=lines is null;
        labInput="";
        labLog.Clear();
        labTraceCursor=0;
        labLastWaiting="";
        labWallClock.Restart();
        labLastProgressMs=labLastStallMs=0;
        message="Laboratory: F10 console; no CPU.";
        LabLog(message);
    }

    // Exercise the real App phase driver without loading DAT or opening
    // a raylib window. This is the regression for the first-frame deck-out.
    static int CheckConsoleStartup()
    {
        StartConsoleLab(null);
        var session = labSession!;
        string before = session.Execute("state").Message;
        for (int i = 0; i < 120; i++) AdvanceDuelPhases(TimeSpan.FromMilliseconds(17));
        if (duel.GameOver || PhaseBusy || session.Queue.IsBusy ||
            session.Queue.Trace.Count != 0 || session.Execute("state").Message != before ||
            displayedTurn != duel.Turn || displayedPhase != LogicalPhase())
        {
            Console.Error.WriteLine("Laboratory startup mutated state or blocked the scheduler.");
            return 1;
        }

        // Ensure the regular legacy mode still runs its automatic draw path.
        duel = DuelConsoleSession.EmptyLabDuel();
        ResetDuelPresentation();
        AdvanceDuelPhases(TimeSpan.FromMilliseconds(17));
        if (duel.Winner != 1)
        {
            Console.Error.WriteLine("Legacy phase driver no longer performs its automatic draw.");
            return 1;
        }
        if (!CheckQueuedAutomaticDraw(new DuelLookAndFeel()) ||
            !CheckQueuedAutomaticDraw(new InstantLookAndFeel()))
        {
            Console.Error.WriteLine("Queued automatic draw phase regression failed.");
            return 1;
        }
        if (!CheckQueuedCombatPresentation())
        {
            Console.Error.WriteLine("Queued combat presentation regression failed.");
            return 1;
        }
        Console.WriteLine("Laboratory, queued automatic draw and combat presentation regressions passed.");
        return 0;
    }

    static bool CheckQueuedAutomaticDraw(DuelLookAndFeel style)
    {
        Card card = new(24, 24, "Draw fixture", "", (3u << 20) | (4u << 25));
        Card inert = new(25, 25, "Inert CPU fixture", "", 22u << 20);
        int handLimit = DemoDuel.EndTurnHandLimit;
        duel = DemoDuel.FromPartialObservation(0, Enumerable.Repeat(card, handLimit).ToArray(),
            new Card?[5], new Card?[5], 8000,
            side0Deck: [card], side1Deck: [inert]);
        alphaCpu = new(1);
        alphaBlocked = false;
        presentation.SetStyle(style);
        ResetDuelPresentation();
        var queue = duel.CreatePrimitiveActionQueue(new AppActionPresentationGate());
        queuedDuel = queue;
        var elapsed = TimeSpan.FromMilliseconds(17);

        void AdvanceFrame()
        {
            AdvanceCardEffects(elapsed);
            AdvanceDuelPhases(elapsed);
            AdvanceQueuedDuel();
        }

        void AdvancePhases()
        {
            for (int i = 0; i < 500 && (PhaseBusy || queue.IsBusy); i++) AdvanceFrame();
        }

        bool Failed(string stage)
        {
            Console.Error.WriteLine($"Automatic draw {style.GetType().Name} failed at {stage}. " +
                $"Turn {duel.Turn}, display {displayedPhase}, updates {phaseUpdates}, " +
                $"hand {duel.Hand.Count}, queued {queue.IsBusy}, motion {MotionBusy}, message {message}");
            return false;
        }

        AdvanceDuelPhases(elapsed);
        if (!queuedTurnDraw || duel.Hand.Count != handLimit || displayedPhase != DuelPhaseDisplay.Draw)
            return Failed("admission");
        AdvanceQueuedDuel();
        if (duel.Hand.Count != handLimit + 1 || queue.IsBusy != style.PlayAnimations)
            return Failed("execution");
        if (style.PlayAnimations)
        {
            if (!CardEffectsBusy || queue.Active?.Command is not TurnDrawCommand)
                return Failed("transfer");
            AdvanceFrame();
            if (displayedPhase != DuelPhaseDisplay.Draw || duel.Hand.Count != handLimit + 1)
                return Failed("gate");
        }
        AdvancePhases();
        if (PhaseBusy || queue.IsBusy || duel.Hand.Count != handLimit + 1 || displayedPhase != DuelPhaseDisplay.Main1 ||
            !TryChooseDuelPhase(DuelPhaseDisplay.End) || displayedPhase != DuelPhaseDisplay.Main1)
            return Failed("first Main Phase");
        AdvancePhases();
        if (PhaseBusy || queue.IsBusy || displayedPhase != DuelPhaseDisplay.End || !duel.HasQueuedEndDiscard ||
            duel.PendingEndDiscards != 1 || duel.ActivePlayer != 0) return Failed("End Phase");
        UseHandAction(handLimit);
        if (!queue.IsBusy || duel.Hand.Count != handLimit + 1) return Failed("discard admission");
        AdvancePhases();
        if (PhaseBusy || queue.IsBusy || duel.ActivePlayer != 1 || duel.Hand.Count != 1 ||
            duel.HandFor(0).Count != handLimit) return Failed("second Main Phase");
        if (alphaCpu.Step(duel, queueTurnActions: true).Action != CpuAction.BeginBattle)
            return Failed("CPU Battle admission");
        AdvancePhases();
        if (PhaseBusy || queue.IsBusy || displayedPhase != DuelPhaseDisplay.Battle ||
            alphaCpu.Step(duel, queueTurnActions: true).Action != CpuAction.EndBattle)
            return Failed("CPU Main2 admission");
        AdvancePhases();
        if (PhaseBusy || queue.IsBusy || displayedPhase != DuelPhaseDisplay.Main2 ||
            alphaCpu.Step(duel, queueTurnActions: true).Action != CpuAction.EndTurn)
            return Failed("CPU End Phase admission");
        AdvancePhases();
        var draws = queue.Trace.Where(item => item.Stage == ActionTraceStage.Completed &&
            item.Entry.Command is TurnDrawCommand).Select(item => item.Entry.Origin);
        int cpuPhases = queue.Trace.Count(item => item.Stage == ActionTraceStage.Completed &&
            item.Entry.Origin == ActionOrigin.Cpu && item.Entry.Command is ChangeBattlePhaseCommand or EndTurnCommand &&
            item.Result?.Outcome == CommandOutcome.Applied);
        if (duel.Winner != 1 || queue.IsBusy || alphaBlocked || cpuPhases != 3 ||
            !draws.SequenceEqual(new[] { ActionOrigin.Player, ActionOrigin.Cpu, ActionOrigin.Player }))
            return Failed("deck out");
        return true;
    }

    // The real App gate and existing sword cue run without loading DAT or
    // creating a window. No claim about pixel fidelity from this regression.
    static bool CheckQueuedCombatPresentation()
    {
        var elapsed = TimeSpan.FromMilliseconds(17);
        foreach (bool targeted in new[] { false, true })
        {
            ActionTraceEntry[]? reference = null;
            foreach (DuelLookAndFeel style in new DuelLookAndFeel[] { new(), new InstantLookAndFeel() })
            {
                Card attackerCard = new(24, 24, "Attack fixture", "", (3u << 20) | (4u << 25) | (140u << 9));
                Card defenderCard = new(1351, 1351, "Defense fixture", "", (3u << 20) | (4u << 25) | (100u << 9));
                duel = DemoDuel.FromPartialObservation(1, [], targeted ? [null, null, null, defenderCard, null] : new Card?[5],
                    [null, null, attackerCard, null, null], 8000);
                alphaCpu = new(1);
                alphaBlocked = false;
                presentation.SetStyle(style);
                ResetDuelPresentation();
                labSession = new DuelConsoleSession(duel, new AppActionPresentationGate());
                var queue = labSession.Queue;
                queuedDuel = queue;
                AdvanceDuelPhases(elapsed);
                if (!labSession.Execute("battle begin").Enqueued) return false;
                AdvanceQueuedDuel();
                AdvanceDuelPhases(elapsed);
                if (queue.IsBusy || !labSession.Execute(targeted ? "attack 2 3" : "attack 2 direct").Enqueued)
                    return false;
                AdvanceQueuedDuel();
                if (duel.Life[0] != 8000 || targeted && duel.Monsters[0, 3] is null)
                    return false;
                if (style.PlayAnimations)
                {
                    if (attackCue is not { AlreadyAimed: false } cue ||
                        cue.Target != (targeted ? NativeDuelLayout.MonsterCenter(3, true) : NativeAttackFrame.DirectTarget(false)) ||
                        queue.Active?.Command is not (DeclareDirectAttackCommand or DeclareTargetedAttackCommand))
                        return false;
                    // Without advancing the clock, repeated gate checks must
                    // neither restart the cue nor allow destruction/damage.
                    for (int i = 0; i < 4; i++) AdvanceQueuedDuel();
                    if (!ReferenceEquals(cue, attackCue) || attackUpdates != 0 ||
                        duel.Life[0] != 8000 || targeted && duel.Monsters[0, 3] is null)
                        return false;
                }
                else if (attackCue is not null) return false;

                for (int frame = 0; frame < 600 && queue.IsBusy; frame++)
                {
                    if (CanPlayCardEffects) AdvanceCardEffects(elapsed);
                    AdvanceDuelPhases(elapsed);
                    AdvanceAlpha(elapsed);
                    AdvanceQueuedDuel();
                    AdvanceLifePresentation(elapsed);
                }
                if (queue.IsBusy || alphaBlocked || attackCue is not null ||
                    duel.Life[0] != (targeted ? 7600 : 6600) ||
                    targeted && (duel.Monsters[0, 3] is not null || duel.Graveyard(0).Count != 1))
                    return false;
                var trace = queue.Trace.ToArray();
                if (reference is not null && !trace.SequenceEqual(reference)) return false;
                reference = trace;
            }
        }
        return true;
    }

    static void LabLog(string text)
    {
        if(string.IsNullOrWhiteSpace(text)) return;
        Console.WriteLine(text);
        labLog.AddRange(text.Split('\n'));
        if(labLog.Count>100) labLog.RemoveRange(0,labLog.Count-100);
    }

    static void AdvanceConsoleLab()
    {
        if(labSession is not { } session) return;
        if(Raylib.IsKeyPressed(KeyboardKey.F10)) labConsoleOpen=!labConsoleOpen;
        var trace=session.Queue.Trace;
        for(int i=labTraceCursor;i<trace.Count;i++)
        {
            var entry=trace[i];
            if(entry.Stage==ActionTraceStage.Executed)
                LabLog($"{entry.Entry.Sequence}: {entry.Entry.Command.GetType().Name} {entry.Result?.Outcome} {entry.Result?.Detail}");
            if(entry.Stage is ActionTraceStage.Started or ActionTraceStage.Completed)
                Console.WriteLine($"[lab timing] ms={labWallClock.ElapsedMilliseconds} seq={entry.Entry.Sequence} stage={entry.Stage} command={entry.Entry.Command.GetType().Name}");
        }
        long now=labWallClock.ElapsedMilliseconds;
        if(labTraceCursor!=trace.Count || visualReviewPaused || session.HasPendingChoice)
            labLastProgressMs=labLastStallMs=now;
        labTraceCursor=trace.Count;
        bool scriptRunning=labScript is not null && labStatus is not (DuelScriptStatus.Completed or DuelScriptStatus.Failed);
        if((scriptRunning || session.Queue.IsBusy) && !visualReviewPaused && !session.HasPendingChoice &&
            now-labLastProgressMs>=5000 && now-labLastStallMs>=5000)
        {
            labLastStallMs=now;
            Console.WriteLine($"[lab wait] ms={now} idleMs={now-labLastProgressMs} active={session.Queue.Active?.Sequence} pending={session.Queue.PendingCount} motion={MotionBusy} phase={PhaseBusy} flight={flight is not null} attack={attackCue is not null} activation={ActivationBusy} cardEffects={CardEffectsBusy} life={LifePresentationBusy} blocked={alphaBlocked} fault={session.Queue.Fault is not null}");
        }
        if(labScript is not { } script || labStatus is DuelScriptStatus.Completed or DuelScriptStatus.Failed ||
            visualReviewPaused || MotionBusy || PhaseBusy) return;
        var step=script.Advance();
        labStatus=step.Status;
        if(step.Status==DuelScriptStatus.WaitingForDecision)
        {
            if(labLastWaiting!=step.Message) LabLog(step.Message);
            labLastWaiting=step.Message;
            return;
        }
        labLastWaiting="";
        if(step.Line is int n)
        {
            labLastProgressMs=labLastStallMs=now;
            LabLog($"[script line {n}]");
        }
        LabLog(step.Message);
        if(step.Status is DuelScriptStatus.Completed or DuelScriptStatus.Failed)
            message=step.Status==DuelScriptStatus.Completed ? "Laboratory script completed. F10 console." : "Script stopped. F10 for log.";
    }

    // Debug-only overlay, not a claim of a native Joey screen.
    static void DrawConsoleLab()
    {
        if(labSession is not { } session) return;
        if(!labConsoleOpen)
        {
            Label($"LAB | F10 console | {labStatus}", 264, 10, 12, Gold);
            return;
        }
        Raylib.DrawRectangle(245,80,545,420,new Color(7,15,28,245));
        Raylib.DrawRectangleLines(245,80,545,420,Gold);
        Label("LAB CONSOLE — F10 hide",255,90,16,Gold);
        int y=118;
        foreach(string line in labLog.TakeLast(17))
        {
            string display=line;
            while(display.Length>0 && MeasureLabel(display,12)>522) display=display[..^1];
            Label(display,255,y,12);
            y+=18;
        }
        bool manual=labScript is null || labStatus is DuelScriptStatus.Completed or DuelScriptStatus.Failed or DuelScriptStatus.WaitingForDecision;
        int ch;while((ch=Raylib.GetCharPressed())>0)
            if(manual && ch>=32 && ch<127 && labInput.Length<256) labInput+=(char)ch;
        if(manual && Raylib.IsKeyPressed(KeyboardKey.Backspace) && labInput.Length>0) labInput=labInput[..^1];
        if(manual && Raylib.IsKeyPressed(KeyboardKey.Enter) && labInput.Length>0)
        {
            var line=labInput.Trim();labInput="";
            LabLog("> "+line);
            if(labStatus==DuelScriptStatus.WaitingForDecision && !session.AllowsPendingChoiceInput(line))
                LabLog("Answer the pending decision before resuming the script.");
            else if(MotionBusy || PhaseBusy || visualReviewPaused)
                LabLog("Presentation busy or paused; retry when ready.");
            else
            {
                var result=session.Execute(line);
                LabLog(result.Message);
            }
        }
        string prompt=manual ? "> "+labInput : "Script running; F6 pauses presentation.";
        while(prompt.Length>0 && MeasureLabel(prompt,14)>522) prompt=prompt[1..];
        Label(prompt,255,453,14,Gold);
        Label($"Queue: {session.Queue.PendingCount} | active: {session.Queue.Active?.Sequence.ToString() ?? "-"} | {labStatus}",255,476,12);
    }
}
