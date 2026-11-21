using Joey.Core;

internal static class DrawSpellChecks
{
    public static void Run(Action<bool, string> check)
    {
        var pot = new Card(291, DrawSpellRules.PotOfGreed, "Fixture draw two", "", 22u << 20);
        var goblin = new Card(672, DrawSpellRules.UpstartGoblin, "Fixture draw and heal", "", 22u << 20);
        var first = new Card(2000, 3000, "First fixture", "", 22u << 20);
        var second = first with { Index = 2001, Id = 3001 };
        var third = first with { Index = 2002, Id = 3002 };
        Card?[] empty = [null, null, null, null, null];
        foreach (int side in new[] { 0, 1 })
        {
            DemoDuel State(Card spell, params Card[] deck) => DemoDuel.FromPartialObservation(side,
                [spell], empty, empty, 8000, side0Deck: side == 0 ? deck : null, side1Deck: side == 1 ? deck : null);
            var d = State(pot, first, second, third);
            check(d.CanActivateFromHand(0) && d.Play(0, out _) && d.Hand.SequenceEqual(new[] { first, second }) &&
                d.DeckCount(side) == 1 && d.Graveyard(side).SequenceEqual(new[] { pot }) &&
                d.CanDraw && d.Draw(out _) && !d.CanDraw && d.Hand.Last() == third,
                $"Draw spells P{side + 1}: Pot draws two in order and preserves the turn's normal draw");
            d = State(goblin, first);
            check(d.SetFromHand(0, out _) && d.DeckCount(side) == 1 && d.Life[1 - side] == 8000 &&
                d.ActivateSetSpell(0, out _) && d.Spells[side, 0] is null && !d.SpellFaceDown[side, 0] &&
                d.Hand.SequenceEqual(new[] { first }) && d.Graveyard(side).SequenceEqual(new[] { goblin }) &&
                d.Life[1 - side] == 9000 && d.Life[side] == 8000 && !d.GameOver,
                $"Draw spells P{side + 1}: Upstart resolves once from Set and heals only the opponent");
            check(!d.ActivateSetSpell(0, out _) && d.Draw(out _) && d.Winner == 1 - side,
                $"Draw spells P{side + 1}: drawing the last card is legal, next normal draw loses");
            d = State(pot, first);
            check(!d.CanActivateFromHand(0) && !d.Play(0, out _) && d.Hand.Single() == pot && d.DeckCount(side) == 1 &&
                d.SetFromHand(0, out _) && !d.ActivateSetSpell(0, out _) && d.Spells[side, 0] == pot &&
                d.SpellFaceDown[side, 0] && d.Graveyard(side).Count == 0 && !d.GameOver,
                $"Draw spells P{side + 1}: insufficient deck rejects either activation atomically");
            var cpu = new CpuDuelController(side);
            d = State(pot, first, second, third, first);
            check(cpu.Step(d).Action == CpuAction.Draw && cpu.Step(d).Action == CpuAction.Spell &&
                d.Graveyard(side).Single() == pot && d.DeckCount(side) == 1,
                $"Draw spells P{side + 1}: CPU uses Pot after its normal draw");
        }
    }
}
