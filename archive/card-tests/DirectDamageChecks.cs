using Joey.Core;

internal static class DirectDamageChecks
{
    private static readonly (ushort Id, int Amount)[] Cases = [(342,200), (343,500), (344,600), (345,800), (1007,300)];
    private static DemoDuel Duel(ushort id, int side = 0) => DemoDuel.FromPartialObservation(side,
        [new Card(999, id, $"Synthetic {id}", "", 22u << 20)], new Card?[5], new Card?[5], 8000);
    private static string State(DemoDuel d) => string.Join(";", d.Turn, d.Phase, d.Winner,
        string.Join(",", d.Life), string.Join(",", d.Hand.Select(c => c.Id)),
        string.Join(",", Enumerable.Range(0, 2).SelectMany(p => d.Graveyard(p)).Select(c => c.Id)),
        string.Join(",", d.Spells.Cast<Card?>().Select(c => c?.Id.ToString() ?? "null")),
        string.Join(",", d.SpellFaceDown.Cast<bool>()));
    public static void Run(Action<bool, string> check)
    {
        check(Enumerable.Range(0,256).All(b => DirectDamageRules.PrepareTarget((byte)b) == 1-(b&1)) &&
            DirectDamageRules.PrepareTarget(null) is null, "P045 preparation masks source bit, unknown remains unknown");
        foreach (var (id, amount) in Cases)
        {
            bool exhaustive = true;
            for (int flags = 0; flags < 256; flags++)
            for (int target = 0; target < 256; target++)
            {
                var r = DirectDamageRules.Resolve(id, (byte)flags, (byte)target);
                exhaustive &= (flags & 4) != 0
                    ? r.Status == DirectDamageStatus.SkippedByRequestFlag && r.Command is null
                    : r.Status == DirectDamageStatus.Command && r.Command is { } c && c.Amount == amount &&
                      c.TargetPlayer == (target & 1) && c.CommandWord == (0x43 | ((target & 1) << 15)) && c.ThirdField == 0 && c.FourthField == 1;
            }
            check(exhaustive, $"P045 {id}: all flag/target bytes produce bounded command or skip");
            check(DirectDamageRules.Resolve(id, null, 0).Status == DirectDamageStatus.Unsupported &&
                DirectDamageRules.Resolve(id, 0, null).Status == DirectDamageStatus.Unsupported &&
                DirectDamageRules.Resolve(id, 4, null).Status == DirectDamageStatus.SkippedByRequestFlag,
                $"P045 {id}: incomplete context is unsupported; guarded path does not require target");
            foreach (int side in new[] { 0, 1 })
            {
                var hand = Duel(id, side);
                check(hand.CanActivateFromHand(0) && hand.Play(0, out _) && hand.Life[1-side] == 8000-amount &&
                    hand.Life[side] == 8000 && hand.Hand.Count == 0 && hand.Graveyard(side).Single().Id == id,
                    $"P045 {id}: hand activation side {side}, exact LP and owner graveyard");
                var set = Duel(id, side);
                check(set.SetFromHand(0, out _) && set.Life[1-side] == 8000 && set.Graveyard(side).Count == 0 &&
                    set.ActivateSetSpell(0, out _) && set.Life[1-side] == 8000-amount && set.Spells[side,0] is null &&
                    !set.SpellFaceDown[side,0] && set.Graveyard(side).Single().Id == id,
                    $"P045 {id}: setting does not resolve; activation consumes own set card side {side}");
                string after = State(set);
                check(!set.ActivateSetSpell(0, out _) && State(set) == after, $"P045 {id}: no repeated set activation");
            }
            foreach (int lp in new[] { 1, amount-1, amount, amount+1, 8000 })
            {
                var d = Duel(id); d.Life[1] = lp;
                check(d.Play(0, out _) && d.Life[1] == Math.Max(0,lp-amount) && d.Winner == (lp <= amount ? 0 : (int?)null),
                    $"P045 {id}: LP boundary {lp} with demo winner policy");
            }
            var full = Duel(id);
            for (int z=0; z<5; z++) full.Spells[0,z] = new Card(z,300,"Occupied","",21u<<20);
            string before = State(full);
            check(!full.CanActivateFromHand(0) && !full.Play(0, out _) && State(full) == before,
                $"P045 {id}: full spell zones reject hand activation without mutation");
            var battle = Duel(id, 1); battle.BeginBattle(out _); before = State(battle);
            check(!battle.Play(0, out _) && !battle.CanActivateFromHand(0) && State(battle) == before,
                $"P045 {id}: battle phase rejects hand activation without mutation");
            var ended = Duel(id); ended.Life[1] = 1; ended.Play(0, out _); before = State(ended);
            check(!ended.Play(0, out _) && !ended.CanActivateFromHand(0) && State(ended) == before,
                $"P045 {id}: post-win activation rejected without mutation");
        }
        foreach (ushort id in new ushort[] { 0, 88, 537, 1324, 2342, 65535 })
        {
            check(DirectDamageRules.Amount(id) is null && DirectDamageRules.Resolve(id, 0, 1).Status == DirectDamageStatus.Unsupported,
                $"P045 unsupported resource {id} is not normalized or inferred from shared primary");
            var d = Duel(id); string before = State(d);
            check(!d.CanActivateFromHand(0) && !d.Play(0, out _) && State(d) == before, $"P045 unsupported {id} leaves demo state unchanged");
        }
        var invalid = Duel(342); string original = State(invalid);
        check(!invalid.Play(-1,out _) && !invalid.Play(1,out _) && !invalid.ActivateSetSpell(-1,out _) &&
            !invalid.ActivateSetSpell(5,out _) && !invalid.ActivateSetSpell(0,out _) && State(invalid)==original,
            "P045 invalid indices/empty set slot preserve state");
    }
}
