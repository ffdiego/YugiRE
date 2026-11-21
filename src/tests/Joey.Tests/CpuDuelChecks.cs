using Joey.Core;
using Joey.LookAndFeel;

internal static class CpuDuelChecks
{
    private static Card Monster(ushort id, int attack = 1800, int defense = 1000) =>
        new(id + 2000, id, "Fixture " + id, "", (15u << 20) | (4u << 25) |
            ((uint)(attack / 10) << 9) | (uint)(defense / 10));
    private static Card Spell(ushort id, int type = 22) => new(id + 2000, id, "Fixture " + id, "", (uint)type << 20);
    private static Card?[] Slots(Card? first = null) => [first, null, null, null, null];
    private static DemoDuel State(int side, IReadOnlyList<Card> hand, Card? own = null, Card? enemy = null,
        IReadOnlyList<Card>? grave = null, IReadOnlyList<Card>? deck = null) =>
        DemoDuel.FromPartialObservation(side, hand, Slots(side == 0 ? own : enemy),
            Slots(side == 1 ? own : enemy), 8000, true,
            side == 0 ? grave : null, side == 1 ? grave : null,
            side == 0 ? deck : null, side == 1 ? deck : null);

    public static void Run(Action<bool, string> check)
    {
        var origin = new CardBounds(400, 520, 108, 153);
        var destination = NativeDuelLayout.MonsterBounds(0, true);
        var halfway = CardTravel.Sample(origin, destination, 4);
        check(CardTravel.Sample(origin, destination, 0) == origin &&
            CardTravel.Sample(origin, destination, CardTravel.Updates) == destination &&
            halfway.Y < 420 && halfway.Y > destination.Y && halfway.Width < origin.Width,
            "Alpha presentation: counter-based card travel moves and scales to the exact zone");
        var strong = Monster(1); var weak = Monster(2, 500, 500);
        var cpu = new CpuDuelController(1);
        var empty = State(1, []);
        check(cpu.Step(empty) is { Status: CpuStepStatus.Acted, Action: CpuAction.Draw } && empty.Winner == 0,
            "CPU: empty deck loses once and game-over becomes immutable");
        check(cpu.Step(empty).Status == CpuStepStatus.GameOver && empty.Turn == 2, "CPU: no action after defeat");
        var other = State(0, [strong], deck: [weak]);
        check(cpu.Step(other).Status == CpuStepStatus.WaitingForOpponent && other.Hand.Count == 1,
            "CPU: leaves opponent turn untouched");
        var win = State(1, [Spell(345)], deck: [weak]); win.Life[0] = 100;
        cpu.Step(win);
        check(cpu.Step(win).Action == CpuAction.Spell && win.Winner == 1, "CPU: lethal spell wins");

        var noMoves = State(1, [], own: weak, enemy: strong, deck: [Spell(999)]);
        int count = 0;
        while (noMoves.Turn == 2 && !noMoves.GameOver && count++ < 30)
            check(cpu.Step(noMoves).Status == CpuStepStatus.Acted, "CPU: bounded few-options action");
        check(noMoves.Turn == 3 && count < 10 && noMoves.Life[1] == 8000,
            "CPU: refuses losing face-up attack and ends turn with unsupported hand card");
        var defense = State(1, [Monster(3, 700, 2000)], enemy: strong, deck: [Spell(999)]);
        cpu.Step(defense); var set = cpu.Step(defense);
        check(set.Action == CpuAction.Set && defense.DefensePosition[1, 0] &&
            !set.Message.Contains("Fixture"), "CPU: defensive set with hidden identity in log");

        foreach (ushort id in new ushort[] { 500, 590, 427, 101, 601, 540 })
        {
            var flip = Monster(id, 450, 600);
            var d = State(1, [], flip, strong, [Spell(342), Spell(1002, 21)],
                Enumerable.Repeat(weak, 12).ToArray());
            d.MonsterFaceDown[1, 0] = d.DefensePosition[1, 0] = true;
            check(d.ChangePosition(0, out _), $"CPU FLIP {id}: setup trigger");
            int steps = 0;
            while (CpuDuelController.PendingOwner(d) == 1 && !d.GameOver && steps++ < 12)
                check(cpu.Step(d).Status == CpuStepStatus.Acted, $"CPU FLIP {id}: resolve choice");
            check(CpuDuelController.PendingOwner(d) is null && steps < 12, $"CPU FLIP {id}: no stalled prompt");
        }
        var attackFlip = State(0, [], strong, Monster(500, 450, 600));
        attackFlip.MonsterFaceDown[1, 0] = attackFlip.DefensePosition[1, 0] = true;
        attackFlip.NextTurn(); attackFlip.NextTurn(); attackFlip.BeginBattle(out _); attackFlip.Attack(0, 0, out _);
        check(attackFlip.PendingFlipTarget?.Owner == 1 && cpu.Step(attackFlip).Status == CpuStepStatus.Acted &&
            attackFlip.PendingFlipTarget is null && attackFlip.Monsters[0, 0] is null,
            "CPU: defender resolves mandatory FLIP during human turn");

        var revive = State(1, [], Monster(1405), strong);
        revive.NextTurn(); revive.BeginBattle(out _); revive.Attack(0, 0, out _);
        check(revive.PendingEffectDecision?.Owner == 1 && cpu.Step(revive).Action == CpuAction.Decision &&
            revive.PendingEffectDecision is null && revive.Life[1] == 8000,
            "CPU: optional revival cost is declined without blocking opponent");

        var discard = State(1, Enumerable.Repeat(Spell(999), 12).ToArray(), deck: [Spell(999)]);
        int discards = 0;
        for (int i = 0; i < 25 && discard.Turn == 2; i++)
        {
            var step = cpu.Step(discard);
            if (step.Action == CpuAction.Discard) discards++;
            check(step.Status == CpuStepStatus.Acted, "CPU: discard progress");
        }
        check(discard.Turn == 3 && discards == 7 && discard.HandFor(1).Count == 6,
            "CPU: enforces end-turn hand limit and advances exactly once");

        var stressed = State(1, Enumerable.Repeat(Spell(338), 75).ToArray(), deck: [weak]);
        int stressActions = 0, tacticalSpells = 0;
        while (stressed.Turn == 2 && stressActions++ < CpuDuelController.MaxActionsPerTurn)
        {
            var step = cpu.Step(stressed);
            if (step.Status != CpuStepStatus.Acted) throw new Exception(step.Message);
            if (step.Action == CpuAction.Spell) tacticalSpells++;
        }
        check(stressed.Turn == 3 && tacticalSpells == CpuDuelController.TacticalActionLimit - 1 &&
            stressed.HandFor(1).Count == 6 && stressActions < CpuDuelController.MaxActionsPerTurn,
            "CPU: tactical limit stops optional actions but still finishes mandatory discards");

        // Entire deterministic matches, with FLIP, LP and trap responses in both directions.
        var pool = new[] { strong, Monster(3, 1400, 2000), Monster(500, 450, 600), Monster(427, 300, 400),
            Monster(101, 900, 400), Monster(590, 700, 600), Spell(345), Spell(342), Spell(341), Spell(1002, 21), Spell(1056, 21) };
        var deck = Enumerable.Range(0, 40).Select(i => pool[i % pool.Length]).ToArray();
        string Play(int seed, DuelLookAndFeel style)
        {
            var d = DemoDuel.FromDecks(deck, deck, seed);
            var controllers = new[] { new CpuDuelController(0), new CpuDuelController(1) };
            var clock = new DuelPresentationClock(style);
            var transcript = new List<string>();
            for (int i = 0; i < 5000 && !d.GameOver; i++)
            {
                int owner = CpuDuelController.PendingOwner(d) ?? d.ActivePlayer;
                var result = controllers[owner].Step(d);
                if (result.Status != CpuStepStatus.Acted) throw new Exception($"CPU seed {seed}: {result.Message}");
                if (controllers[owner].ActionsThisTurn > CpuDuelController.MaxActionsPerTurn) throw new Exception("Unbounded CPU turn");
                transcript.Add($"{d.Turn}:{result.Action}:{d.Life[0]}:{d.Life[1]}:{result.Message}");
                clock.Show(DuelPresentationCue.CardMove); clock.Advance(TimeSpan.FromSeconds(1));
            }
            if (!d.GameOver) throw new Exception("CPU match did not terminate: " + seed);
            return string.Join("|", transcript) + $"/{d.Winner}/{d.IsDraw}";
        }
        for (int seed = 0; seed < 40; seed++)
        {
            string expected = Play(seed, new DuelLookAndFeel());
            check(expected == Play(seed, new FastLookAndFeel()) && expected == Play(seed, new InstantLookAndFeel()),
                $"CPU: full duel seed {seed}, deterministic result independent of presentation");
        }
    }

