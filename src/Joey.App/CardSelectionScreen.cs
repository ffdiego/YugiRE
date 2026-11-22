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
            bool chosen = duel.SelectedBatchIndices.Contains(option.SourceIndex);
            if (DrawSelectionCard(option.Card, slot, chosen))
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

    static bool DrawSelectionCard(Card card, int slot, bool chosen, string? source = null)
    {
        int x = 247 + slot % 5 * 106, y = 182 + slot / 5 * 155;
        var bounds = new Rectangle(x, y, 87, 112);
        Raylib.DrawRectangleRec(bounds, Panel);
        DrawArtwork(card, x + 3, y + 3, 81, 104);
        bool hovered = Hit(bounds);
        Raylib.DrawRectangleLinesEx(bounds, chosen || hovered ? 3 : 1,
            chosen ? Color.Lime : hovered ? Color.White : Gold);
        string name = card.Name;
        while (name.Length > 4 && MeasureLabel(name, 12) > 98) name = name[..^1];
        if (name.Length < card.Name.Length) name = name[..^3] + "...";
        Label(name, x, y + 116, 12);
        if (source is not null) Label(source, x, y + 131, 12, Gold);
        return hovered && Raylib.IsMouseButtonPressed(MouseButton.Left);
    }
}
