using Joey.Core;

static class SummonTests
{
    public static void Run(Action<bool, string> check)
    {
        Card Monster(int level, ushort id = 1) => new(id, id, "Level " + level, "", (1u << 20) | ((uint)level << 25));
        check(Enumerable.Range(1, 8).Select(level => DemoDuel.NormalTributes(Monster(level))).SequenceEqual(new[] { 0, 0, 0, 0, 1, 1, 2, 2 }),
            "Native normal tribute counts at level boundaries");
        check(!new Card(0, 0, "Unused", "", 0).IsMonster && Monster(4).IsMonster,
            "Native monster classification excludes zero class");
        var low = Monster(4);
        foreach (int level in new[] { 5, 6 })
        {
            var high = Monster(level, 2);
            var duel = new DemoDuel([high]); duel.Monsters[0, 0] = low;
            check(!duel.Summon(0, 0, 1, true, out _) && duel.Hand.Count == 1 && duel.Monsters[0, 0] == low,
                $"Level {level} cannot bypass the one-tribute requirement");
            check(duel.Summon(0, 0, 0, true, out _, [0]) && duel.Monsters[0, 0] == high && duel.Hand.Count == 0 &&
                duel.Graveyard(0).SequenceEqual([low]), $"Level {level} pays one tribute and reuses its occupied zone");
        }
        var boss = Monster(7, 3);
        var full = new DemoDuel([boss, boss]);
        for (int zone = 0; zone < 5; zone++) full.Monsters[0, zone] = low;
        check(!full.Summon(0, 0, 0, true, out _, [0, 0]) && !full.Summon(0, 0, 0, true, out _, [-1, 0]) &&
            !full.Summon(0, 0, 0, true, out _, [0, 5]) && full.Hand.Count == 2 && full.Graveyard(0).Count == 0,
            "Invalid or repeated tribute slots never pay a partial cost");
        check(!full.Summon(0, 0, 4, true, out _, [0, 1]) && full.Monsters[0, 0] == low && full.Monsters[0, 1] == low,
            "Occupied destination outside tribute selection is rejected before payment");
        check(full.Summon(0, 0, 1, true, out _, [0, 1]) && full.Monsters[0, 0] == null && full.Monsters[0, 1] == boss &&
            full.Graveyard(0).Count == 2 && full.Hand.Count == 1 && full.Life.SequenceEqual(new[] { 8000, 8000 }),
            "Full field can pay two tributes without LP damage or requiring a preexisting vacancy");
        check(!full.Summon(0, 0, 2, true, out _, [2, 3]) && full.Monsters[0, 2] == low && full.Graveyard(0).Count == 2,
            "Second normal summon is rejected before spending additional tributes");
        var wrong = new DemoDuel([boss]); wrong.Monsters[1, 0] = low; wrong.Monsters[1, 1] = low;
        check(!wrong.Summon(0, 0, 0, true, out _, [0, 1]) && !wrong.Summon(0, 1, 0, true, out _, [0, 1]) &&
            wrong.Monsters[1, 0] == low && wrong.Hand.Count == 1, "Opponent monsters cannot be used as ordinary demo tributes");
        var warrior = new Card(4, 4, "Warrior", "", (15u << 20) | (4u << 25));
        var sword = new Card(5, 300, "Legendary Sword", "", 21u << 20);
        var equipped = new DemoDuel([warrior, boss, sword]);
        equipped.Summon(0, 0, 0, true, out _); equipped.Equip(1, 0, 0, out _);
        equipped.NextTurn(); equipped.NextTurn();
        check(!equipped.Summon(0, 0, 0, true, out _, [0, 1]) && equipped.Graveyard(0).Count == 0 && equipped.EquipmentCards.Count == 1,
            "Missing second tribute does not remove an equipped monster or its equipment");
        equipped.Monsters[0, 1] = low;
        check(equipped.Summon(0, 0, 0, true, out _, [0, 1]) && equipped.Graveyard(0).Count == 3 && equipped.EquipmentCards.Count == 0,
            "Tribute payment also removes equipment attached to a released monster");
    }
}
