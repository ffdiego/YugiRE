using Joey.Core;
using Raylib_cs;

static partial class Game
{
    private static PendingFusionSummon? activeFusionRequest;
    private static FusionSummonOption? selectedFusionResult;
    private static readonly List<FusionMaterial> selectedFusionMaterials = [];
    private static int fusionPage;
    private static bool fusionDefensePosition;

    // Temporary controls use the existing card-selection layout and archive artwork.
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

        Raylib.DrawRectangle(230, 80, 555, 502, new Color(9, 17, 26, 246));
        Raylib.DrawRectangleLines(230, 80, 555, 502, Gold);
        Label(request.Spell.Name, 250, 95, 21, Gold);
        var result = selectedFusionResult;
        int options = result?.Materials.Count ?? request.Options.Count;
        int lastPage = (options - 1) / CardSelectionPageSize;
        Label(result is null ? "Choose a Fusion Monster." : $"Choose materials for {result.Result.Name}.",
            250, 126, 16);
        if (result is not null)
            Label(duel.FirstFreeMonsterZone() < 0
                ? "Use a monster from the field to free a zone."
                : "Choose materials from your hand or field.", 250, 151, 13);

        for (int slot = 0; slot < CardSelectionPageSize; slot++)
        {
            int index = fusionPage * CardSelectionPageSize + slot;
            if (index >= options) break;
            if (result is null)
            {
                if (!DrawSelectionCard(request.Options[index].Result, slot, false)) continue;
                selectedFusionResult = request.Options[index];
                fusionPage = 0;
                return;
            }
            var material = result.Materials[index];
            bool chosen = selectedFusionMaterials.Contains(material);
            bool selectable = chosen || duel.CanCompleteFusionSelection(result, [.. selectedFusionMaterials, material]);
            if (!DrawSelectionCard(material.Card, slot, chosen, $"{material.Zone} {material.Index + 1}", selectable))
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
        if (Button("< Prev", 339, 531, 90, 30, fusionPage > 0)) fusionPage--;
        Label($"{fusionPage + 1}/{lastPage + 1}", 474, 537, 17, Gold);
        if (Button("Next >", 614, 531, 105, 30, fusionPage < lastPage)) fusionPage++;
    }

    static void ResetFusionChoice()
    {
        selectedFusionResult = null;
        selectedFusionMaterials.Clear();
        fusionPage = 0;
        fusionDefensePosition = false;
    }

    static void CommitHumanFusion(PendingFusionSummon request, FusionSummonOption result)
    {
        int spellZone = request.FromSet ? request.SourceIndex : duel.FirstFreeSpellZone();
        Rectangle? origin = request.FromSet ? null : HandBounds(request.SourceIndex);
        if (!duel.ResolveFusionSummon(result.DeckIndex, selectedFusionMaterials, out message, fusionDefensePosition))
            return;
        var spent = selectedFusionMaterials.Where(material => material.Zone == FusionMaterialZone.Hand)
            .Select(material => material.Index);
        if (!request.FromSet) spent = spent.Append(request.SourceIndex);
        foreach (int index in spent.OrderDescending()) handLayouts[request.Owner].RemoveAt(index);
        ShowPlayedCardDetail(request.Spell);
        BeginCardActivation(request.Spell, ZoneBounds(request.Owner, spellZone, true), origin);
        ClearDuelSelection();
        ResetFusionChoice();
    }
}
