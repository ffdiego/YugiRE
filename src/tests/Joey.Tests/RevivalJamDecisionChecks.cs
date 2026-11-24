using Joey.Core;

internal static class RevivalJamDecisionChecks
{
    private static Card Monster(ushort id, int index, int attack = 1500, int defense = 500) =>
        new(index, id, $"Monster {id}", "",
            (14u << 20) | (4u << 25) |
            ((uint)(attack / 10) << 9) | (uint)(defense / 10));

    private static Card?[] Slots(Card? card = null) => [card, null, null, null, null];

    // Player 1 is attacking on turn 2; the defender belongs to player 0.
    private static DemoDuel DestroyJam(Card jam, int life0 = 8000, Card? attacker = null)
    {
        var duel = DemoDuel.FromPartialObservation(1, [], Slots(jam),
            Slots(attacker ?? Monster(900, 100, attack: 2000)), 8000,
            side0Deck: [Monster(991, 1991)], side1Deck: [Monster(992, 1992)]);
        duel.Life[0] = life0;
        if (!duel.BeginBattle(out _) || !duel.Attack(0, 0, out _))
            throw new InvalidOperationException("Revival Jam test battle did not resolve.");
        return duel;
    }

    public static void Run(Action<bool, string> check)
    {
        var jam = Monster(RevivalJamRules.ResourceId, 1060);
        var declined = DestroyJam(jam);
        var request = declined.PendingEffectDecision;
        check(request is not null && request.Source == jam &&
              request.Owner == 0 && request.LifePointCost == 1000 &&
              declined.Graveyard(0).Any(c => ReferenceEquals(c, request.Source)),
            "P002: battle destruction opens optional owner LP-cost decision");
        if (request is null) throw new InvalidOperationException("Expected P002 decision.");

        check(EffectDecisionPolicies.Choose(EffectPromptMode.Ask, request) is null &&
              EffectDecisionPolicies.Choose(EffectPromptMode.Off, request) ==
                  EffectDecisionAnswer.Decline &&
              EffectDecisionPolicies.Choose(EffectPromptMode.Automatic, request) ==
                  EffectDecisionAnswer.Decline &&
              EffectDecisionPolicies.Choose(EffectPromptMode.Automatic, request,
                  _ => EffectDecisionAnswer.Accept) == EffectDecisionAnswer.Accept,
            "P002: showing a prompt is separate from an explicit decision policy");

        int lifeBefore = declined.Life[0];
        check(!declined.ResolveEffectDecision(request.Id + 1, EffectDecisionAnswer.Accept, out _) &&
              declined.Life[0] == lifeBefore && declined.PendingEffectDecision == request &&
              !declined.EndTurn(out _),
            "P002: stale request cannot pay or advance a pending duel");
        check(declined.ResolveEffectDecision(request.Id, EffectDecisionAnswer.Decline, out _) &&
              declined.Life[0] == lifeBefore &&
              declined.PendingDelayedRevivals.Count == 0 &&
              declined.EffectDecisionHistory.Count == 1 &&
              declined.EffectDecisionHistory[0].Answer == EffectDecisionAnswer.Decline &&
              declined.EndTurn(out _) && declined.Draw(out _) &&
              declined.Monsters[0, 0] is null,
            "P002: refusal costs no LP and does not revive after the next Draw");

        var acceptedJam = Monster(RevivalJamRules.ResourceId, 1060);
        var accepted = DestroyJam(acceptedJam);
        var pay = accepted.PendingEffectDecision!;
        int lpAfterBattle = accepted.Life[0];
        check(accepted.ResolveEffectDecision(pay.Id, EffectDecisionAnswer.Accept, out _) &&
              accepted.Life[0] == lpAfterBattle - 1000 &&
              accepted.PendingDelayedRevivals.Count == 1 &&
              accepted.PendingDelayedRevivals[0].DueTurn == 3 &&
              accepted.Monsters[0, 0] is null &&
              !accepted.ResolveEffectDecision(pay.Id, EffectDecisionAnswer.Accept, out _),
            "P002: accepting charges LP once and schedules the next owner turn");
        check(accepted.EndTurn(out _) && accepted.Turn == 3 &&
              accepted.Monsters[0, 0] is null &&
              accepted.PendingDelayedRevivals.Count == 1,
            "P002 native order: advancing turn alone does not revive before the Draw");
        check(accepted.Draw(out _) &&
              ReferenceEquals(accepted.Monsters[0, 0], pay.Source) &&
              accepted.DefensePosition[0, 0] && !accepted.MonsterFaceDown[0, 0] &&
              !accepted.Graveyard(0).Contains(acceptedJam) &&
              accepted.PendingDelayedRevivals.Count == 0,
            "P002 native order: exact source returns in defense after the owner's Draw");

        var poor = DestroyJam(Monster(RevivalJamRules.ResourceId, 1060), life0: 1500);
        check(poor.Life[0] == 1000 &&
              poor.PendingEffectDecision is null &&
              poor.Graveyard(0).Count == 1,
            "P002: insufficient LP cannot open an unpayable cost decision");

        var blockedJam = Monster(RevivalJamRules.ResourceId, 1060);
        var blocked = DestroyJam(blockedJam);
        var blockedDecision = blocked.PendingEffectDecision!;
        check(blocked.ResolveEffectDecision(blockedDecision.Id,
                  EffectDecisionAnswer.Accept, out _),
            "P002: payment can be made before a later blocked summon");
        int blockedLife = blocked.Life[0];
        for (int zone = 0; zone < 5; zone++)
            blocked.Monsters[0, zone] = Monster((ushort)(950 + zone), 2000 + zone);
        check(blocked.EndTurn(out _) && blocked.PendingDelayedRevivals.Count == 1 &&
              blocked.Draw(out _) && blocked.PendingDelayedRevivals.Count == 0 &&
              blocked.Graveyard(0).Any(c => ReferenceEquals(c, blockedDecision.Source)) &&
              blocked.Life[0] == blockedLife,
            "P002: a full field at post-Draw Standby cancels revival without LP refund");

        var jamAttacker = Monster(RevivalJamRules.ResourceId, 1060);
        var jamDefender = Monster(RevivalJamRules.ResourceId, 2060);
        var both = DestroyJam(jamDefender, attacker: jamAttacker);
        var first = both.PendingEffectDecision;
        check(first is not null && first.Owner == 1 &&
              both.Graveyard(0).Count == 1 && both.Graveyard(1).Count == 1,
            "P002: simultaneous battle destruction retains both card occurrences");
        check(first is not null &&
              both.ResolveEffectDecision(first.Id, EffectDecisionAnswer.Accept, out _) &&
              both.PendingEffectDecision is { Owner: 0 } second &&
              both.ResolveEffectDecision(second.Id, EffectDecisionAnswer.Decline, out _) &&
              both.PendingEffectDecision is null &&
              both.PendingDelayedRevivals.Count == 1,
            "P002: multiple optional decisions are serialized without losing a trigger");
        check(both.EndTurn(out _) && both.Turn == 3 &&
              both.Draw(out _) && both.Monsters[1, 0] is null &&
              both.EndTurn(out _) && both.Turn == 4 &&
              both.Monsters[1, 0] is null && both.PendingDelayedRevivals.Count == 1 &&
              both.Draw(out _) &&
              ReferenceEquals(both.Monsters[1, 0], first!.Source) &&
              both.DefensePosition[1, 0],
            "P002: owner already active at destruction waits until post-Draw Standby of next turn");
    }
}
