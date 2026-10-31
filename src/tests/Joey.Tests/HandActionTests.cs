using Joey.Core;

static class HandActionTests
{
    public static void Run(Action<bool, string> check)
    {
        var monster = new Card(1, 1, "Monster", "", (1u << 20) | (4u << 25));
        var boss = monster with { Id = 2, Properties = (1u << 20) | (6u << 25) };
        var sparks = new Card(3, 342, "Sparks", "", 21u << 20);
        var infinite = new Card(4, 0x593, "Infinite Cards", "", 21u << 20);
        var unknown = new Card(5, 500, "Unsupported spell", "", 21u << 20);
        var duel = new DemoDuel([monster, monster, monster]);
        duel.Monsters[0, 0] = monster; duel.Monsters[0, 2] = monster;
        check(duel.CanSummonFromHand(0) && duel.CanSetFromHand(0) && duel.Hand.Count == 3,
            "Hover eligibility never spends a card or the normal summon");
        check(duel.SummonLeftmost(0, false, out _) && duel.Monsters[0, 1] == monster && duel.Monsters[0, 3] == null,
            "Automatic summon fills the first gap from the player's left");
        check(!duel.CanSummonFromHand(0) && !duel.SetFromHand(0, out _),
            "Setting a monster shares the normal summon limit");
        var tribute = new DemoDuel([boss]);
        for (int z = 0; z < 5; z++) tribute.Monsters[0, z] = monster;
        check(tribute.CanSummonFromHand(0) && !tribute.SummonLeftmost(0, false, out _) && tribute.Graveyard(0).Count == 0,
            "Tribute summon can be offered on a full field but cannot skip payment");
        check(tribute.SummonLeftmost(0, true, out _, [3]) && tribute.Monsters[0, 3] == boss &&
            tribute.MonsterFaceDown[0, 3] && tribute.DefensePosition[0, 3],
            "Tribute set reuses the leftmost zone released by the selected tribute");
        check(!tribute.ChangePosition(3, out _) && tribute.MonsterFaceDown[0, 3],
            "Setting also applies the placement-turn position lock");
        tribute.NextTurn(); tribute.NextTurn();
        check(tribute.ChangePosition(3, out _) && !tribute.MonsterFaceDown[0, 3] && !tribute.DefensePosition[0, 3],
            "A later position change reveals a set monster in attack");
        DemoDuel WithSpell(Card card) => DemoDuel.FromDecks(Enumerable.Repeat(card, 40).ToArray(), Enumerable.Repeat(card, 40).ToArray());
        var spells = WithSpell(infinite);
        spells.Spells[0, 0] = unknown;
        check(spells.SetFromHand(0, out _) && spells.Spells[0, 1] == infinite &&
            spells.SpellFaceDown[0, 1] && !spells.HandLimitDisabled,
            "Set spell occupies the first free spell zone without applying its effect");
        check(spells.ActivateSetSpell(1, out _) && spells.HandLimitDisabled && !spells.SpellFaceDown[0, 1],
            "Activating set Infinite Cards applies its continuous hand-limit effect");
        spells = WithSpell(sparks); spells.Spells[0, 0] = unknown; spells.Spells[0, 1] = unknown;
        check(spells.SetFromHand(0, out _) && spells.Life[1] == 8000 && spells.Spells[0, 2] == sparks,
            "Setting Sparks deals no damage");
        check(spells.ActivateSetSpell(2, out _) && spells.Life[1] == 7800 && spells.Spells[0, 2] == null &&
            spells.Graveyard(0).SequenceEqual([sparks]), "A supported set spell resolves and vacates its slot");
        spells = WithSpell(unknown); spells.Spells[0, 0] = unknown; spells.Spells[0, 1] = unknown;
        check(!spells.CanActivateFromHand(0) && spells.CanSetFromHand(0) && spells.SetFromHand(0, out _) &&
            !spells.ActivateSetSpell(2, out _) && spells.SpellFaceDown[0, 2],
            "Unsupported effects can be set but never pretend to activate");
        var full = new DemoDuel([sparks]);
        for (int z = 0; z < 5; z++) full.Spells[0, z] = unknown;
        check(!full.CanActivateFromHand(0) && !full.CanSetFromHand(0) && !full.Play(0, out _) && full.Hand.Count == 1,
            "A full spell field prevents activation and setting without spending the card");
        var combat = new DemoDuel([monster]);
        combat.SetFromHand(0, out _); combat.NextTurn(); combat.SummonLeftmost(0, false, out _);
        combat.BeginBattle(out _); combat.Attack(0, 0, out _);
        check(!combat.MonsterFaceDown[0, 0], "An attacked face-down defender is revealed");
    }
}
