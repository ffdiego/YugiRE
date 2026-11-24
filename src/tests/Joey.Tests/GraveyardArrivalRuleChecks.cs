using Joey.Core;

public static class GraveyardArrivalRuleChecks
{
    public static void Run(Action<bool, string> check)
    {
        foreach (int owner in new[] { 0, 1 })
        {
            int enemy = 1 - owner;
            foreach (ushort id in new ushort[] { 462, 533 })
            {
                GraveyardArrivalIntent? Resolve(GraveyardArrivalCause cause,
                    int? hand, int? actor, bool arrived = true, bool ended = false) =>
                    GraveyardArrivalRules.AfterMovement(id, owner, hand, actor, cause, arrived, ended);

                var expected = id == 462
                    ? new GraveyardArrivalIntent(id, GraveyardArrivalEffectKind.Draw, owner, 2)
                    : new GraveyardArrivalIntent(id, GraveyardArrivalEffectKind.Damage, enemy, 1000);
                check(Resolve(GraveyardArrivalCause.DiscardByEffect, owner, enemy) == expected,
                    $"Opponent discard {id}, owner {owner}");
                check(Resolve(GraveyardArrivalCause.DiscardByEffect, owner, owner) is null,
                    $"Own effect does not trigger {id}, owner {owner}");
                check(Resolve(GraveyardArrivalCause.DiscardAsCost, owner, enemy) is null,
                    $"Cost does not trigger {id}, owner {owner}");
                check(Resolve(GraveyardArrivalCause.HandLimit, owner, null) is null,
                    $"Hand limit does not trigger {id}, owner {owner}");
                check(Resolve(GraveyardArrivalCause.DiscardByEffect, enemy, enemy) is null &&
                    Resolve(GraveyardArrivalCause.DiscardByEffect, null, enemy) is null &&
                    Resolve(GraveyardArrivalCause.DiscardByEffect, owner, null) is null,
                    $"No assumed source or actor {id}, owner {owner}");
                check(Resolve(GraveyardArrivalCause.DiscardByEffect, owner, enemy, false) is null &&
                    Resolve(GraveyardArrivalCause.DiscardByEffect, owner, enemy, true, true) is null,
                    $"Actual arrival and game-over gates {id}, owner {owner}");
                check(Resolve(GraveyardArrivalCause.Mill, null, enemy) is null &&
                    Resolve(GraveyardArrivalCause.EffectDestruction, null, enemy) is null,
                    $"Non-hand movements do not trigger {id}, owner {owner}");
                check(Resolve(GraveyardArrivalCause.SendFromHandByEffect, owner, enemy)
                    is { Kind: GraveyardArrivalEffectKind.ReviewDirectHandSend, IsResolvable: false },
                    $"Historical direct-send ambiguity fails closed {id}, owner {owner}");
            }

            foreach (ushort id in new ushort[] { 478, 1042 })
            {
                foreach (var cause in Enum.GetValues<GraveyardArrivalCause>())
                {
                    int? hand = cause is GraveyardArrivalCause.DiscardByEffect or
                        GraveyardArrivalCause.SendFromHandByEffect or GraveyardArrivalCause.DiscardAsCost or
                        GraveyardArrivalCause.HandLimit ? owner : null;
                    var result = GraveyardArrivalRules.AfterMovement(id, owner, hand, enemy, cause, true, false);
                    check(result == new GraveyardArrivalIntent(id, GraveyardArrivalEffectKind.ReturnToDeckTop, owner, 1),
                        $"Deck-top intent {id}, owner {owner}, {cause}");
                }
                check(GraveyardArrivalRules.AfterMovement(id, owner, null, enemy,
                        GraveyardArrivalCause.BattleDestruction, false, false) is null &&
                    GraveyardArrivalRules.AfterMovement(id, owner, null, enemy,
                        GraveyardArrivalCause.BattleDestruction, true, true) is null,
                    $"Deck-top intent requires completed movement and live duel {id}");
            }
        }
        check(GraveyardArrivalRules.AfterMovement(1242, 0, 0, 1,
                GraveyardArrivalCause.DiscardByEffect, true, false) is null,
            "Gilfer is deliberately outside this implementation");
        check(GraveyardArrivalRules.AfterMovement(999, 0, 0, 1,
                GraveyardArrivalCause.DiscardByEffect, true, false) is null,
            "Unrelated card does not produce intent");
    }
}
