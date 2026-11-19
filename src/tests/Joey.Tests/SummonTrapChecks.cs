using Joey.Core;

internal static class SummonTrapChecks
{
    private static Card Monster(ushort id, int index, int atk = 1000, int def = 800,
        int level = 4, int kind = 3) =>
        new(index, id, $"Monster {id}", "", ((uint)kind << 20) |
            ((uint)level << 25) | ((uint)(atk / 10) << 9) | (uint)(def / 10));
    private static Card Trap(ushort id, int index) =>
        new(index, id, id == 1002 ? "Trap Hole" : "Torrential Tribute", "", 21u << 20);
    private static Card?[] Slots(Card? a = null, Card? b = null, Card? c = null) =>
        [a, b, c, null, null];

    public static void Run(Action<bool, string> check)
    {
        var trapHole = Trap(1002, 120);
        var torrential = Trap(1425, 272);
        var small = Monster(10, 101, 900);
        var exact = Monster(11, 102, 1000);
        var strong = Monster(12, 103, 1600);
        var sangan = Monster(47, 248);
        var witch = Monster(573, 261, 1100, 1200);
        var senju = Monster(1109, 485, 1400, 1000);
        var ritual = new Card(777, 730, "Relinquished", "", (18u << 20) | (1u << 25));
        var eligibleSangan = Monster(14, 104, 1200, 1900);
        var eligibleWitch = Monster(15, 105, 1900, 1200);

        check(SummonTrapRules.Supports(1002) && SummonTrapRules.Supports(1425) &&
              !SummonTrapRules.Supports(335) && !SummonTrapRules.Supports(1000) &&
              SummonTrapRules.IsEligible(trapHole, 0,
                  new(1, 0, exact, DemoSummonKind.Normal), 1000) &&
              !SummonTrapRules.IsEligible(trapHole, 1,
                  new(1, 0, exact, DemoSummonKind.Normal), 1000) &&
              !SummonTrapRules.IsEligible(trapHole, 0,
                  new(1, 0, small, DemoSummonKind.Flip), 999) &&
              SummonTrapRules.IsEligible(torrential, 1,
                  new(1, 0, small, DemoSummonKind.Normal), 900),
              "P196/P038: exact Trap IDs, opponent-only Trap Hole and inclusive 1000 current ATK");

        // The first player sets; the second player successfully Summons.
        var normal = DemoDuel.FromPartialObservation(0, [trapHole], Slots(), Slots(), 8000,
            side1Deck: [exact]);
        check(normal.SetFromHand(0, out _) &&
              normal.SpellFaceDown[0, 0] &&
              normal.PendingSummonResponse is null &&
              !normal.CanActivateFromHand(0),
              "Trap Hole: setting a Trap never activates it");
        normal.NextTurn();
        check(normal.Draw(out _) && normal.SummonLeftmost(0, false, out _) &&
              normal.PendingSummonResponse is
                  { Summon: { Player: 1, Zone: 0, Kind: DemoSummonKind.Normal } } &&
              normal.PendingSummonResponse.EligibleTraps.Single().Owner == 0 &&
              normal.PendingSummonResponse.EligibleTraps.Single().Trap == trapHole &&
              !normal.EndTurn(out _) && !normal.BeginBattle(out _) &&
              !normal.ActivateSummonTrap(1, 0, out _) &&
              !normal.ActivateSummonTrap(0, 1, out _),
              "Trap Hole: opponent Normal Summon opens exactly one response window and blocks actions");
        check(normal.ActivateSummonTrap(0, 0, out _) &&
              normal.PendingSummonResponse is null &&
              normal.LastSummonTrap is { TrapResourceId: 1002, Responder: 0,
                  SummoningPlayer: 1, SummonKind: DemoSummonKind.Normal, DestroyedMonsters: 1 } &&
              normal.Spells[0, 0] is null &&
              normal.Monsters[1, 0] is null &&
              normal.Graveyard(1).Single() == exact &&
              normal.Graveyard(0).Single() == trapHole &&
              !normal.ActivateSummonTrap(0, 0, out _),
              "Trap Hole: opponent's monster destroyed once, Trap sent to its owner's graveyard");

        // A Trap cannot be activated on the same turn it was Set, even for
        // its controller's own successful Summon (Torrential).
        var fresh = DemoDuel.FromPartialObservation(0, [torrential, strong],
            Slots(), Slots(), 8000);
        check(fresh.SetFromHand(0, out _) &&
              fresh.SummonLeftmost(0, false, out _) &&
              fresh.PendingSummonResponse is null &&
              fresh.Spells[0, 0] == torrential &&
              fresh.Monsters[0, 0] == strong &&
              !fresh.ActivateSummonTrap(0, 0, out _),
              "Torrential: cannot respond on the turn it was Set");

        // Current ATK, not base ATK, controls Trap Hole. Sogen boosts Warrior.
        var warrior = Monster(50, 106, 900, 900, kind: 15);
        var field = new Card(490, 332, "Sogen", "", 22u << 20);
        var buffed = DemoDuel.FromPartialObservation(0, [trapHole, field], Slots(),
            Slots(), 8000, side1Deck: [warrior], equipmentContextResolved: true);
        check(buffed.SetFromHand(0, out _) && buffed.Play(0, out _) &&
              buffed.AttackValue(1, 0) == 0,
              "Trap Hole: prior set and field buff are legal setup");
        buffed.NextTurn();
        check(buffed.Draw(out _) && buffed.SummonLeftmost(0, false, out _) &&
              buffed.AttackValue(1, 0) == 1100 &&
              buffed.PendingSummonResponse is not null &&
              buffed.ActivateSummonTrap(0, 0, out _) &&
              buffed.Graveyard(1).Single() == warrior,
              "Trap Hole: Sogen's +200 ATK makes a 900 ATK Warrior eligible");

        var low = DemoDuel.FromPartialObservation(0, [trapHole], Slots(), Slots(), 8000,
            side1Deck: [small]);
        low.SetFromHand(0, out _); low.NextTurn(); low.Draw(out _);
        check(low.SummonLeftmost(0, false, out _) &&
              low.PendingSummonResponse is null &&
              low.Spells[0, 0] == trapHole && low.Monsters[1, 0] == small,
              "Trap Hole: a Normal Summon below 1000 ATK opens no response");

        var flip = DemoDuel.FromPartialObservation(0, [trapHole], Slots(),
            Slots(exact), 8000);
        flip.SetFromHand(0, out _); flip.NextTurn();
        flip.MonsterFaceDown[1, 0] = flip.DefensePosition[1, 0] = true;
        check(flip.ChangePosition(0, out _) &&
              flip.PendingSummonResponse is
                  { Summon: { Kind: DemoSummonKind.Flip, Player: 1 } } &&
              flip.ActivateSummonTrap(0, 0, out _) &&
              flip.Monsters[1, 0] is null &&
              flip.Graveyard(1).Single() == exact &&
              flip.LastSummonTrap?.SummonKind == DemoSummonKind.Flip,
              "Trap Hole: voluntary Flip Summon qualifies, with no battle required");

        var external = DemoDuel.FromPartialObservation(0, [trapHole], Slots(),
            Slots(exact), 8000);
        external.SetFromHand(0, out _); external.NextTurn();
        external.MonsterFaceDown[1, 0] = true;
        check(external.RevealMonsterByEffect(1, 0, out _) &&
              external.PendingSummonResponse is null && external.Spells[0, 0] == trapHole,
              "Trap Hole: external face-up reveal does not create a Summon response");

        var faceDownSet = DemoDuel.FromPartialObservation(0, [trapHole], Slots(),
            Slots(), 8000, side1Deck: [strong]);
        faceDownSet.SetFromHand(0, out _); faceDownSet.NextTurn(); faceDownSet.Draw(out _);
        check(faceDownSet.SetFromHand(0, out _) &&
              faceDownSet.PendingSummonResponse is null &&
              faceDownSet.Spells[0, 0] == trapHole,
              "Trap Hole: opponent setting a monster face-down does not activate a response");

        // Both players are affected by Torrential. No monster is removed before
        // destruction finishes, and queued Sangan/Witch searches remain available.
        var wipe = DemoDuel.FromPartialObservation(0, [torrential],
            Slots(sangan), Slots(witch), 8000,
            side0Deck: [eligibleSangan], side1Deck: [strong, eligibleWitch]);
        check(wipe.SetFromHand(0, out _) && wipe.NextTurn() /* turn 2 */ &&
              wipe.Draw(out _) && wipe.SummonLeftmost(0, false, out _) &&
              wipe.PendingSummonResponse is { Summon: { Player: 1 } } &&
              wipe.PendingSummonResponse.EligibleTraps.Single().Trap == torrential &&
              wipe.ActivateSummonTrap(0, 0, out _) &&
              wipe.LastMonsterWipe is { EffectResourceId: 1425, TotalDestroyed: 3,
                  DestroyedPlayer0: 1, DestroyedPlayer1: 2 } &&
              Enumerable.Range(0, 5).All(z => wipe.Monsters[0, z] is null &&
                wipe.Monsters[1, z] is null) &&
              wipe.Graveyard(0).SequenceEqual([torrential, sangan]) &&
              wipe.Graveyard(1).SequenceEqual([witch, strong]) &&
              wipe.PendingCardSelection is { Owner: 0, EffectResourceId: 47 } &&
              wipe.PendingCardSelection.EligibleCards.Single().Card == eligibleSangan,
              "Torrential: destroy both fields after Summon, then open queued Sangan search");
        check(wipe.ResolveCardSelection(
                  wipe.PendingCardSelection!.EligibleCards.Single().SourceIndex, out _) &&
              wipe.PendingCardSelection is { Owner: 1, EffectResourceId: 573 } &&
              wipe.PendingCardSelection.EligibleCards.Single().Card == eligibleWitch &&
              wipe.ResolveCardSelection(
                  wipe.PendingCardSelection.EligibleCards.Single().SourceIndex, out _) &&
              wipe.PendingCardSelection is null &&
              wipe.HandFor(0).Single() == eligibleSangan &&
              wipe.HandFor(1).Single() == eligibleWitch,
              "Torrential: sequential graveyard searches still resolve on each player's deck");

        var self = DemoDuel.FromPartialObservation(0, [torrential, strong],
            Slots(small), Slots(exact), 8000);
        self.SetFromHand(0, out _); self.NextTurn(); self.NextTurn();
        check(self.SummonLeftmost(0, false, out _) &&
              self.PendingSummonResponse is { Summon: { Player: 0 } } &&
              self.PendingSummonResponse.EligibleTraps.Single().Owner == 0 &&
              self.ActivateSummonTrap(0, 0, out _) &&
              self.LastMonsterWipe is { DestroyedPlayer0: 2, DestroyedPlayer1: 1 },
              "Torrential: controller can react to own successful Normal Summon on later turn");

        var passed = DemoDuel.FromPartialObservation(0, [trapHole],
            Slots(), Slots(), 8000, side1Deck: [senju, ritual]);
        passed.SetFromHand(0, out _); passed.NextTurn(); passed.Draw(out _);
        check(passed.SummonLeftmost(0, false, out _) &&
              passed.PendingSummonResponse is not null &&
              passed.PendingCardSelection is null &&
              passed.PassSummonResponse(out _) &&
              passed.PendingSummonResponse is null &&
              passed.PendingCardSelection is { Owner: 1, EffectResourceId: 1109,
                  Source: CardSelectionSource.Deck } &&
              passed.Spells[0, 0] == trapHole &&
              passed.ResolveCardSelection(0, out _) &&
              passed.HandFor(1).Single() == ritual,
              "P165: passing response preserves optional Ritual search, without spending Trap");

        var triggered = DemoDuel.FromPartialObservation(0, [trapHole],
            Slots(), Slots(), 8000, side1Deck: [senju, ritual]);
        triggered.SetFromHand(0, out _); triggered.NextTurn(); triggered.Draw(out _);
        check(triggered.SummonLeftmost(0, false, out _) &&
              triggered.PendingSummonResponse is not null &&
              triggered.ActivateSummonTrap(0, 0, out _) &&
              triggered.Monsters[1, 0] is null &&
              triggered.PendingCardSelection is { Owner: 1, EffectResourceId: 1109 } &&
              triggered.ResolveCardSelection(0, out _) &&
              triggered.HandFor(1).Single() == ritual,
              "P165: once successfully summoned, Senju's trigger survives Trap Hole destruction");

        // A special summon during battle is deliberately NOT given a Main
        // Phase Normal/Flip response; that would create an invalid trap window.
        var tomato = Monster(1123, 300, 1400, 1100);
        var dark = Monster(90, 301, 1000, 900);
        var battle = DemoDuel.FromPartialObservation(1, [], Slots(tomato),
            Slots(strong), 8000, side0Deck: [dark]);
        battle.Spells[1, 0] = torrential;
        battle.SpellFaceDown[1, 0] = true; // preexisting fixture, not set now
        check(battle.BeginBattle(out _) && battle.Attack(0, 0, out _) &&
              battle.PendingSummonResponse is null &&
              battle.PendingCardSelection is { Destination: CardSelectionDestination.Field } &&
              battle.ResolveCardSelection(0, out _) &&
              battle.Monsters[0, 0] == dark &&
              battle.PendingSummonResponse is null &&
              battle.Spells[1, 0] == torrential,
              "P038: damage-step recruitment is not offered a bogus Normal/Flip response");
    }
}
