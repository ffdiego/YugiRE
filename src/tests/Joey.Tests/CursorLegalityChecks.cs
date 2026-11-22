using Joey.Core;
using Joey.LookAndFeel;

internal static class CursorLegalityChecks
{
    public static void Run(Action<bool, string> check)
    {
        static NativeCursorAction? ActionFor(DemoDuel duel, int index, bool requestSet) =>
            NativeDuelCursor.HandAction(
                duel.Hand[index].IsMonster,
                duel.CanSummonFromHand(index),
                duel.CanSetFromHand(index),
                duel.CanActivateFromHand(index),
                requestSet,
                discarding: false);

        Card destinyBoard = new(68, 1528, "Destiny Board", "", 21u << 20);
        Card fieldSpell = new(490, 332, "Sogen", "", 22u << 20);
        Card monster = new(10, 120, "Test monster", "", (1u << 20) | (4u << 25));
        Card boss = new(11, 121, "Tribute boss", "", (1u << 20) | (7u << 25));
        Card?[] empty = new Card?[5];

        var hand = DemoDuel.FromPartialObservation(0,
            [destinyBoard, fieldSpell, monster, boss], empty, empty, 8000);
        check(hand.CanSetFromHand(0) && !hand.CanActivateFromHand(0) &&
              ActionFor(hand, 0, requestSet: false) == NativeCursorAction.Set &&
              ActionFor(hand, 0, requestSet: true) == NativeCursorAction.Set &&
              !NativeDuelCursor.CanToggleHandAction(false, false,
                  hand.CanSetFromHand(0), hand.CanActivateFromHand(0)),
              "Trap in hand: Set is default and right-click cannot offer Activate");

        check(hand.CanSetFromHand(1) && hand.CanActivateFromHand(1) &&
              ActionFor(hand, 1, requestSet: false) == NativeCursorAction.Activate &&
              ActionFor(hand, 1, requestSet: true) == NativeCursorAction.Set &&
              NativeDuelCursor.CanToggleHandAction(false, false,
                  hand.CanSetFromHand(1), hand.CanActivateFromHand(1)),
              "Sogen: a spell with two real actions can switch Activate and Set");

        check(hand.CanSummonFromHand(2) && hand.CanSetFromHand(2) &&
              ActionFor(hand, 2, requestSet: false) == NativeCursorAction.Summon &&
              ActionFor(hand, 2, requestSet: true) == NativeCursorAction.Set,
              "Monster: right-click can switch between Summon and Set");

        check(!hand.CanSummonFromHand(3) && !hand.CanSetFromHand(3) &&
              ActionFor(hand, 3, requestSet: false) is null &&
              ActionFor(hand, 3, requestSet: true) is null,
              "Monster needing unavailable tributes shows no impossible cursor action");

        check(hand.SetFromHand(0, out _) && hand.Spells[0, 0]?.Id == destinyBoard.Id &&
              hand.Hand[0].Id == fieldSpell.Id,
              "Trap Set-only cursor maps to the Core's committed Set operation");

        var noSpace = DemoDuel.FromPartialObservation(0,
            [destinyBoard], empty, empty, 8000);
        for (int zone = 0; zone < 5; zone++)
            noSpace.Spells[0, zone] = destinyBoard;
        check(!noSpace.CanSetFromHand(0) && !noSpace.CanActivateFromHand(0) &&
              ActionFor(noSpace, 0, requestSet: false) is null,
              "Trap without an available spell/trap zone shows no impossible Set cloud");
    }
}
