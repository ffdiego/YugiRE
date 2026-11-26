using Joey.Core;

internal static class MonsterBattleRuleChecks
{
    private static Card Monster(ushort id, byte attribute, int attack = 1000) =>
        new(id, id, $"Monster {id}", "",
            (3u << 20) | (4u << 25) | ((uint)attribute << 29) | ((uint)(attack / 10) << 9) | 80u);

    public static void Run(Action<bool, string> check)
    {
        RunBattleDraws(check);
        RunQueuedBattleDraws(check);
        (ushort Id, byte Boost, byte Weaken)[] auras =
            [(491, 1, 2), (627, 2, 1), (523, 3, 4), (597, 4, 3), (526, 5, 6), (608, 6, 5)];
        foreach (var (id, boost, weaken) in auras)
        {
            var aura = Monster(id, boost, 500);
            var ally = Monster(900, weaken, 300);
            var enemy = Monster(901, boost, 1800);
            var duel = DemoDuel.FromPartialObservation(0, [aura],
                [ally, null, null, null, null], [enemy, null, null, null, null], 8000);
            check(duel.Summon(0, 0, 1, true, out _) &&
                  duel.AttackValue(0, 0) == 0 && duel.AttackValue(0, 1) == 1000 &&
                  duel.AttackValue(1, 0) == 2300 && duel.DefenseValue(1, 0) == 800 &&
                  MonsterAuraRules.AttackDelta(id, 0) == 0,
                $"Aura {id}: both sides, self, ATK only, nonnegative total and no unknown attribute bonus");
            duel.MonsterFaceDown[0, 1] = duel.DefensePosition[0, 1] = true;
            check(duel.AttackValue(0, 0) == 300 && duel.AttackValue(1, 0) == 1800,
                $"Aura {id}: face-down source contributes nothing");
            duel.MonsterFaceDown[0, 1] = false;
            duel.NextTurn();
            check(duel.BeginBattle(out _) && duel.Attack(0, 1, out _) &&
                  duel.Monsters[0, 1] is null && duel.AttackValue(0, 0) == 300 &&
                  duel.AttackValue(1, 0) == 1800,
                $"Aura {id}: face-up defense source applies until destroyed");
        }

        var light = Monster(910, 1);
        var dark = Monster(911, 2);
        var hoshiningen = Monster(491, 1);
        var apprentice = Monster(627, 2);
        var field = new Card(1129, 1129, "Luminous Spark", "", 22u << 20);
        var stacked = DemoDuel.FromPartialObservation(0, [field],
            [light, hoshiningen, null, null, null], [dark, hoshiningen, apprentice, null, null], 8000);
        check(stacked.Play(0, out _) && stacked.AttackValue(0, 0) == 2100 &&
              stacked.AttackValue(1, 0) == 700 && stacked.DefenseValue(0, 0) == 400,
            "Auras stack across sides and compose with a field spell before clamping");
        stacked.MonsterFaceDown[0, 0] = true;
        check(stacked.AttackValue(0, 0) == 1000 && stacked.DefenseValue(0, 0) == 800,
            "Face-down aura target keeps its underlying stats");

        var blocker = Monster(912, 2, 2400);
        foreach (ushort id in new ushort[] { 386, 396, 397, 421, 487, 634 })
        {
            var attacker = Monster(id, 2, 600);
            var duel = DemoDuel.FromPartialObservation(1, [],
                [blocker, null, null, null, null], [attacker, null, null, null, null], 8000);
            check(!duel.CanAttackDirectly(0) && duel.BeginBattle(out _) &&
                  duel.CanAttackDirectly(0) && duel.Attack(0, -1, out _) &&
                  duel.Life[0] == 7400 && duel.Monsters[0, 0] == blocker &&
                  !duel.CanAttackDirectly(0) && !duel.Attack(0, -1, out _),
                $"Direct attack {id}: ignores the blocker and uses up the normal attack");
        }

        var alligator = Monster(812, 6);
        var water = Monster(913, 3);
        var fire = Monster(914, 4);
        var earth = Monster(915, 5);
        var filtered = DemoDuel.FromPartialObservation(1, [],
            [water, fire, earth, blocker, null], [alligator, null, null, null, null], 8000);
        filtered.DefensePosition[0, 3] = true;
        check(filtered.BeginBattle(out _) && !filtered.CanAttackDirectly(0) &&
              !filtered.Attack(0, -1, out _) && filtered.CanAttack(0),
            "Alligator: disallowed face-up defender rejects direct attack without consuming it");
        filtered.MonsterFaceDown[0, 3] = true;
        check(filtered.CanAttackDirectly(0) && filtered.Attack(0, -1, out _) &&
              filtered.Life[0] == 7000 && filtered.MonsterFaceDown[0, 3],
            "Alligator: allowed visible attributes and a hidden defender permit direct damage");

        var mirror = new Card(1056, 1056, "Mirror Force", "", 21u << 20);
        var response = DemoDuel.FromPartialObservation(0, [mirror],
            [water, null, null, null, null], [alligator, null, null, null, null], 8000);
        check(response.SetFromHand(0, out _), "Direct response fixture sets Mirror Force");
        response.NextTurn();
        check(response.BeginBattle(out _) && response.Attack(0, -1, out _) &&
              response.PendingAttackResponse?.Attack.DirectAttack == true &&
              response.Life[0] == 8000,
            "A direct attack through a defender opens the standard response window");
        response.Monsters[0, 0] = blocker;
        check(response.PassAttackResponse(out _) && response.Life[0] == 8000 && !response.CanAttack(0),
            "Direct attack legality is checked again after a response");

        var ordinary = DemoDuel.FromPartialObservation(1, [],
            [blocker, null, null, null, null], [light, null, null, null, null], 8000);
        ordinary.MonsterFaceDown[0, 0] = true;
        check(ordinary.BeginBattle(out _) && !ordinary.CanAttackDirectly(0),
            "A hidden enemy still blocks an ordinary direct attack");

        var jinzo = Monster(421, 2, 600);
        var cpu = DemoDuel.FromPartialObservation(1, [],
            [blocker, null, null, null, null], [jinzo, null, null, null, null], 8000);
        check(cpu.BeginBattle(out _) &&
              new CpuDuelController(1).Step(cpu) is { Status: CpuStepStatus.Acted, Action: CpuAction.Attack, TargetZone: -1 } &&
              cpu.Life[0] == 7400,
            "CPU can make a legal direct attack through a stronger defending monster");
    }