    public static void RunCatalog(Action<bool, string> check, Archive archive)
    {
        var catalog = Catalog.Load(archive);
        var packs = archive.Read(@"bin#\card_pack.bin");
        var deck = AlphaDeck.Create(CardListResources.ForPacks(catalog, packs));
        var art = CardArtwork.Load(archive, catalog);
        check(deck.All(c => art.For(c) is { } image && !image.StartsWith(@"card\token_", StringComparison.OrdinalIgnoreCase)),
            "Alpha: all cards are available in the packs and have non-token artwork");
        check(deck.Count == 40 && deck.GroupBy(c => c.Id).All(g => g.Count() <= 3), "Alpha: curated DAT deck has 40 cards, at most three copies");
        for (int seed = 0; seed < 30; seed++)
        {
            var d = DemoDuel.FromDecks(deck, deck, seed);
            var players = new[] { new CpuDuelController(0), new CpuDuelController(1) };
            int steps = 0;
            while (!d.GameOver && steps++ < 5000)
            {
                var action = players[CpuDuelController.PendingOwner(d) ?? d.ActivePlayer].Step(d);
                if (action.Status != CpuStepStatus.Acted) throw new Exception($"Alpha DAT seed {seed}: {action.Message}");
            }
            check(d.GameOver && steps < 5000, $"Alpha DAT: complete duel seed {seed} in {steps} actions / {d.Turn} turns");
        }
    }
}
