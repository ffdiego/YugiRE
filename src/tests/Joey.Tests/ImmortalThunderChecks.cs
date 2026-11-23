using Joey.Core;

internal static class ImmortalThunderChecks
{
    private static Card Monster(ushort id, int index, int atk = 1500, int def = 1300, int level = 4) =>
        new(index, id, id == 461 ? "The Immortal of Thunder" : $"Monster {id}", "",
            (10u << 20) | ((uint)level << 25) |
            ((uint)(atk / 10) << 9) | (uint)(def / 10));
    private static Card Spell(ushort id, int index) =>
        new(index, id, $"Spell {id}", "", 22u << 20);
    private static Card?[] Slots(Card? a = null, Card? b = null) =>
        [a, b, null, null, null];
    private static void Hide(DemoDuel duel, int player, int zone = 0)
    {
        duel.MonsterFaceDown[player, zone] = true;
        duel.DefensePosition[player, zone] = true;
    }

    public static void Run(Action<bool,string> check)
    {
        var immortal = Monster(461, 164);
        var immortalCopy = Monster(461, 1164);
        var attacker = Monster(650, 1650, atk: 2100, def: 1500);
        var highLevel = Monster(660, 1660, level: 6);
        var darkHole = Spell(335, 2335);
        var hane = Monster(540, 1540);
        var command = new Card(1530, 1530, "Royal Command", "", 21u << 20);

        check(FlipEffectRules.Effect(461) == DemoFlipEffect.Gain3000LifePoints &&
              FlipEffectRules.Supports(461) &&
              FlipEffectRules.Effect(164) == DemoFlipEffect.None &&
              ImmortalThunderRules.FlipLifeGain == 3000 &&
              ImmortalThunderRules.GraveyardLifeLoss == 5000 &&
              ImmortalThunderRules.LosesLifeOnFieldToGraveyard(461, true) &&
              !ImmortalThunderRules.LosesLifeOnFieldToGraveyard(461, false) &&
              !ImmortalThunderRules.LosesLifeOnFieldToGraveyard(164, true),
              "P065: original resource ID and flip-history gate, not DAT index");

        var flip = DemoDuel.FromPartialObservation(0, [],
            Slots(immortal), Slots(), 8000);
        Hide(flip,0);
        check(flip.ChangePosition(0, out _) &&
              flip.Life.SequenceEqual([11000,8000]) &&
              flip.MonsterWasFlipped[0,0] && !flip.MonsterFaceDown[0,0] &&
              flip.Monsters[0,0] == immortal &&
              flip.Graveyard(0).Count == 0 && !flip.GameOver &&
              !flip.ChangePosition(0,out _),
              "P065: Flip Summon adds 3000 only once without leaving field");

        var reveal = DemoDuel.FromPartialObservation(0, [],
            Slots(immortal), Slots(), 8000);
        Hide(reveal,0);
        check(reveal.RevealMonsterByEffect(0,0,out _) &&
              reveal.Life[0] == 11000 && reveal.MonsterWasFlipped[0,0] &&
              !reveal.RevealMonsterByEffect(0,0,out _) && reveal.Life[0] == 11000,
              "P065: effect reveal gains 3000 once and preserves flipped history");

        var none = DemoDuel.FromPartialObservation(0, [darkHole], Slots(immortal), Slots(), 8000);
        check(none.Play(0,out _) &&
              none.Graveyard(0).Contains(immortal) &&
              none.Monsters[0,0] is null && none.Life.SequenceEqual([8000,8000]),
              "P065: normal face-up summon state cannot stand in for previously flipped");

        var hidden = DemoDuel.FromPartialObservation(0, [darkHole], Slots(immortal), Slots(), 8000);
        Hide(hidden,0);
        check(hidden.Play(0,out _) &&
              hidden.Graveyard(0).Contains(immortal) && hidden.Life[0] == 8000 &&
              hidden.Monsters[0,0] is null &&
              !hidden.MonsterWasFlipped[0,0],
              "P065: face-down never-flipped monster goes to GY without historical loss");

        var wiped = DemoDuel.FromPartialObservation(0, [darkHole], Slots(immortal), Slots(), 8000);
        Hide(wiped,0);
        check(wiped.RevealMonsterByEffect(0,0,out _) && wiped.Life[0] == 11000 &&
              wiped.Play(0,out _) &&
              wiped.Graveyard(0).Contains(immortal) &&
              wiped.Graveyard(0).Contains(darkHole) &&
              wiped.Life.SequenceEqual([6000,8000]) &&
              wiped.Monsters[0,0] is null && !wiped.MonsterWasFlipped[0,0],
              "P065: flipped monster destroyed by Dark Hole loses 5000 after its gain");

        var tribute = DemoDuel.FromPartialObservation(0, [highLevel], Slots(immortal), Slots(), 8000);
        Hide(tribute,0);
        check(tribute.RevealMonsterByEffect(0,0,out _) &&
              tribute.Summon(0,0,0,true,out _,tributeZones: [0]) &&
              tribute.Graveyard(0).SequenceEqual([immortal]) &&
              tribute.Monsters[0,0] == highLevel &&
              tribute.Life.SequenceEqual([6000,8000]) &&
              !tribute.MonsterWasFlipped[0,0],
              "P065: tribute counts as field-to-Graveyard, resets history for new occupant");

        var bounce = DemoDuel.FromPartialObservation(1, [],
            Slots(immortal), Slots(hane), 8000);
        Hide(bounce,0); Hide(bounce,1);
        check(bounce.RevealMonsterByEffect(0,0,out _) &&
              bounce.Life[0] == 11000 &&
              bounce.RevealMonsterByEffect(1,0,out _) &&
              bounce.PendingFlipTarget is { Owner: 1, Effect: DemoFlipEffect.ReturnOne } &&
              bounce.ResolveFlipTarget(0,0,out _) &&
              bounce.Monsters[0,0] is null &&
              bounce.HandFor(0).Contains(immortal) &&
              bounce.Graveyard(0).Count == 0 &&
              bounce.Life[0] == 11000,
              "P065: Hane-Hane bounce to Hand never applies field-to-GY LP loss");

        var battle = DemoDuel.FromPartialObservation(1, [],
            Slots(immortal), Slots(attacker), 8000);
        Hide(battle,0);
        check(battle.BeginBattle(out _) &&
              battle.Attack(0,0,out _) &&
              battle.Monsters[0,0] is null &&
              battle.Graveyard(0).SequenceEqual([immortal]) &&
              battle.Monsters[1,0] == attacker &&
              battle.Life.SequenceEqual([6000,8000]),
              "P065: during damage step, FLIP +3000 resolves before battle burial -5000");

        var noGain = DemoDuel.FromPartialObservation(0, [], Slots(immortal), Slots(), 8000);
        Hide(noGain,0);
        noGain.Spells[1,3] = command;
        check(noGain.FlipEffectsNegated &&
              noGain.ChangePosition(0,out _) &&
              noGain.Life.SequenceEqual([8000,8000]) &&
              noGain.MonsterWasFlipped[0,0],
              "P065: Royal Command negates FLIP LP gain; monster remains actually revealed");

        var two = DemoDuel.FromPartialObservation(0, [darkHole],
            Slots(immortal,immortalCopy), Slots(), 8000);
        Hide(two,0,0); Hide(two,0,1);
        check(two.RevealMonsterByEffect(0,0,out _) &&
              two.RevealMonsterByEffect(0,1,out _) &&
              two.Life[0] == 14000 &&
              two.Play(0,out _) &&
              two.Graveyard(0).Contains(immortal) &&
              two.Graveyard(0).Contains(immortalCopy) &&
              two.Life[0] == 4000 &&
              !two.MonsterWasFlipped[0,0] && !two.MonsterWasFlipped[0,1],
              "P065: each independent flipped occurrence loses 5000 exactly once");

        var lethal = DemoDuel.FromPartialObservation(0, [darkHole],
            Slots(immortal), Slots(), 8000);
        Hide(lethal,0);
        check(lethal.RevealMonsterByEffect(0,0,out _) &&
              (lethal.Life[0] = 4900) == 4900 &&
              lethal.Play(0,out _) &&
              lethal.Life[0] == 0 && lethal.Winner == 1 &&
              lethal.GameOver && lethal.Graveyard(0).Contains(immortal),
              "P065: losing 5000 on field-to-GY clamps LP and determines winner");

        var enemy = DemoDuel.FromPartialObservation(1, [darkHole],
            Slots(), Slots(immortal), 8000);
        Hide(enemy,1);
        check(enemy.RevealMonsterByEffect(1,0,out _) &&
              enemy.Life.SequenceEqual([8000,11000]) &&
              enemy.Play(0,out _) &&
              enemy.Life.SequenceEqual([8000,6000]) &&
              enemy.Graveyard(1).Contains(immortal),
              "P065: independent owner side 1 gains and loses its own LP");

        var reset = DemoDuel.FromPartialObservation(0, [],
            Slots(immortal), Slots(), 8000);
        Hide(reset,0);
        check(reset.RevealMonsterByEffect(0,0,out _) &&
              reset.MonsterWasFlipped[0,0] &&
              !reset.RevealMonsterByEffect(0,0,out _) &&
              reset.Life[0] == 11000 &&
              reset.Graveyard(0).Count == 0,
              "P065: no synthetic second FLIP while source remains face-up");
    }
}
