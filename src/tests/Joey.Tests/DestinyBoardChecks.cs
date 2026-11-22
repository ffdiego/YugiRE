using Joey.Core;

internal static class DestinyBoardChecks
{
    private static Card Card(ushort id, int index, string name, int classCode) =>
        new(index, id, name, "", (uint)classCode << 20);

    public static void Run(Action<bool, string> check)
    {
        var board = Card(1528, 68, "Destiny Board", 21);
        Card[] letters =
        [
            Card(1541, 371, "Spirit Message I", 22),
            Card(1542, 373, "Spirit Message N", 22),
            Card(1543, 374, "Spirit Message A", 22),
            Card(1544, 372, "Spirit Message L", 22)
        ];
        var dummy = Card(300, 1, "Placeholder spell", 22);
        var slots = new Card?[2, 5];
        var faceDown = new bool[2, 5];
        slots[0, 0] = board;
        check(DestinyBoardRules.HasActiveBoard(0, slots, faceDown) &&
              DestinyBoardRules.NextMessage(0, slots, faceDown) == 1541 &&
              DestinyBoardRules.NextMessage(1, slots, faceDown) is null &&
              DestinyBoardRules.CompleteField(0, slots, faceDown) is null,
              "Destiny Board: active face-up board requires all four distinct messages");
        for (int i = 0; i < 4; i++)
        {
            check(DestinyBoardRules.NextMessage(0, slots, faceDown) == letters[i].Id,
                  $"Destiny Board: next missing letter follows I/N/A/L index {i}");
            slots[0, i + 1] = letters[i];
        }
        check(DestinyBoardRules.CompleteField(0, slots, faceDown)?.Select(c => c.Id)
                  .SequenceEqual([1528, 1541, 1542, 1543, 1544]) == true &&
              DestinyBoardRules.NextMessage(0, slots, faceDown) is null,
              "Destiny Board: five face-up field cards complete F-I-N-A-L in canonical order");
        faceDown[0, 4] = true;
        check(DestinyBoardRules.CompleteField(0, slots, faceDown) is null,
              "Destiny Board: a set Spirit Message does not satisfy the field condition");
        faceDown[0, 4] = false;
        slots[0, 4] = dummy;
        check(DestinyBoardRules.CompleteField(0, slots, faceDown) is null,
              "Destiny Board: other spells do not substitute for the final message");
        slots[0, 4] = letters[3];
        faceDown[0, 0] = true;
        check(!DestinyBoardRules.HasActiveBoard(0, slots, faceDown) &&
              DestinyBoardRules.NextMessage(0, slots, faceDown) is null &&
              DestinyBoardRules.CompleteField(0, slots, faceDown) is null,
              "Destiny Board: all messages still require a face-up source trap");

        var outOfOrder = new Card?[2, 5];
        outOfOrder[0, 0] = board;
        outOfOrder[0, 1] = letters[1];
        check(DestinyBoardRules.NextMessage(0, outOfOrder, new bool[2, 5]) is null,
              "Destiny Board: do not silently reorder Spirit Messages on an inconsistent board");

        var alone = DemoDuel.FromPartialObservation(0, [letters[0]],
            new Card?[5], new Card?[5], 8000);
        check(!alone.CanSetFromHand(0) && !alone.SetFromHand(0, out _) &&
              alone.Hand.Count == 1,
              "Destiny Board: Spirit Messages cannot be set normally from the hand");

        foreach (int owner in new[] { 0, 1 })
        {
            var duel = DemoDuel.FromPartialObservation(owner, [board],
                new Card?[5], new Card?[5], 8000,
                side0Deck: owner == 0 ? [dummy, letters[0], letters[1], letters[2], letters[3]] : [dummy],
                side1Deck: owner == 1 ? [dummy, letters[0], letters[1], letters[2], letters[3]] : [dummy]);
            List<DemoDestinyBoardMessagePlaced> placements = [];
            List<DemoDestinyBoardCompletion> completions = [];
            duel.DestinyBoardMessagePlaced += placements.Add;
            duel.DestinyBoardCompleted += completions.Add;

            check(duel.SetFromHand(0, out _) && duel.Spells[owner, 0] == board &&
                  duel.SpellFaceDown[owner, 0] &&
                  !duel.ActivateSetSpell(0, out _) &&
                  placements.Count == 0,
                  $"Destiny Board P{owner}: set-turn trap cannot immediately activate");

            // Return to the owner's next Main Phase: only then activate.
            duel.NextTurn(); duel.NextTurn();
            check(duel.ActivePlayer == owner &&
                  duel.ActivateSetSpell(0, out _) &&
                  !duel.SpellFaceDown[owner, 0] &&
                  duel.Winner is null,
                  $"Destiny Board P{owner}: face-up activation is a separate committed action");
            for (int i = 0; i < 4; i++)
            {
                int before = duel.DeckCount(owner);
                check(duel.EndTurn(out _) && duel.ActivePlayer == 1 - owner &&
                      placements.Count == i && duel.Winner is null,
                      $"Destiny Board P{owner}: own End Phase places no letter at step {i}");
                check(duel.EndTurn(out var summary) &&
                      placements.Count == i + 1 &&
                      placements[^1] is { Player: var side, Zone: var zone, FromDeck: true } &&
                      side == owner && zone == i + 1 &&
                      placements[^1].Card.Id == letters[i].Id &&
                      duel.Spells[owner, i + 1]?.Id == letters[i].Id &&
                      !duel.SpellFaceDown[owner, i + 1] &&
                      duel.DeckCount(owner) == before - 1 &&
                      duel.Life.SequenceEqual([8000, 8000]),
                      $"Destiny Board P{owner}: opponent End Phase places letter {i} from deck without LP changes");
                if (i == 3)
                    check(duel.Winner == owner && duel.DestinyBoardWinner == owner &&
                          duel.GameOver && duel.ActivePlayer == 1 - owner &&
                          summary.Contains("F-I-N-A-L", StringComparison.OrdinalIgnoreCase) &&
                          completions is [{ Player: var p, Pieces: var pieces }] &&
                          p == owner && pieces.Select(c => c.Id)
                             .SequenceEqual([1528, 1541, 1542, 1543, 1544]) &&
                          !duel.EndTurn(out _) && completions.Count == 1,
                          $"Destiny Board P{owner}: fifth face-up card wins without changing LP or advancing turn");
                else
                    check(duel.Winner is null && duel.ActivePlayer == owner &&
                          duel.DestinyBoardWinner is null && completions.Count == 0,
                          $"Destiny Board P{owner}: first {i + 1} messages do not win");
            }
            check(duel.DeckSnapshot(owner).SequenceEqual([dummy]),
                  $"Destiny Board P{owner}: selecting messages preserves unrelated deck card order");
        }

        var blocked = DemoDuel.FromPartialObservation(0, [board],
            new Card?[5], new Card?[5], 8000, side0Deck: letters);
        blocked.SetFromHand(0, out _);
        blocked.NextTurn(); blocked.NextTurn();
        blocked.ActivateSetSpell(0, out _);
        for (int zone = 1; zone < 5; zone++) blocked.Spells[0, zone] = dummy;
        int kept = blocked.DeckCount(0);
        blocked.NextTurn(); blocked.NextTurn();
        check(blocked.DeckCount(0) == kept && blocked.DestinyBoardWinner is null &&
              blocked.Spells[0, 0] == board && blocked.Spells[0, 1] == dummy,
              "Destiny Board: full spell zone does not consume a message from Deck");
    }
}
