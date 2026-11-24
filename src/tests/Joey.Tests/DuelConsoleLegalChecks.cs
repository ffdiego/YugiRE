using Joey.Core;
using Joey.Lab;

internal static class DuelConsoleLegalChecks
{
    private sealed class Gate : IPresentationGate
    {
        public bool CanAdvance => true;
        public bool Release { get; set; }
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) => Release;
    }

    public static void Run(Action<bool,string> check)
    {
        Card low = DuelConsoleRunner.FixtureCard();
        Card high = new(81,81,"Fixture boss","",(3u << 20) | (7u << 25));
        Card pot = new(1010,1010,"Pot of Greed","",22u << 20);
        Card upstart = new(1065,1065,"Upstart Goblin","",22u << 20);
        Card dian = new(341,341,"Dian Keto","",22u << 20);
        Card dark = new(335,335,"Dark Hole","",22u << 20);
        Card jar = low with { Id = FlipEffectRules.MorphingJar };
        Card?[] Slots(params Card[] cards) => Enumerable.Range(0,5).Select(i => i < cards.Length ? cards[i] : null).ToArray();
        foreach (int side in new[] { 0, 1 })
        {
            (string State, ActionTraceEntry[] Trace) Run(bool delayed)
            {
                var d = DemoDuel.FromPartialObservation(side, [high, pot, upstart, dian, dark, low],
                    Slots(low,low), Slots(low,low), 8000,
                    side0Deck: Enumerable.Repeat(low,8).ToArray(), side1Deck: Enumerable.Repeat(low,8).ToArray());
                var gate = new Gate();
                var session = new DuelConsoleSession(d, delayed ? gate : null);
                void Execute(string line)
                {
                    check(session.Execute(line).Enqueued, "A04.3 admits " + line);
                    for (int i=0; i<120 && session.Queue.IsBusy; i++)
                    {
                        gate.Release = false;
                        session.Queue.Tick();
                        if (delayed && session.Queue.Active is not null)
                        {
                            var previous = session.Queue.Trace.Count;
                            session.Queue.Tick();
                            check(session.Queue.Trace.Count == previous, "A04.3 gate does not repeat command");
                            gate.Release = true; session.Queue.Tick();
                        }
                    }
                    check(!session.Queue.IsBusy && session.Queue.Trace.Last().Result?.Outcome == CommandOutcome.Applied,
                        "A04.3 applied " + line);
                }
                check(!session.Execute("summon 0 0 0 0").Success &&
                      !session.Execute("summon 0 0").Success && !session.Execute("flip 0").Success &&
                      !session.Execute("activate hand 5").Success, "A04.3 illegal actions reject atomically");
                Execute("summon 0 0 0 1");
                check(d.Monsters[side,0] == high && d.Graveyard(side).Count == 2 &&
                    !session.Execute("summon 4 1").Success, "A04.3 tributes and normal summon allowance");
                Execute("set 0"); // Pot
                Execute("activate set 0");
                Execute("activate hand 0"); // Upstart
                Execute("activate hand 0"); // Dian Keto
                check(d.Life[side] == 9000 && d.Life[1-side] == 9000 &&
                    d.Hand.Count == 5, "A04.3 draw and LP spell results");
                Execute("activate hand 0"); // Dark Hole
                check(d.LastMonsterWipe is { EffectResourceId:335, TotalDestroyed:3 } &&
                      Enumerable.Range(0,5).All(z => d.Monsters[0,z] is null && d.Monsters[1,z] is null),
                      "A04.3 Dark Hole uses shared wipe path");
                Execute($"discard {side} 0");
                Execute($"send {side} 0");
                check(d.Hand.Count == 2 && session.Queue.Trace.Any(t => t.Entry.Origin == ActionOrigin.Console &&
                    t.Entry.Command is DiscardCommand), "A04.3 explicit discard and send differ");
                return (session.Execute("state").Message, session.Queue.Trace.ToArray());
            }
            var immediate = Run(false); var gated = Run(true);
            check(immediate.State == gated.State && immediate.Trace.SequenceEqual(gated.Trace),
                "A04.3 same legal script with immediate/gated execution");
        }
        var equipMonster = new Card(42,77,"Warrior","",(15u<<20)|(4u<<25)|(100u<<9)|100u);
        var sword = new Card(300,300,"Legendary Sword","",22u<<20);
        var e = DemoDuel.FromPartialObservation(0,[sword],Slots(equipMonster),Slots(),8000,equipmentContextResolved:true);
        var ec = new DuelConsoleSession(e);
        check(!ec.Execute("equip hand 0 1 0").Success && ec.Execute("equip hand 0 0 0").Enqueued &&
            e.EquipmentCards.Count == 0,"A04.3 equip admission uses C12 legality without mutation");
        for(int i=0;i<100 && ec.Queue.IsBusy;i++) ec.Queue.Tick();
        check(e.EquipmentCards.Count==1 && e.AttackValue(0,0)==1300 &&
            ec.Queue.Trace.Any(t=>t.Entry.Command is EquipCommand && t.Entry.Origin==ActionOrigin.Console),
            "A04.3 console uses shared equipment executor");
        var f = DemoDuel.FromPartialObservation(0, [low], Slots(jar, low), Slots(), 8000,
            side0Deck: Enumerable.Repeat(low,6).ToArray(), side1Deck: Enumerable.Repeat(low,6).ToArray());
        f.MonsterFaceDown[0,0] = f.DefensePosition[0,0] = true;
        var fc = new DuelConsoleSession(f);
        check(!fc.Execute("position 0 defense").Success && fc.Execute("flip 0").Enqueued, "A04.3 Flip is distinct from position");
        for(int i=0;i<100 && fc.Queue.IsBusy;i++) fc.Queue.Tick();
        check(f.HandFor(0).Count == 5 && f.HandFor(1).Count == 5 &&
            !f.MonsterFaceDown[0,0] && !fc.Execute("position 0 defense").Success &&
            fc.Execute("position 1 defense").Enqueued, "A04.3 Morphing Jar and position locks");
        for(int i=0;i<100 && fc.Queue.IsBusy;i++) fc.Queue.Tick();
        check(f.DefensePosition[0,1] && !fc.Execute("position 1 attack").Success, "A04.3 one position change per turn");
    }
}
