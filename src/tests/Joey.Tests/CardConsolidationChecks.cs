using Joey.Core;

internal static class CardConsolidationChecks
{
    static Card Monster(ushort id, int index, int atk = 1000, int def = 1000) =>
        new(index,id,$"Monster {id}","",(11u << 20)|(4u << 25)|((uint)(atk/10)<<9)|(uint)(def/10));
    static Card?[] Slots(Card? card = null) => [card,null,null,null,null];

    public static void Run(Action<bool,string> check)
    {
        var command = new Card(1530,1530,"Royal Command","",21u << 20);
        var clown = Monster(119,201);
        var enemy = Monster(130,202);
        var vermin = Monster(1307,588);
        var copy = Monster(1307,1588);
        var warrior = Monster(255,46,1200,1000);

        var position = DemoDuel.FromPartialObservation(0,[],Slots(clown),Slots(enemy),8000);
        position.Spells[1,0] = command;
        check(position.FlipEffectsNegated && position.ChangePosition(0,out _) &&
              position.PendingFlipTarget is { Trigger: DemoMonsterEffectTrigger.PositionChange } &&
              position.ResolveFlipTarget(1,0,out _) && position.Graveyard(1).SequenceEqual([enemy]),
              "Consolidation: Royal Command does not negate Dream Clown position trigger");

        var initial = DemoDuel.FromPartialObservation(0,[],Slots(vermin),Slots(),8000,side0Deck:[copy]);
        initial.MonsterFaceDown[0,0] = initial.DefensePosition[0,0] = true;
        initial.Spells[1,0] = command;
        check(initial.ChangePosition(0,out _) && initial.PendingCardSelection is null &&
              initial.DeckSnapshot(0).SequenceEqual([copy]) && initial.MonsterWasFlipped[0,0],
              "Consolidation: Royal Command denies Bubonic Vermin at FLIP admission");

        var late = DemoDuel.FromPartialObservation(0,[],Slots(vermin),Slots(),8000,side0Deck:[copy]);
        late.MonsterFaceDown[0,0] = late.DefensePosition[0,0] = true;
        check(late.ChangePosition(0,out _) && late.PendingCardSelection is { EffectResourceId: 1307 },
              "Consolidation: Bubonic Vermin opens optional recruit");
        late.Spells[1,0] = command;
        check(late.ResolveCardSelection(0,out _) && late.PendingCardSelection is null &&
              late.DeckSnapshot(0).SequenceEqual([copy]) && late.Monsters[0,1] is null,
              "Consolidation: late Royal Command cancels Bubonic Vermin without paying/moving a card");

        var battle = DemoDuel.FromPartialObservation(1,[],Slots(vermin),Slots(warrior),8000,side0Deck:[copy]);
        battle.MonsterFaceDown[0,0] = battle.DefensePosition[0,0] = true;
        check(battle.BeginBattle(out _) && battle.Attack(0,0,out _) &&
              battle.PendingCardSelection is { EffectResourceId: 1307 } &&
              battle.ResolveCardSelection(0,out _) &&
              battle.Banished(0).SequenceEqual([vermin]) && battle.Banished(1).SequenceEqual([warrior]) &&
              battle.Graveyard(0).Count==0 && battle.Monsters[0,1]==copy && battle.MonsterFaceDown[0,1],
              "Consolidation: Bubonic FLIP resolves then P188 banishes original battle pair only");
    }
}
