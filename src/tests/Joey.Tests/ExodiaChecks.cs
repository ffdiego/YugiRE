using Joey.Core;

internal static class ExodiaChecks
{
    private static Card Piece(string name, int index) =>
        new(index, (ushort)(3000 + index), name, "",
            (1u << 20) | (3u << 25));

    public static void Run(Action<bool, string> check)
    {
        Card[] five = ExodiaRules.RequiredNames
            .Select((name, index) => Piece(name, index)).ToArray();
        Card filler = Piece("Other monster", 20);
        Card?[] emptyField = new Card?[5];

        check(ExodiaRules.CompleteHand(five)?.Select(c => c.Name)
                  .SequenceEqual(ExodiaRules.RequiredNames) == true,
              "Exodia: five distinct pieces are recognized in native display order");
        check(ExodiaRules.CompleteHand([.. five.Reverse(), filler])?.Count == 5 &&
              ExodiaRules.CompleteHand(five.Select(c => c with { Name = c.Name.ToUpperInvariant() }))?.Count == 5,
              "Exodia: hand order, unrelated extra cards and case do not affect recognition");
        check(ExodiaRules.CompleteHand([five[0], five[1], five[2], five[3], five[3]]) is null &&
              ExodiaRules.CompleteHand([.. five.Take(4), Piece("Exodia Necross", 19)]) is null &&
              ExodiaRules.CompleteHand([.. five.Take(4),
                  five[4] with { Properties = 22u << 20 }]) is null,
              "Exodia: duplicates, other Exodia cards and non-monster impostors do not count");

        var snapshot = DemoDuel.FromPartialObservation(0, five, emptyField,
            emptyField, 8000);
        check(snapshot.Winner == 0 && snapshot.ExodiaWinner == 0 &&
              snapshot.GameOver && !snapshot.CanDraw &&
              !snapshot.Draw(out _),
              "Exodia: an already complete observed hand ends the duel and locks actions");

        for (int player = 0; player < 2; player++)
        {
            var duel = DemoDuel.FromPartialObservation(player, five.Take(4).ToArray(),
                emptyField, emptyField, 8000,
                side0Deck: player == 0 ? [five[4]] : null,
                side1Deck: player == 1 ? [five[4]] : null);
            var events = new List<DemoExodiaCompletion>();
            List<string> eventOrder = [];
            duel.HandTransferred += _ => eventOrder.Add("draw");
            duel.ExodiaCompleted += completed =>
            {
                eventOrder.Add("exodia");
                events.Add(completed);
            };

            check(!duel.GameOver && duel.Draw(out var message) &&
                  duel.Winner == player && duel.ExodiaWinner == player &&
                  duel.GameOver && duel.Life.SequenceEqual([8000, 8000]) &&
                  message.Contains("Exodia", StringComparison.OrdinalIgnoreCase),
                  $"Exodia: player {player} wins immediately upon drawing the fifth distinct piece");
            check(eventOrder.SequenceEqual(["draw", "exodia"]) &&
                  events is [{ Player: var winner, Pieces: var cards }] &&
                  winner == player && cards.Select(c => c.Name).SequenceEqual(ExodiaRules.RequiredNames) &&
                  duel.DeckCount(player) == 0 && duel.HandFor(player).Count == 5 &&
                  !duel.Draw(out _) && events.Count == 1,
                  $"Exodia: player {player} emits once, after the fifth card transfer animation");
        }

        // Sangan is a field-to-graveyard searcher, not a FLIP effect.
        // Tribute it, choose the final Exodia piece from Deck, then win on
        // the committed transfer to Hand without inventing a new card effect.
        Card sangan = new(500, 47, "Sangan", "",
            (1u << 20) | (3u << 25) | (100u << 9) | 60u);
        Card tributeSummon = new(501, 2000, "Tribute test monster", "",
            (1u << 20) | (5u << 25));
        var searched = DemoDuel.FromPartialObservation(0,
            [tributeSummon, .. five.Take(4)],
            [sangan, null, null, null, null], emptyField, 8000,
            side0Deck: [five[4], filler]);
        var searchEvents = new List<DemoExodiaCompletion>();
        searched.ExodiaCompleted += searchEvents.Add;
        check(searched.Summon(0, 0, 0, true, out _, [0]) &&
              searched.PendingCardSelection is { Owner: 0,
                  Source: CardSelectionSource.Deck,
                  Destination: CardSelectionDestination.Hand } &&
              searched.ExodiaWinner is null && searched.Winner is null,
              "Exodia: Sangan's valid tribute can open a search without prematurely winning");
        check(searched.ResolveCardSelection(0, out _) &&
              searched.Winner == 0 && searched.ExodiaWinner == 0 &&
              searchEvents.Count == 1 && searched.HandFor(0).Count == 5,
              "Exodia: a completed Sangan search for the fifth piece wins immediately");

        // A monster returned from the field to the owner's hand counts
        // only after the bounce is committed. Hane-Hane may target the
        // opponent's monster during the opponent's own turn.
        Card hane = new(502, FlipEffectRules.HaneHane, "Hane-Hane", "",
            (11u << 20) | (4u << 25));
        var bounced = DemoDuel.FromPartialObservation(0,
            five.Take(4).ToArray(),
            [five[4], null, null, null, null],
            [hane, null, null, null, null], 8000);
        bounced.MonsterFaceDown[1, 0] = bounced.DefensePosition[1, 0] = true;
        check(bounced.ExodiaWinner is null && bounced.NextTurn() &&
              bounced.ChangePosition(0, out _) &&
              bounced.PendingFlipTarget is { Effect: DemoFlipEffect.ReturnOne },
              "Exodia: an Exodia piece on the field does not win before being returned");
        check(bounced.ResolveFlipTarget(0, 0, out _) &&
              bounced.ExodiaWinner == 0 && bounced.Winner == 0 &&
              bounced.Monsters[0, 0] is null && bounced.HandFor(0).Count == 5,
              "Exodia: Hane-Hane returning the fifth piece to the opponent's hand triggers the owner's win");

        var notAllInHand = DemoDuel.FromPartialObservation(0, five.Take(4).ToArray(),
            [five[4], null, null, null, null], emptyField, 8000,
            side0Deck: [filler]);
        check(notAllInHand.Draw(out _) && notAllInHand.ExodiaWinner is null &&
              !notAllInHand.GameOver,
              "Exodia: a piece on the field is not treated as present in hand");
    }
}
