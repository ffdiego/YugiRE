using Joey.Core;

namespace Joey.Lab;

public static class ActionTraceExport
{
    public static ActionTraceDocument Capture(ActionQueue queue, string scope) => ActionTraceJson.Capture(queue, scope);
    public static string Serialize(ActionTraceDocument document) => ActionTraceJson.Serialize(document);

    // A real rule-produced trace. Not a claim of native parity; synthetic
    // catalogue entries for a deterministic pilot; no original DAT required.
    public static int RunBattlePilot(string[] args, TextWriter output, TextWriter error)
    {
        if(args.Length!=0) { error.WriteLine("Usage: action-trace-battle-pilot"); return 2; }
        Card monster=new(24,24,"Fixture monster","",(3u<<20)|(4u<<25)|(140u<<9));
        var duel=DemoDuel.FromPartialObservation(1,[],new Card?[5],new Card?[5],8000,side1Deck:[monster]);
        var console = new DuelConsoleSession(duel);
        bool RunScript(params string[] lines)
        {
            var script = new DuelConsoleScript(console, lines);
            for (int i = 0; i < 128; i++)
            {
                console.Queue.Tick();
                switch (script.Advance().Status)
                {
                    case DuelScriptStatus.Completed: return true;
                    case DuelScriptStatus.Failed:
                    case DuelScriptStatus.WaitingForDecision: return false;
                }
            }
            return false;
        }

        if (!RunScript("draw 1 1", "summon 0 2", "battle begin", "attack 2 direct", "battle end"))
        {
            error.WriteLine("Synthetic direct battle pilot did not complete.");
            return 1;
        }
        output.WriteLine(Serialize(Capture(console.Queue, "synthetic-rule-pilot-draw-summon-direct-attack")));
        return 0;
    }

    public static int RunPilot(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length != 0) { error.WriteLine("Usage: action-trace-pilot"); return 2; }
        Card monster = new(24,24,"Fixture monster","",(3u<<20)|(4u<<25));
        Card upstart = new(1065,1065,"Upstart Goblin","",22u<<20);
        Card sparks = new(342,342,"Sparks","",22u<<20);
        var duel = DemoDuel.FromPartialObservation(0,[upstart,sparks,monster],new Card?[5],new Card?[5],8000,
            side0Deck:[monster]);
        var queue = duel.CreatePrimitiveActionQueue();
        bool Drain()
        {
            for (int i=0;i<128 && queue.IsBusy;i++) queue.Tick();
            return !queue.IsBusy;
        }
        if (!duel.TryQueueDrawSpell(DuelZone.Hand,0,out _) || !Drain() ||
            !duel.TryQueueLifePointSpell(DuelZone.Hand,0,out _) || !Drain() ||
            !duel.TryQueueNormalPlacement(0,0,false,out _) || !Drain())
        { error.WriteLine("Synthetic action trace pilot did not complete."); return 1; }
        output.WriteLine(Serialize(Capture(queue,"synthetic-rule-pilot-upstart-sparks-summon")));
        return 0;
    }
}
