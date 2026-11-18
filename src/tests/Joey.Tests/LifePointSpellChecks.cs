using Joey.Core;

internal static class LifePointSpellChecks
{
    // Ten supported normal spells. The first five reuse audited P045;
    // healing is an explicit text-backed sandbox rule, not native parity.
    private static readonly (ushort Id, int Caster, int Opponent)[] Cases =
    [
        (342, 0, -200), (343, 0, -500), (344, 0, -600),
        (345, 0, -800), (1007, 0, -300),
        (338, 500, 0), (339, 600, 0), (340, 800, 0),
        (341, 1000, 0), (1315, 1000, 1000)
    ];

    private static Card Spell(ushort id) =>
        new(2000 + id, id, $"Synthetic LP spell {id}", "", 22u << 20);

    private static DemoDuel Duel(ushort id, int side = 0) =>
        DemoDuel.FromPartialObservation(side, [Spell(id)], new Card?[5],
                                        new Card?[5], 8000);

    private static string State(DemoDuel duel) => string.Join(";", duel.Turn, duel.Phase,
        duel.Winner, string.Join(",", duel.Life),
        string.Join(",", duel.Hand.Select(card => card.Id)),
        string.Join(",", duel.Spells.Cast<Card?>().Select(card => card?.Id)),
        string.Join(",", duel.SpellFaceDown.Cast<bool>()),
        string.Join(",", Enumerable.Range(0, 2).SelectMany(p => duel.Graveyard(p)).Select(card => card.Id)));

    public static void Run(Action<bool, string> check)
    {
        foreach (var (id, casterDelta, opponentDelta) in Cases)
        foreach (int side in new[] { 0, 1 })
        {
            var change = LifePointSpellRules.ResolveDemo(id, side);
            check(LifePointSpellRules.Supports(id) &&
                  change is { } planned && planned.CasterDelta == casterDelta &&
                  planned.OpponentDelta == opponentDelta,
                  $"Simple LP spell {id}: plan preserves caster/opponent orientation for side {side}");

            var fromHand = Duel(id, side);
            check(fromHand.CanActivateFromHand(0) && fromHand.Play(0, out _) &&
                  fromHand.Life[side] == 8000 + casterDelta &&
                  fromHand.Life[1 - side] == 8000 + opponentDelta &&
                  fromHand.Hand.Count == 0 && fromHand.Graveyard(side).Single().Id == id &&
                  Enumerable.Range(0, 5).All(z => fromHand.Spells[side, z] is null),
                  $"Simple LP spell {id}: hand activation updates both LP sides and owner graveyard");

            var fromSet = Duel(id, side);
            check(fromSet.SetFromHand(0, out _) &&
                  fromSet.Life[0] == 8000 && fromSet.Life[1] == 8000 &&
                  fromSet.Spells[side, 0]?.Id == id && fromSet.SpellFaceDown[side, 0] &&
                  fromSet.Graveyard(side).Count == 0,
                  $"Simple LP spell {id}: setting does not resolve an effect");
            check(fromSet.ActivateSetSpell(0, out _) &&
                  fromSet.Life[side] == 8000 + casterDelta &&
                  fromSet.Life[1 - side] == 8000 + opponentDelta &&
                  fromSet.Spells[side, 0] is null && !fromSet.SpellFaceDown[side, 0] &&
                  fromSet.Graveyard(side).Single().Id == id,
                  $"Simple LP spell {id}: activating set spell resolves once and frees zone");
            string after = State(fromSet);
            check(!fromSet.ActivateSetSpell(0, out _) && State(fromSet) == after,
                  $"Simple LP spell {id}: a consumed set spell cannot activate again");

            var full = Duel(id, side);
            for (int z = 0; z < 5; z++)
                full.Spells[side, z] = Spell(0);
            string fullBefore = State(full);
            check(!full.CanActivateFromHand(0) && !full.Play(0, out _) &&
                  State(full) == fullBefore,
                  $"Simple LP spell {id}: full spell field never spends a card");
        }

        foreach (ushort unknown in new ushort[] { 0, 337, 300, 1311, 2342, ushort.MaxValue })
        {
            var duel = Duel(unknown);
            string before = State(duel);
            check(!LifePointSpellRules.Supports(unknown) &&
                  LifePointSpellRules.ResolveDemo(unknown, 0) is null &&
                  !duel.CanActivateFromHand(0) && !duel.Play(0, out _) &&
                  State(duel) == before,
                  $"Simple LP spell: unsupported resource {unknown} is not inferred");
        }
        check(LifePointSpellRules.ResolveDemo(338, -1) is null &&
              LifePointSpellRules.ResolveDemo(338, 2) is null,
              "Simple LP spell: invalid caster rejected");

        var unknownSet = Duel(337); // Mooyan Curry requires selecting a player.
        check(unknownSet.SetFromHand(0, out _) &&
              !unknownSet.ActivateSetSpell(0, out _) &&
              unknownSet.Spells[0, 0]?.Id == 337 &&
              unknownSet.SpellFaceDown[0, 0] &&
              unknownSet.Graveyard(0).Count == 0,
              "Mooyan Curry is not falsely activated without a target picker");

        var battle = Duel(338, side: 1);
        check(battle.BeginBattle(out _), "LP spell setup: enter opponent turn battle phase");
        string battleBefore = State(battle);
        check(!battle.CanActivateFromHand(0) && !battle.Play(0, out _) &&
              State(battle) == battleBefore,
              "LP healing may not activate from hand during battle phase");

        var setInBattle = Duel(1315, side: 1);
        check(setInBattle.SetFromHand(0, out _) && setInBattle.BeginBattle(out _),
              "LP spell setup: set before battle phase");
        string setBefore = State(setInBattle);
        check(!setInBattle.ActivateSetSpell(0, out _) && State(setInBattle) == setBefore,
              "Set LP spell cannot activate during battle phase");

        var saturating = Duel(1315);
        saturating.Life[0] = int.MaxValue - 50;
        saturating.Life[1] = int.MaxValue - 10;
        check(saturating.Play(0, out _) &&
              saturating.Life[0] == int.MaxValue &&
              saturating.Life[1] == int.MaxValue && saturating.Winner is null,
              "Rain of Mercy gains above 8000 and safely saturates at int.MaxValue");

        var combo = DemoDuel.FromPartialObservation(0,
            [Spell(342), Spell(338), Spell(1315)], new Card?[5], new Card?[5], 8000);
        check(combo.Play(0, out _) && combo.Play(0, out _) && combo.Play(0, out _) &&
              combo.Life[0] == 9500 && combo.Life[1] == 8800 &&
              combo.Graveyard(0).Select(c => c.Id).SequenceEqual(new ushort[] {342,338,1315}),
              "Mixed burn and recovery share a single playable LP pipeline");
    }
}