    private sealed class BattleDrawGate : IPresentationGate
    {
        public Type? Hold { get; set; }
        public bool CanAdvance => true;
        public bool CanComplete(QueuedDuelCommand command, CommandResult result) =>
            command.Command.GetType() != Hold;
    }

    private static Card?[] Field(Card? card) => [card, null, null, null, null];

    private static void Drain(ActionQueue queue)
    {
        for (int tick = 0; queue.IsBusy && tick < 40; tick++) queue.Tick();
        if (queue.IsBusy) throw new InvalidOperationException("Battle draw queue did not drain.");
    }

    private static void RunQueuedBattleDraws(Action<bool, string> check)
    {
        var first = Monster(951, 1);
        var second = Monster(952, 1);
        var normal = Monster(24, 1, 600);
        var butcher = Monster(BattleDamageEffectRules.BistroButcher, 2, 1800);
        var sorcerer = Monster(BattleDamageEffectRules.MaskedSorcerer, 2, 900);
        Card[] pieces = ExodiaRules.RequiredNames.Select((name, i) =>
            Monster((ushort)(3100 + i), 1) with { Name = name }).ToArray();

        foreach (int side in new[] { 0, 1 })
        foreach (var (source, target, defense, lethal, shortfall, exodia) in new[]
        {
            (sorcerer, (Card?)null, false, false, false, false),
            (butcher, normal, false, false, false, false),
            (butcher, normal, true, false, false, false),
            (butcher, normal, false, true, false, false),
            (butcher, normal, false, false, true, false),
            (butcher, normal, false, false, true, true),
            (butcher, normal, false, false, false, true)
        })
        {
            int opponent = 1 - side;
            int recipient = source == sorcerer ? side : opponent;
            Card[] deck = exodia ? (shortfall ? [pieces[4]] : [pieces[4], second]) :
                shortfall ? [first] : [first, second];
            DemoDuel State()
            {
                var duel = DemoDuel.FromPartialObservation(opponent, exodia ? pieces[..4] : [],
                    side == 0 ? Field(source) : Field(target), side == 1 ? Field(source) : Field(target),
                    lethal ? 100 : 8000, side0Deck: recipient == 0 ? deck : [], side1Deck: recipient == 1 ? deck : []);
                duel.EndTurn(out _);
                duel.DefensePosition[opponent, 0] = defense;
                return duel;
            }

            int targetSlot = target is null ? -1 : 0;
            var legacy = State();
            check(legacy.BeginBattle(out _) && legacy.Attack(0, targetSlot, out _), "Legacy battle draw comparison");
            var queued = State();
            var gate = new BattleDrawGate();
            var queue = queued.CreatePrimitiveActionQueue(gate);
            check(queued.BeginBattle(out _) && queued.TryQueueAttack(0, targetSlot, out _),
                "Battle draw source is admitted through the queue");

            if (target is not null && !defense && !lethal)
            {
                gate.Hold = typeof(DamageCommand);
                for (int i = 0; i < 12 && queue.Active?.Command is not DamageCommand && queue.IsBusy; i++) queue.Tick();
                check(queue.Active?.Command is DamageCommand && queued.HandFor(recipient).Count == (exodia ? 4 : 0) &&
                    queued.Monsters[opponent, 0] == target, "Damage gate blocks battle draws and removal");
                gate.Hold = typeof(DrawCommand);
                for (int i = 0; i < 12 && queue.Active?.Command is not DrawCommand && queue.IsBusy; i++) queue.Tick();
                check(queue.Active?.Command is DrawCommand && queued.LastDeckFlow is null &&
                    queued.Monsters[opponent, 0] == target, "First draw gate blocks remaining draws and battle removal");
                gate.Hold = null;
            }
            Drain(queue);
            check(queued.Life.SequenceEqual(legacy.Life) && queued.Winner == legacy.Winner &&
                queued.ExodiaWinner == legacy.ExodiaWinner && queued.LastDeckFlow == legacy.LastDeckFlow &&
                queued.HandFor(recipient).SequenceEqual(legacy.HandFor(recipient)) &&
                queued.DeckSnapshot(recipient).SequenceEqual(legacy.DeckSnapshot(recipient)) &&
                queued.Monsters.Cast<Card?>().SequenceEqual(legacy.Monsters.Cast<Card?>()) &&
                queued.Graveyard(opponent).SequenceEqual(legacy.Graveyard(opponent)),
                "Battle draw, completion, loss precedence and removal agree with the legacy sandbox");
        }

        foreach (int side in new[] { 0, 1 })
        foreach (bool pass in new[] { false, true })
        {
            int opponent = 1 - side;
            var trap = new Card(1214, AttackTrapRules.MagicCylinder, "Magic Cylinder", "", 21u << 20);
            var duel = DemoDuel.FromPartialObservation(opponent, [trap],
                side == 0 ? Field(sorcerer) : Field(null), side == 1 ? Field(sorcerer) : Field(null), 8000,
                side0Deck: side == 0 ? [first] : [], side1Deck: side == 1 ? [first] : []);
            check(duel.SetFromHand(0, out _) && duel.EndTurn(out _) && duel.BeginBattle(out _), "Battle draw trap fixture");
            var console = new DuelConsoleSession(duel);
            check(console.Execute("attack 0 direct").Enqueued, "Battle draw attack before the trap window");
            Drain(console.Queue);
            check(console.Execute(pass ? "respond-attack pass" : $"respond-attack {opponent} 0").Enqueued,
                "Battle draw trap pass or activation is admitted");
            Drain(console.Queue);
            check(duel.Life[pass ? opponent : side] == 7100 && duel.HandFor(side).Count == (pass ? 1 : 0) &&
                (duel.LastDeckFlow is not null) == pass, "Only actual battle damage triggers the draw after the response");
        }

        var reflected = DemoDuel.FromPartialObservation(1, [], Field(sorcerer), Field(normal),
            8000, side0Deck: [first]);
        reflected.DefensePosition[0, 0] = true;
        reflected.MonsterFaceDown[0, 0] = true;
        var reflectedQueue = reflected.CreatePrimitiveActionQueue();
        check(reflected.BeginBattle(out _) && reflected.TryQueueAttack(0, 0, out _),
            "Masked Sorcerer can defend while hidden");
        Drain(reflectedQueue);
        check(reflected.Life[1] == 7800 && reflected.HandFor(0).SequenceEqual([first]) &&
            reflected.Monsters[0, 0] == sorcerer, "Reflected defense damage draws for the defending source");
    }

