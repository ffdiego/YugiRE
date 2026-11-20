using Joey.Core;
using Raylib_cs;

static partial class Game
{
    private const int CardSelectionPageSize = 10;
    private static int cardSelectionPage;
    private static PendingCardSelection? activeSelectionRequest;
    private static bool selectionFaceDownDefense;

    // Provisional sandbox UI. Native Joey coordinates/timings remain unmapped.
    static void DrawCardSelectionOverlay()
    {
        var request = duel.PendingCardSelection;
        if (request is null)
        {
            activeSelectionRequest = null;
            return;
        }
        if (!ReferenceEquals(request, activeSelectionRequest))
        {
            activeSelectionRequest = request;
            selectionFaceDownDefense = false;
            cardSelectionPage = 0;
        }

        int lastPage = Math.Max(0, (request.EligibleCards.Count - 1) / CardSelectionPageSize);
        cardSelectionPage = Math.Clamp(cardSelectionPage, 0, lastPage);
        Raylib.DrawRectangle(230, 80, 555, 502, new Color(9, 17, 26, 246));
        Raylib.DrawRectangleLines(230, 80, 555, 502, Gold);
        Label($"P{request.Owner + 1}: {request.EffectResourceId} card selection", 250, 95, 21, Gold);
        string rule = request.RequiredAttributeCode is byte attribute
            ? $"attribute {attribute}, ATK <= 1500" : request.Filter.ToString();
        Label($"{request.Source} -> {request.Destination} | {rule}", 250, 126, 17);
        Label(request.MinSelections == 0
            ? request.MaxSelections > 1
                ? $"Choose up to {request.MaxSelections} copies, then Done (or Skip)."
                : request.Destination == CardSelectionDestination.Hand
                    ? "Optional search: select a card to add to hand, or Skip."
                    : "Optional recruit: select a monster or Skip."
            : "Select one card (mandatory).", 250, 151, 13);

        for (int slot = 0; slot < CardSelectionPageSize; slot++)
        {
            int index = cardSelectionPage * CardSelectionPageSize + slot;
            if (index >= request.EligibleCards.Count) break;
            var option = request.EligibleCards[index];
            int x = 247 + slot % 5 * 106, y = 182 + slot / 5 * 155;
            var bounds = new Rectangle(x, y, 87, 112);
            Raylib.DrawRectangleRec(bounds, Panel);
            DrawArtwork(option.Card, x + 3, y + 3, 81, 104);
            bool hovered = Hit(bounds);
            bool chosen = duel.SelectedBatchIndices.Contains(option.SourceIndex);
            Raylib.DrawRectangleLinesEx(bounds, chosen || hovered ? 3 : 1,
                chosen ? Color.Lime : hovered ? Color.White : Gold);
            string name = option.Card.Name;
            while (name.Length > 4 && Raylib.MeasureText(name, 12) > 98) name = name[..^1];
            if (name.Length < option.Card.Name.Length) name = name[..^3] + "...";
            Label(name, x, y + 116, 12);
            if (hovered && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                bool resolved = request.MaxSelections > 1
                    ? duel.SelectBatchCard(option.SourceIndex, out message)
                    : duel.ResolveCardSelection(option.SourceIndex, null,
                        request.MaySelectFaceDownDefense && selectionFaceDownDefense, out message);
                if (resolved && !ReferenceEquals(request, duel.PendingCardSelection))
                {
                    cardSelectionPage = 0;
                    ClearDuelSelection();
                }
                return;
            }
        }

        if (request.MaySelectFaceDownDefense &&
            Button(selectionFaceDownDefense ? "Position: Set DEF" : "Position: ATK",
                250, 493, 190, 30))
            selectionFaceDownDefense = !selectionFaceDownDefense;

        if (request.MaxSelections > 1)
        {
            Label($"Selected: {duel.SelectedBatchIndices.Count}/{request.MaxSelections}",
                449, 500, 13, Gold);
            if (Button("Done", 608, 493, 110, 30, duel.SelectedBatchIndices.Count > 0))
            {
                if (duel.FinishOptionalCardSelection(out message))
                {
                    cardSelectionPage = 0;
                    ClearDuelSelection();
                }
                return;
            }
        }

        if (request.MinSelections == 0 &&
            duel.SelectedBatchIndices.Count == 0 &&
            Button("Skip", 247, 531, 85, 30))
        {
            if (duel.SkipOptionalCardSelection(out message))
            {
                cardSelectionPage = 0;
                ClearDuelSelection();
            }
            return;
        }
        if (Button("< Prev", 339, 531, 90, 30, cardSelectionPage > 0))
            cardSelectionPage--;
        Label($"{cardSelectionPage + 1}/{lastPage + 1}", 474, 537, 17, Gold);
        if (Button("Next >", 614, 531, 105, 30, cardSelectionPage < lastPage))
            cardSelectionPage++;
    }
}
