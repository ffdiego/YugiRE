using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;

static partial class Game
{
    static DuelConsoleSession? labSession, liveConsoleSession;
    static DuelConsoleSession? ConsoleSession => labSession ?? liveConsoleSession;
    static float consoleHeight;
    static bool consoleCapturedThisFrame;
    static bool ConsoleCapturesInput => screen == "duel" && (labConsoleOpen || consoleHeight > 0 || consoleCapturedThisFrame);
    static readonly List<string> consoleHistory=[];
    static int consoleHistoryIndex;
    static int consoleScroll;
    const int ConsolePanelHeight=330;

    static bool ConsoleTogglePressed() => Raylib.IsKeyPressed(KeyboardKey.Apostrophe) ||
        Raylib.IsKeyPressed(KeyboardKey.Grave) || Raylib.IsKeyPressed(KeyboardKey.F10);

    static void ResetConsoleLog()
    {
        labInput="";labLog.Clear();consoleHistory.Clear();consoleHistoryIndex=0;consoleScroll=0;
        labTraceCursor=0;labLastWaiting="";
        labWallClock.Restart();labLastProgressMs=labLastStallMs=0;
    }

    static void ReadDuelConsoleToggle()
    {
        consoleCapturedThisFrame=false;
        bool toggle=ConsoleTogglePressed();
        bool close=labConsoleOpen && Raylib.IsKeyPressed(KeyboardKey.Escape);
        if(!toggle&&!close)return;
        consoleCapturedThisFrame=true;
        if(labConsoleOpen)labConsoleOpen=false;
        else
        {
            if(ConsoleSession is null)
            {
                // Lazy attachment: leave the legacy path untouched until
                // the operator explicitly opens the developer console.
                liveConsoleSession=DuelConsoleSession.Attach(duel,new AppActionPresentationGate(),cards);
                queuedDuel=liveConsoleSession.Queue;
                labStatus=DuelScriptStatus.Completed;
                ResetConsoleLog();
                labTraceCursor=queuedDuel.Trace.Count;
                LabLog("Live duel console. CPU input paused while open. Type help.");
            }
            labConsoleOpen=true;
        }
        // Discard the toggle character; never insert it into a command.
        while(Raylib.GetCharPressed()>0) { }
    }
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
        ResetConsoleLog();
        message="Laboratory: apostrophe/F10 console; no CPU.";
        LabLog(message);
    }

    // Exercise the real App phase driver without loading DAT or opening
    // a raylib window. This is the regression for the first-frame deck-out.
    static int CheckConsoleStartup()
    {
        if (SelectPresentationStyle([]) is not FastLookAndFeel ||
            SelectPresentationStyle(["--fast"]).UpdatesPerSecond != 60 ||
            SelectPresentationStyle(["--classic"]).GetType() != typeof(DuelLookAndFeel) ||
            SelectPresentationStyle(["--instant", "--fast"]) is not InstantLookAndFeel ||
            SelectPresentationStyle(["--mute-joey"]).PlayJoeyVoice)
        {
            Console.Error.WriteLine("Presentation CLI profile regression.");
            return 1;
        }
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
            Console.Error.WriteLine("Queued player and console combat presentation regression failed.");
            return 1;
        }
        if (!CheckQueuedCpuAttack(targeted: false) || !CheckQueuedCpuAttack(targeted: true))
        {
            Console.Error.WriteLine("Queued CPU combat presentation regression failed.");
            return 1;
        }
        if (!CheckBanishSummonSelection())
        {
            Console.Error.WriteLine("Queued inherent summon selection regression failed.");
            return 1;
        }
        Console.WriteLine("Laboratory, queued automatic draw, combat presentation and inherent summon selection regressions passed.");
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
        foreach (int side in new[] { 0, 1 })
        foreach (var (targeted, playerInput) in new[] { (false, false), (true, false), (false, true), (true, true) })
        {
            ActionTraceEntry[]? reference = null;
            foreach (DuelLookAndFeel style in new DuelLookAndFeel[] { new(), new InstantLookAndFeel() })
            {
                Card attackerCard = new(24, 24, "Attack fixture", "", (3u << 20) | (4u << 25) | (140u << 9));
                Card defenderCard = new(1351, 1351, "Defense fixture", "", (3u << 20) | (4u << 25) | (100u << 9));
                Card?[] attackers = [null, null, attackerCard, null, null];
                Card?[] defenders = targeted ? [null, null, null, defenderCard, null] : new Card?[5];
                if (targeted)
                {
                    attackers[0] = attackerCard;
                    defenders[1] = defenderCard;
                }
                // Start on turn 2, then advance legally to turn 3 for the near-side case.
                int enemy = 1 - side;
                duel = DemoDuel.FromPartialObservation(1, [], side == 0 ? attackers : defenders,
                    side == 1 ? attackers : defenders, 8000);
                alphaCpu = new(1);
                alphaBlocked = false;
                presentation.SetStyle(style);
                ResetDuelPresentation();
                labSession = new DuelConsoleSession(duel, new AppActionPresentationGate());
                var queue = labSession.Queue;
                queuedDuel = queue;
                AdvanceDuelPhases(elapsed);
                if (side == 0)
                {
                    if (!labSession.Execute("turn end").Enqueued) return false;
                    for (int frame = 0; frame < 120 && queue.IsBusy; frame++)
                    {
                        AdvanceDuelPhases(elapsed);
                        AdvanceQueuedDuel();
                    }
                    AdvanceDuelPhases(elapsed);
                    if (queue.IsBusy || duel.ActivePlayer != side || duel.Turn != 3) return false;
                }
                if (!labSession.Execute("battle begin").Enqueued) return false;
                AdvanceQueuedDuel();
                AdvanceDuelPhases(elapsed);
                if (queue.IsBusy) return false;
                if (playerInput)
                {
                    attacker = 2;
                    CommitHumanAttack(targeted ? 3 : -1);
                    if (!queue.IsBusy || attackCue is not null || duel.Life[enemy] != 8000) return false;
                }
                else if (!labSession.Execute(targeted ? "attack 2 3" : "attack 2 direct").Enqueued)
                    return false;
                AdvanceQueuedDuel();
                if (duel.Life[enemy] != 8000 || targeted && duel.Monsters[enemy, 3] is null)
                    return false;
                if (style.PlayAnimations)
                {
                    if (attackCue is not { } cue || cue.AlreadyAimed != (playerInput && targeted && side == 0) ||
                        cue.Target != (targeted ? NativeDuelLayout.MonsterCenter(3, enemy == 0) : NativeAttackFrame.DirectTarget(side == 0)) ||
                        queue.Active?.Command is not (DeclareDirectAttackCommand or DeclareTargetedAttackCommand))
                        return false;
                    // Without advancing the clock, repeated gate checks must
                    // neither restart the cue nor allow destruction/damage.
                    for (int i = 0; i < 4; i++) AdvanceQueuedDuel();
                    if (!ReferenceEquals(cue, attackCue) || attackUpdates != 0 ||
                        duel.Life[enemy] != 8000 || targeted && duel.Monsters[enemy, 3] is null)
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
                if (queue.IsBusy || alphaBlocked || attackCue is not null || duel.Life[side] != 8000 ||
                    duel.Life[enemy] != (targeted ? 7600 : 6600) ||
                    targeted && (duel.Monsters[enemy, 3] is not null || duel.Graveyard(enemy).Count != 1 ||
                        duel.Monsters[side, 0] != attackerCard || duel.Monsters[enemy, 1] != defenderCard))
                    return false;
                var trace = queue.Trace.ToArray();
                if (playerInput && !trace.Any(entry => entry.Stage == ActionTraceStage.Completed &&
                    entry.Entry.Command is (DeclareDirectAttackCommand or DeclareTargetedAttackCommand) &&
                    entry.Entry.Origin == ActionOrigin.Player &&
                    entry.Result?.Outcome == CommandOutcome.Applied)) return false;
                if (reference is not null && !trace.SequenceEqual(reference)) return false;
                reference = trace;
            }
        }
        return true;
    }

    static bool CheckQueuedCpuAttack(bool targeted)
    {
        Card card = new(24, 24, "CPU attack fixture", "", (3u << 20) | (4u << 25) | (140u << 9));
        Card defender = new(1351, 1351, "CPU defense fixture", "", (3u << 20) | (4u << 25) | (100u << 9));
        Card other = new(24, 24, "Other monster fixture", "", (3u << 20) | (4u << 25) | (200u << 9));
        Card?[] enemies = targeted ? [null, other, null, defender, null] : new Card?[5];
        DemoDuel Setup() => DemoDuel.FromPartialObservation(1, [], enemies,
            [null, null, card, null, null], 8000);

        bool Failed(string stage)
        {
            Console.Error.WriteLine($"Queued CPU attack (targeted={targeted}) failed at {stage}.");
            return false;
        }

        var headless = Setup();
        if (!headless.BeginBattle(out _)) return Failed("headless phase");
        var headlessQueue = headless.CreatePrimitiveActionQueue();
        if (new CpuDuelController(1).Step(headless, queueTurnActions: true).Action != CpuAction.Attack)
            return Failed("headless admission");
        for (int tick = 0; tick < 10 && headlessQueue.IsBusy; tick++) headlessQueue.Tick();
        if (headlessQueue.IsBusy || headless.Life[0] != (targeted ? 7600 : 6600) ||
            targeted && (headless.Graveyard(0).Count != 1 || headless.Monsters[0, 1] != other))
            return Failed("headless result");

        var elapsed = TimeSpan.FromMilliseconds(17);
        foreach (DuelLookAndFeel style in new DuelLookAndFeel[] { new(), new InstantLookAndFeel() })
        {
            duel = Setup();
            if (!duel.BeginBattle(out _)) return Failed("App phase");
            alphaCpu = new(1);
            alphaBlocked = false;
            cpuDelay = TimeSpan.Zero;
            presentation.SetStyle(style);
            ResetDuelPresentation();
            displayedTurn = duel.Turn;
            displayedPhase = LogicalPhase();
            var queue = duel.CreatePrimitiveActionQueue(new AppActionPresentationGate());
            queuedDuel = queue;

            AdvanceAlpha(elapsed);
            if (!queue.IsBusy || attackCue is not null || duel.Life[0] != 8000) return Failed("App admission or early damage");
            AdvanceQueuedDuel();
            if (duel.Life[0] != 8000) return Failed("App admission or early damage");
            if (style.PlayAnimations)
            {
                if (attackCue is not { AlreadyAimed: false, NearSide: false } cue ||
                    queue.Active?.Command is not (DeclareDirectAttackCommand or DeclareTargetedAttackCommand)) return Failed("cue start");
                for (int tick = 0; tick < 4; tick++) AdvanceQueuedDuel();
                if (!ReferenceEquals(cue, attackCue) || attackUpdates != 0 || duel.Life[0] != 8000)
                    return Failed("cue observed twice or damage before gate");
            }
            else if (attackCue is not null) return Failed("Instant cue");

            for (int frame = 0; frame < 600 && queue.IsBusy; frame++)
            {
                if (CanPlayCardEffects) AdvanceCardEffects(elapsed);
                AdvanceDuelPhases(elapsed);
                AdvanceAlpha(elapsed);
                AdvanceQueuedDuel();
                AdvanceLifePresentation(elapsed);
            }
            if (queue.IsBusy || alphaBlocked || attackCue is not null || !duel.Life.SequenceEqual(headless.Life) ||
                alphaCpu.ActionsThisTurn != 1 || !queue.Trace.SequenceEqual(headlessQueue.Trace) ||
                !duel.Graveyard(0).SequenceEqual(headless.Graveyard(0)))
                return Failed($"App result and trace parity in {style.GetType().Name}");
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
        if(ConsoleSession is not { } session) return;
        var trace=session.Queue.Trace;
        for(int i=labTraceCursor;i<trace.Count;i++)
        {
            var entry=trace[i];
            if(entry.Stage==ActionTraceStage.Executed)
                LabLog($"{entry.Entry.Sequence}: {entry.Entry.Command.GetType().Name} {entry.Result?.Outcome} {entry.Result?.Detail}");
            if(labSession is not null && (entry.Stage is ActionTraceStage.Started or ActionTraceStage.Completed))
                Console.WriteLine($"[lab timing] ms={labWallClock.ElapsedMilliseconds} seq={entry.Entry.Sequence} stage={entry.Stage} command={entry.Entry.Command.GetType().Name}");
        }
        long now=labWallClock.ElapsedMilliseconds;
        if(labTraceCursor!=trace.Count || visualReviewPaused || session.HasPendingChoice)
            labLastProgressMs=labLastStallMs=now;
        labTraceCursor=trace.Count;
        bool scriptRunning=labScript is not null && labStatus is not (DuelScriptStatus.Completed or DuelScriptStatus.Failed);
        if(labSession is not null && (scriptRunning || session.Queue.IsBusy) && !visualReviewPaused && !session.HasPendingChoice &&
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

    // Retractable developer overlay. UI time is independent of native
    // animation speed and of the Core scheduler.
    static void DrawConsoleLab()
    {
        if(ConsoleSession is not { } session)return;
        float target=labConsoleOpen?ConsolePanelHeight:0;
        float distance=Math.Clamp(Raylib.GetFrameTime(),0f,0.1f)*1800f;
        consoleHeight=consoleHeight<target?Math.Min(target,consoleHeight+distance):
            Math.Max(target,consoleHeight-distance);
        int height=(int)consoleHeight;
        if(height==0)return;
        int top=height-ConsolePanelHeight;
        Raylib.DrawRectangle(0,top,800,ConsolePanelHeight,new Color(7,15,28,238));
        Raylib.DrawRectangle(0,top+ConsolePanelHeight-2,800,2,Gold);
        Label(labSession is null?"DUEL CONSOLE | LIVE":"DUEL CONSOLE | LAB",12,top+8,16,Gold);
        Label("apostrophe / F10 / Esc",578,top+8,12);
        bool manual=labScript is null || labStatus is DuelScriptStatus.Completed or DuelScriptStatus.Failed or DuelScriptStatus.WaitingForDecision;
        if(labConsoleOpen)
        {
            if(Raylib.IsKeyPressed(KeyboardKey.PageUp))consoleScroll=Math.Min(Math.Max(0,labLog.Count-13),consoleScroll+8);
            if(Raylib.IsKeyPressed(KeyboardKey.PageDown))consoleScroll=Math.Max(0,consoleScroll-8);
            if(manual&&Raylib.IsKeyPressed(KeyboardKey.Up)&&consoleHistoryIndex>0)
                labInput=consoleHistory[--consoleHistoryIndex];
            if(manual&&Raylib.IsKeyPressed(KeyboardKey.Down)&&consoleHistoryIndex<consoleHistory.Count)
            {
                consoleHistoryIndex++;
                labInput=consoleHistoryIndex==consoleHistory.Count?"":consoleHistory[consoleHistoryIndex];
            }
            int ch;while((ch=Raylib.GetCharPressed())>0)
                if(manual && ch>=32 && ch<127 && labInput.Length<256) labInput+=(char)ch;
            if(manual && Raylib.IsKeyPressed(KeyboardKey.Backspace) && labInput.Length>0) labInput=labInput[..^1];
            if(manual && Raylib.IsKeyPressed(KeyboardKey.Enter) && labInput.Length>0)
            {
                var line=labInput.Trim();labInput="";
                if(line.Length>0)
                {
                    consoleHistory.Add(line);
                    if(consoleHistory.Count>100)consoleHistory.RemoveAt(0);
                    consoleHistoryIndex=consoleHistory.Count;consoleScroll=0;
                    LabLog("> "+line);
                    string op=line.Split((char[]?)null,2,StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
                    bool readOrDecision=op is "state" or "diff" or "trace" or "help" or "choices" or "decision";
                    if(labStatus==DuelScriptStatus.WaitingForDecision && !session.AllowsPendingChoiceInput(line))
                        LabLog("Answer the pending decision before resuming the script.");
                    else if(!readOrDecision && (MotionBusy || PhaseBusy || visualReviewPaused))
                        LabLog("Presentation busy or paused; retry when ready.");
                    else
                    {
                        var result=session.Execute(line);
                        LabLog(result.Message);
                        if(result.Enqueued)ClearDuelSelection();
                    }
                }
            }
        }
        int y=top+34;
        foreach(string line in labLog.Take(Math.Max(0,labLog.Count-consoleScroll)).TakeLast(13))
        {
            string display=line;
            while(display.Length>0 && MeasureLabel(display,14)>776)display=display[..^1];
            Label(display,12,y,14);y+=18;
        }
        string prompt=manual?"> "+labInput:"Script running; F6 pauses presentation.";
        while(prompt.Length>0 && MeasureLabel(prompt,16)>776)prompt=prompt[1..];
        Label(prompt,12,top+278,16,Gold);
        Label($"Queue: {session.Queue.PendingCount} | active: {session.Queue.Active?.Sequence.ToString() ?? "-"} | Up/Down history | PgUp/PgDn log",12,top+306,12);
    }
}