    private static void RunBattleDraws(Action<bool, string> check)
    {
        var sorcerer = Monster(219, 2, 900);
        var butcher = Monster(522, 2, 1800);
        var low = Monster(950, 1, 600);
        var first = Monster(951, 1);
        var second = Monster(952, 1);

        var direct = DemoDuel.FromPartialObservation(1, [],
            [null, null, null, null, null], [sorcerer, null, null, null, null],
            8000, side1Deck: [first, second]);
        check(direct.BeginBattle(out _) && direct.Attack(0, -1, out _) &&
              direct.Life[0] == 7100 && direct.HandFor(1).SequenceEqual([first]) &&
              direct.DeckSnapshot(1).SequenceEqual([second]) && direct.HandFor(0).Count == 0 &&
              direct.LastDeckFlow is { EffectResourceId: 219, Controller: 1, AffectedPlayer: 1, MovedFromDeck: 1 },
            "Masked Sorcerer: direct battle damage draws for the source controller");

        var defense = DemoDuel.FromPartialObservation(1, [],
            [sorcerer, null, null, null, null], [low, null, null, null, null],
            8000, side0Deck: [first]);
        defense.DefensePosition[0, 0] = true;
        check(defense.BeginBattle(out _) && defense.Attack(0, 0, out _) &&
              defense.Life[1] == 7800 && defense.HandFor(0).SequenceEqual([first]) &&
              defense.Monsters[0, 0] == sorcerer && defense.Monsters[1, 0] == low,
            "Masked Sorcerer: positive battle damage as defender draws without destroying the attacker");

        var hit = DemoDuel.FromPartialObservation(1, [],
            [low, null, null, null, null], [butcher, null, null, null, null],
            8000, side0Deck: [first, second]);
        check(hit.BeginBattle(out _) && hit.Attack(0, 0, out _) &&
              hit.Life[0] == 6800 && hit.HandFor(0).SequenceEqual([first, second]) &&
              hit.HandFor(1).Count == 0 && hit.Graveyard(0).SequenceEqual([low]) &&
              hit.LastDeckFlow is { EffectResourceId: 522, Controller: 1, AffectedPlayer: 0, DrawFailed: false },
            "Bistro Butcher: battle damage to the opponent draws two cards for that opponent");

        var zero = DemoDuel.FromPartialObservation(1, [],
            [low, null, null, null, null], [butcher, null, null, null, null],
            8000, side0Deck: [first]);
        zero.DefensePosition[0, 0] = true;
        check(zero.BeginBattle(out _) && zero.Attack(0, 0, out _) &&
              zero.Life[0] == 8000 && zero.HandFor(0).Count == 0 &&
              zero.LastDeckFlow is null,
            "Bistro Butcher: destroying a defender without LP damage does not trigger a draw");

        var shortDeck = DemoDuel.FromPartialObservation(1, [],
            [null, null, null, null, null], [butcher, null, null, null, null],
            8000, side0Deck: [first]);
        check(shortDeck.BeginBattle(out _) && shortDeck.Attack(0, -1, out _) &&
              shortDeck.HandFor(0).SequenceEqual([first]) && shortDeck.Winner == 1 &&
              shortDeck.LastDeckFlow is { Requested: 2, MovedFromDeck: 1, DrawFailed: true },
            "Bistro Butcher: forced draw shortfall follows the sandbox deck-out policy");
    }
}
