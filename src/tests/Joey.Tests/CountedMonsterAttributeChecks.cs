using Joey.Core;

internal static class CountedMonsterAttributeChecks
{
    private static Card Monster(ushort id, int type = 3, int attack = 1600) =>
        new(id, id, $"Monster {id}", "", ((uint)type << 20) | (4u << 25) |
            ((uint)(attack / 10) << 9) | 120u);

    private static readonly Card Spell = new(342, 342, "Spell", "", 22u << 20);
    private static readonly Card Dragon = Monster(1351, type: 1, attack: 1000);
    private static readonly Card Machine = Monster(24, type: 7);
    private static readonly Card Plant = Monster(24, type: 13);
    private static readonly Card Harpie = Monster(61, type: 16);

    private sealed record Scenario(ushort Id, int Attack, int Defense = 1200,
        int Type = 3, Card? Ally = null, Card? Enemy = null, Card? Hidden = null,
        Card[]? OwnGrave = null, Card[]? EnemyGrave = null, int Hand = 0);

    private static void Drain(ActionQueue queue)
    {
        for (int tick = 0; queue.IsBusy && tick < 50; tick++) queue.Tick();
        if (queue.IsBusy || queue.Trace.Any(entry => entry.Result?.Outcome is CommandOutcome.Rejected or CommandOutcome.Skipped))
            throw new InvalidOperationException("Counted attribute scenario did not resolve.");
    }

    public static void Run(Action<bool, string> check)
    {
        Scenario[] scenarios =
        [
            new(11, 2100, Ally: Monster(553), Enemy: Monster(553), Hidden: Monster(553)),
            new(553, 2100, Ally: Monster(11), Enemy: Monster(11), Hidden: Monster(11)),
            new(234, 1800, Ally: Plant, Enemy: Plant, Hidden: Plant),
            new(406, 1900, Type: 7, Ally: Machine, Enemy: Machine, Hidden: Machine),
            new(385, 2200, 1800, Ally: Harpie, Enemy: Harpie, Hidden: Harpie),
            new(367, 1800, OwnGrave: [Monster(24), Spell, Monster(24)], EnemyGrave: [Dragon]),
            new(759, 2800, OwnGrave: [Monster(34), Monster(1210), Spell],
                EnemyGrave: [Monster(2034), Monster(721), Monster(24)]),
            new(808, 2600, Ally: Dragon, Hidden: Dragon, OwnGrave: [Dragon], EnemyGrave: [Dragon, Spell]),
            new(515, 2200, 1800, Hand: 2),
            new(1111, 0, 0, Hand: 4)
        ];

        foreach (var scenario in scenarios)
        foreach (int side in new[] { 0, 1 })
        {
            int opponent = 1 - side;
            Card?[] own = [Monster(scenario.Id, scenario.Type), scenario.Ally, null, null, null];
            Card?[] enemy = [Dragon, scenario.Enemy, scenario.Hidden, null, null];
            var duel = DemoDuel.FromPartialObservation(side, Enumerable.Repeat(Spell, scenario.Hand).ToArray(),
                side == 0 ? own : enemy, side == 1 ? own : enemy, 8000,
                side0Graveyard: side == 0 ? scenario.OwnGrave : scenario.EnemyGrave,
                side1Graveyard: side == 1 ? scenario.OwnGrave : scenario.EnemyGrave);
            if (scenario.Hidden is not null)
                duel.MonsterFaceDown[opponent, 2] = duel.DefensePosition[opponent, 2] = true;
            if (side == 0)
                check(duel.EndTurn(out _) && duel.EndTurn(out _), "Move the first player to a legal Battle Phase turn");
            check(duel.AttackValue(side, 0) == 1600, "Counted attribute rules activate with the action queue");
            var queue = duel.CreatePrimitiveActionQueue();
            check(duel.AttackValue(side, 0) == scenario.Attack && duel.DefenseValue(side, 0) == scenario.Defense,
                $"Counted attributes {scenario.Id}: only eligible sources and current zones count for side {side}");
            check(duel.TryQueueBattlePhaseChange(true, out _), "Enter Battle Phase through the queue");
            Drain(queue);
            check(duel.TryQueueAttack(0, 0, out _), $"Counted attributes {scenario.Id}: targeted attack admitted");
            Drain(queue);
            int delta = scenario.Attack - Dragon.Attack;
            check(duel.Life[side] == 8000 + Math.Min(delta, 0) &&
                duel.Life[opponent] == 8000 - Math.Max(delta, 0) &&
                (duel.Monsters[side, 0] is null) == (delta <= 0) &&
                (duel.Monsters[opponent, 0] is null) == (delta >= 0) &&
                queue.Trace.Any(entry => entry.Stage == ActionTraceStage.Executed &&
                    entry.Entry.Command is DeclareTargetedAttackCommand),
                $"Counted attributes {scenario.Id}: queued combat uses the calculated attack");
        }

        foreach (int defender in new[] { 0, 1 })
        {
            int attacker = 1 - defender;
            Card?[] own = [Monster(515), null, null, null, null];
            Card?[] enemy = [Monster(24, attack: 1400), null, null, null, null];
            var duel = DemoDuel.FromPartialObservation(defender, [Spell],
                defender == 0 ? own : enemy, defender == 1 ? own : enemy, 8000,
                side0Deck: defender == 0 ? [Spell] : [], side1Deck: defender == 1 ? [Spell] : []);
            duel.MonsterFaceDown[defender, 0] = duel.DefensePosition[defender, 0] = true;
            var queue = duel.CreatePrimitiveActionQueue();
            check(duel.TryQueueTurnDraw(out _), "Muka Muka fixture: draw through the queue");
            Drain(queue);
            check(duel.Hand.Count == 2 && duel.DefenseValue(defender, 0) == 1200,
                "Muka Muka remains at printed DEF while face-down");
            check(duel.TryQueueEndTurn(out _), "Muka Muka fixture: switch turns");
            Drain(queue);
            check(duel.TryQueueBattlePhaseChange(true, out _), "Muka Muka fixture: enter Battle Phase");
            Drain(queue);
            check(duel.TryQueueAttack(0, 0, out _), "Muka Muka fixture: attack the Set occurrence");
            Drain(queue);
            check(!duel.MonsterFaceDown[defender, 0] && duel.DefenseValue(defender, 0) == 1800 &&
                duel.Life[attacker] == 7600 && duel.Monsters[defender, 0]?.Id == 515,
                "Muka Muka uses the drawn cards when revealed for damage calculation");
        }
    }

    public static void RunCatalog(Action<bool, string> check, Archive archive)
    {
        var catalog = Catalog.Load(archive).ToDictionary(card => card.Id);
        var tribute = catalog[24];
        foreach (ushort id in new ushort[] { 11, 234, 367, 385, 406, 515, 553, 759, 808, 1111 })
        {
            Card card = catalog[id];
            var duel = DemoDuel.FromPartialObservation(1, [card], new Card?[5],
                [tribute, tribute, null, null, null], 8000);
            var queue = duel.CreatePrimitiveActionQueue();
            int[] costs = Enumerable.Range(0, DemoDuel.NormalTributes(card)).ToArray();
            check(card.IsMonster && duel.TryQueueNormalPlacement(0, 2, false, out _, costs),
                $"Counted attributes {id}: the DAT occurrence can be Normal Summoned with its required tributes");
            Drain(queue);
            check(duel.Monsters[1, 2]?.Id == id && duel.Graveyard(1).Count == costs.Length,
                $"Counted attributes {id}: the queued placement spends exactly the selected tributes");
        }
    }
}
