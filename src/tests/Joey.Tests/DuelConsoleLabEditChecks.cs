using Joey.Core;
using Joey.Lab;

internal static class DuelConsoleLabEditChecks
{
    public static void Run(Action<bool,string> check)
    {
        var monster = DuelConsoleRunner.FixtureCard();
        var sangan = monster with { Id = 47 };
        var spell = new Card(336,336,"Fixture Spell","",22u << 20);
        var d = DemoDuel.FromPartialObservation(0, [], new Card?[5], new Card?[5], 8000);
        var console = new DuelConsoleSession(d, cards: [monster, sangan, spell]);
        void Drain()
        {
            for (int i=0; i<100 && console.Queue.IsBusy; i++) console.Queue.Tick();
            if (console.Queue.IsBusy) throw new Exception("Lab edit did not drain.");
        }
        CommandOutcome Run(string command)
        {
            if (!console.Execute(command).Enqueued) throw new Exception("Expected admission: " + command);
            Drain();
            return console.Queue.Trace.Last().Result!.Outcome;
        }
        check(!console.Execute("spawn 999 0 hand").Success &&
              !console.Execute("spawn 24 0 monster 5").Success &&
              !console.Execute("move 0 hand 0 deck").Success &&
              console.Queue.Trace.Count == 0, "A04.2 invalid ID/slot/source rejected without mutation");
        check(console.Execute("spawn 24 0 hand").Enqueued && d.Hand.Count == 0,
              "A04.2 spawn admission is not mutation");
        Drain();
        check(d.TryCaptureCardTarget(0,DuelZone.Hand,0,out var first), "A04.2 spawn has occurrence");
        check(Run("spawn 24 0 hand") == CommandOutcome.Applied &&
            d.TryCaptureCardTarget(0,DuelZone.Hand,1,out var second) && second.Occurrence != first.Occurrence,
            "A04.2 duplicate resource creates distinct physical copies");
        check(Run("move 0 hand 0 deck") == CommandOutcome.Applied &&
            d.TryCaptureCardTarget(0,DuelZone.Deck,0,out var inDeck) && inDeck.Occurrence == first.Occurrence,
            "A04.2 move preserves occurrence");
        check(Run("move 0 deck 0 monster 2 set") == CommandOutcome.Applied &&
            d.TryCaptureCardTarget(0,DuelZone.Monster,2,out var inField) && inField.Occurrence == first.Occurrence &&
            d.MonsterFaceDown[0,2] && d.DefensePosition[0,2], "A04.2 setup face-down monster");
        check(Run("spawn 47 0 monster 2") == CommandOutcome.Rejected &&
            Run("spawn 336 0 monster 3") == CommandOutcome.Rejected &&
            d.Monsters[0,2] == monster && d.Monsters[0,3] is null, "A04.2 occupied and incompatible destination reject");
        check(Run("move 0 monster 2 grave") == CommandOutcome.Applied &&
            d.TryCaptureCardTarget(0,DuelZone.Graveyard,0,out var grave) && grave.Occurrence == first.Occurrence,
            "A04.2 move to grave preserves ID");
        check(Run("move 0 grave 0 hand") == CommandOutcome.Applied && d.Hand.Count == 2 &&
            d.TryCaptureCardTarget(0,DuelZone.Hand,1,out var back) && back.Occurrence == first.Occurrence,
            "A04.2 move from grave preserves ID and hand order");
        check(Run("spawn 47 1 monster 0") == CommandOutcome.Applied &&
            Run("move 1 monster 0 grave") == CommandOutcome.Applied && d.PendingCardSelection is null,
            "A04.2 setup move is not a gameplay destruction or graveyard trigger");
        check(Run("spawn 336 1 spell 0") == CommandOutcome.Applied && d.SpellFaceDown[1,0] &&
            Run("move 1 spell 0 hand") == CommandOutcome.Applied && d.HandFor(1).Single() == spell,
            "A04.2 setup spell moves without activation");
        var coinResults=new List<LabCoinResult>();
        var diceResults=new List<LabDiceResult>();
        d.LabCoinResultReported+=coinResults.Add;
        d.LabDiceResultReported+=diceResults.Add;
        string beforeRandom=console.Execute("state").Message;
        int traceBefore=console.Queue.Trace.Count;
        check(!console.Execute("coin 0 10 2 0 0").Success &&
              !console.Execute("coin 0 10 3 8 0").Success &&
              !console.Execute("dice 1 11 6").Success &&
              !console.Execute("dice 1 10 7").Success &&
              console.Queue.Trace.Count==traceBefore,
            "V20/V21: invalid diagnostic inputs reject before admission");
        check(console.Execute("coin 0 2 3 5 1").Enqueued && coinResults.Count==0,
            "V20: admission does not report the result");
        Drain();
        check(coinResults.SequenceEqual([new LabCoinResult(0,2,3,5,1)]) &&
              Run("dice 1 7 6")==CommandOutcome.Applied &&
              diceResults.SequenceEqual([new LabDiceResult(1,7,6)]) &&
              console.Execute("state").Message==beforeRandom,
            "V20/V21: deterministic reported results, no duel state mutation");
        var ordinary=DuelConsoleSession.EmptyLabDuel();
        ordinary.CreatePrimitiveActionQueue();
        var attached=DuelConsoleSession.Attach(ordinary);
        check(!attached.Execute("coin 0 0 1 1 0").Success &&
              !attached.Execute("dice 0 0 1").Success &&
              attached.Queue.Trace.Count==0,
            "V20/V21: attaching console cannot authorize lab commands");
        check(console.Execute("state").Message.Contains("#" + first.Occurrence.Value),
            "A04.2 state includes physical identity");
    }
}
