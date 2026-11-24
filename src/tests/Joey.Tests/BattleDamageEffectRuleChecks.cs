using Joey.Core;

public static class BattleDamageEffectRuleChecks
{
    public static void Run(Action<bool, string> check)
    {
        foreach (int side in new[] { 0, 1 })
        {
            int enemy = 1 - side;
            var cases = new (ushort Id, BattleDamageEffectKind Kind, int Recipient, int Count)[]
            {
                (113, BattleDamageEffectKind.RandomDiscard, enemy, 1),
                (219, BattleDamageEffectKind.Draw, side, 1),
                (522, BattleDamageEffectKind.Draw, enemy, 2),
                (1328, BattleDamageEffectKind.SearchParasite, side, 1),
                (1511, BattleDamageEffectKind.BanishOpponentGraveyard, enemy, 2)
            };
            foreach (var item in cases)
            {
                var expected = new BattleDamageEffectIntent(item.Id, side, item.Recipient, item.Kind, item.Count);
                check(BattleDamageEffectRules.AfterBattleDamage(item.Id, side, enemy, 1, false) == expected,
                    $"Battle damage intent {item.Id}, side {side}");
                check(BattleDamageEffectRules.AfterBattleDamage(item.Id, side, enemy, 0, false) == null &&
                    BattleDamageEffectRules.AfterBattleDamage(item.Id, side, side, 500, false) == null &&
                    BattleDamageEffectRules.AfterBattleDamage(item.Id, side, enemy, 500, true) == null,
                    $"Battle damage intent {item.Id}: zero, own-side and lethal gates");
            }
        }
        check(BattleDamageEffectRules.AfterBattleDamage(999, 0, 1, 500, false) == null,
            "Unrelated monster has no followup");
        int calls = 0;
        int Next(int count) { calls++; return count - 1; }
        check(BattleDamageEffectRules.ChooseRandomDiscardIndex(0, Next) == null && calls == 0,
            "Empty hand consumes no random choice");
        check(BattleDamageEffectRules.ChooseRandomDiscardIndex(3, Next) == 2 && calls == 1,
            "Random discard chooses from current hand once");
        check(BattleDamageEffectRules.ChooseRandomDiscardIndex(1, Next) == 0 && calls == 2,
            "Single-card hand chooses its only occurrence");
        bool invalid = false;
        try { BattleDamageEffectRules.ChooseRandomDiscardIndex(3, n => n); }
        catch (InvalidOperationException) { invalid = true; }
        check(invalid, "Reject out-of-range RNG instead of discarding wrong occurrence");
    }
}
