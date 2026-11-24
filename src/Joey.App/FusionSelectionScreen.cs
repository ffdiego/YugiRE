using Joey.Core;
using Raylib_cs;

static partial class Game
{
    private static PendingFusionSummon? activeFusionRequest;
    private static FusionSummonOption? selectedFusionResult;
    private static readonly List<FusionMaterial> selectedFusionMaterials = [];
    private static int fusionPage;
    private static bool fusionDefensePosition;
    private static SelectionListView? fusionSelectionList;
    private static bool fusionListInitialized;

    // Native stable list for results and materials; retain existing fusion decisions.
    static void DrawFusionSelection(PendingFusionSummon request)
    {
        if (!ReferenceEquals(request, activeFusionRequest))
        {
            activeFusionRequest = request;
            ResetFusionChoice();
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Backspace))
        {
            duel.CancelFusionSummon(out message);
            ClearDuelSelection();
            ResetFusionChoice();
            return;
        }

        var result = selectedFusionResult;
        int options = result?.Materials.Count ?? request.Options.Count;
        if (!fusionListInitialized)
        {
            fusionSelectionList = CreateSelectionList(1, request.Owner, result is null
                ? request.Options.Select(option => option.Result).ToArray()
                : result.Materials.Select(material => material.Card).ToArray());
            fusionListInitialized = true;
        }
        if (fusionSelectionList is { } native) DrawNativeSelectionBackground(native);
        else
        {
            Raylib.DrawRectangle(230, 80, 555, 502, new Color(9, 17, 26, 246));
            Raylib.DrawRectangleLines(230, 80, 555, 502, Gold);
        }
        Label(request.Spell.Name, 250, 95, 21, Gold);
        int lastPage = (options - 1) / CardSelectionPageSize;
        Label(result is null ? "Choose a Fusion Monster." : $"Choose materials for {result.Result.Name}.",
            250, 126, 16);
        if (result is not null)
        {
            string help = duel.FirstFreeMonsterZone() < 0
                ? "Use a monster from the field to free a zone."
                : "Choose materials from your hand or field.";
            // Preserve source-occurrence identity in the provisional help
            // line, since identical miniature art can appear in both zones.
            if (fusionSelectionList?.Window.SelectedCardIndex is int focus && focus >= 0 && focus < options)
            {
                var material = result.Materials[focus];
                help = $"{material.Card.Name} | {material.Zone} {material.Index + 1}";
            }
            Label(help, 250, 151, 13);
        }

        int visibleSlots = fusionSelectionList?.Window.VisibleCount ?? CardSelectionPageSize;
        for (int slot = 0; slot < visibleSlots; slot++)
        {
            int index = fusionSelectionList?.Window.CardIndexAtSlot(slot) ?? fusionPage * CardSelectionPageSize + slot;
            if (index < 0 || index >= options) break;
            if (result is null)
            {
                bool picked = fusionSelectionList is { } list
                    ? DrawNativeSelectionCard(list, request.Options[index].Result, slot, false)
                    : DrawSelectionCard(request.Options[index].Result, slot, false);
                if (!picked) continue;
                selectedFusionResult = request.Options[index];
                fusionPage = 0;
                fusionSelectionList = null;
                fusionListInitialized = false;
                return;
            }
            var material = result.Materials[index];
            bool chosen = selectedFusionMaterials.Contains(material);
            bool selectable = chosen || duel.CanCompleteFusionSelection(result, [.. selectedFusionMaterials, material]);
            bool pickedMaterial = fusionSelectionList is { } materialList
                ? DrawNativeSelectionCard(materialList, material.Card, slot, chosen, selectable)
                : DrawSelectionCard(material.Card, slot, chosen, $"{material.Zone} {material.Index + 1}", selectable);
            if (!pickedMaterial)
                continue;
            if (chosen) selectedFusionMaterials.Remove(material);
            else selectedFusionMaterials.Add(material);
            return;
        }

        if (result is not null)
        {
            if (Button("Choose result", 247, 493, 135, 30))
            {
                ResetFusionChoice();
                return;
            }
            Label($"{selectedFusionMaterials.Count}/{result.Recipe.Count}", 394, 502, 15);
            if (Button(fusionDefensePosition ? "Position: DEF" : "Position: ATK", 444, 493, 151, 30))
                fusionDefensePosition = !fusionDefensePosition;
            bool complete = selectedFusionMaterials.Count == result.Recipe.Count &&
                (duel.FirstFreeMonsterZone() >= 0 ||
                 selectedFusionMaterials.Any(material => material.Zone == FusionMaterialZone.Field));
            if (Button("Summon", 614, 493, 105, 30, complete))
            {
                CommitHumanFusion(request, result);
                return;
            }
        }

        if (Button("Cancel", 247, 531, 85, 30))
        {
            duel.CancelFusionSummon(out message);
            ClearDuelSelection();
            ResetFusionChoice();
            return;
        }
        if (fusionSelectionList is { } listView)
        {
            DrawNativeSelectionNavigation(listView);
            return;
        }
        if (Button("< Prev", 339, 531, 90, 30, fusionPage > 0)) fusionPage--;
        Label($"{fusionPage + 1}/{lastPage + 1}", 474, 537, 17, Gold);
        if (Button("Next >", 614, 531, 105, 30, fusionPage < lastPage)) fusionPage++;
    }

    static void ResetFusionChoice()
    {
        selectedFusionResult = null;
        selectedFusionMaterials.Clear();
        fusionPage = 0;
        fusionSelectionList = null;
        fusionListInitialized = false;
        fusionDefensePosition = false;
    }

    static void CommitHumanFusion(PendingFusionSummon request, FusionSummonOption result)
    {
        var destination = request.FieldOwner is int fieldOwner ? FieldZoneBounds(fieldOwner) :
            ZoneBounds(request.Owner, request.FromSet ? request.SourceIndex : duel.FirstFreeSpellZone(), true);
        bool fromHand = request.FieldOwner is null && !request.FromSet;
        Rectangle? origin = fromHand ? HandBounds(request.SourceIndex) : null;
        if (!duel.ResolveFusionSummon(result.DeckIndex, selectedFusionMaterials, out message, fusionDefensePosition))
            return;
        var spent = selectedFusionMaterials.Where(material => material.Zone == FusionMaterialZone.Hand)
            .Select(material => material.Index);
        if (fromHand) spent = spent.Append(request.SourceIndex);
        foreach (int index in spent.OrderDescending()) handLayouts[request.Owner].RemoveAt(index);
        ShowPlayedCardDetail(request.Spell);
        BeginCardActivation(request.Spell, destination, origin,
            nearSide: (request.FieldOwner ?? request.Owner) == ViewPlayer);
        ClearDuelSelection();
        ResetFusionChoice();
    }
}
