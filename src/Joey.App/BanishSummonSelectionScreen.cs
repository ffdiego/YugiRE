using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;

static partial class Game
{
    sealed class BanishSummonChoice(CardTarget source, int turn, int required,
        IReadOnlyList<QueuedGraveyardCandidate> candidates)
    {
        public CardTarget Source { get; } = source;
        public int Turn { get; } = turn;
        public int Required { get; } = required;
        public IReadOnlyList<QueuedGraveyardCandidate> Candidates { get; } = candidates;
        public List<CardTarget> Picks { get; } = [];
        public bool Defense { get; set; }
        public int Page { get; set; }
        public bool ListInitialized { get; set; }
        public SelectionListView? List { get; set; }
    }

    static BanishSummonChoice? banishSummonChoice;

    static bool TryGetBanishSummonSource(int index, out CardTarget source)
    {
        source = default;
        return queuedDuel is { IsBusy: false } && duel.ActivePlayer == ViewPlayer &&
            index >= 0 && index < ViewHand.Count &&
            GraveyardBanishSummonRules.QueuedRequiredCount(ViewHand[index]) > 0 &&
            duel.TryCaptureCardTarget(ViewPlayer, DuelZone.Hand, index, out source);
    }

    static bool CanStartBanishSummon(int index) =>
        TryGetBanishSummonSource(index, out var source) &&
        duel.QueuedGraveyardBanishSummonCandidates(source).Count != 0;

    static bool TryStartBanishSummon(int index)
    {
        if (!TryGetBanishSummonSource(index, out var source)) return false;
        var candidates = duel.QueuedGraveyardBanishSummonCandidates(source);
        if (candidates.Count == 0) return false;
        int required = GraveyardBanishSummonRules.QueuedRequiredCount(ViewHand[index]);
        ClearDuelSelection();
        banishSummonChoice = new(source, duel.Turn, required, candidates);
        return true;
    }

    static bool ChooseBanishSummonCard(BanishSummonChoice choice, QueuedGraveyardCandidate candidate)
    {
        if (!ReferenceEquals(choice, banishSummonChoice)) return false;
        if (choice.Picks.Remove(candidate.Target)) return false;
        if (!duel.TryCaptureCardTarget(choice.Source.Side, DuelZone.Graveyard,
                candidate.SourceIndex, out var current) || current != candidate.Target)
        {
            message = "Graveyard selection changed. Choose the summon again.";
            ClearDuelSelection();
            return false;
        }

        choice.Picks.Add(current);
        if (choice.Picks.Count < choice.Required) return false;
        int slot = duel.FirstFreeMonsterZone();
        bool queued = duel.TryQueueGraveyardBanishSummon(choice.Source, slot, choice.Picks,
            choice.Defense, out message);
        ClearDuelSelection();
        return queued;
    }

    static void DrawBanishSummonSelection(BanishSummonChoice choice)
    {
        if (!choice.ListInitialized)
        {
            // 0059F5CD: 0040C940 receives cost + 6; the constructor uses selectlist for modes 7–9.
            choice.List = CreateSelectionList(choice.Required + 6, choice.Source.Side,
                choice.Candidates.Select(candidate => candidate.Card).ToArray());
            choice.ListInitialized = true;
        }

        var list = choice.List;
        if (list is not null)
            DrawNativeSelectionBackground(list, i => choice.Candidates[i].Card);
        else
        {
            Raylib.DrawRectangle(230, 80, 555, 502, new Color(9, 17, 26, 246));
            Raylib.DrawRectangleLines(230, 80, 555, 502, Gold);
        }
        Label($"Choose {choice.Required} Graveyard card(s) to banish ({choice.Picks.Count}/{choice.Required})",
            250, 95, 17, Gold);

        if (Button(choice.Defense ? "Position: DEF" : "Position: ATK", 250, 493, 190, 30))
            choice.Defense = !choice.Defense;

        int visibleSlots = list?.Window.VisibleCount ?? CardSelectionPageSize;
        for (int slot = 0; slot < visibleSlots; slot++)
        {
            int index = list?.Window.CardIndexAtSlot(slot) ?? choice.Page * CardSelectionPageSize + slot;
            if (index < 0 || index >= choice.Candidates.Count) break;
            var candidate = choice.Candidates[index];
            bool chosen = choice.Picks.Contains(candidate.Target);
            if (list is not null ? DrawNativeSelectionCard(list, candidate.Card, slot, chosen)
                : DrawSelectionCard(candidate.Card, slot, chosen))
            {
                ChooseBanishSummonCard(choice, candidate);
                return;
            }
        }

        if (Button("Cancel", 247, 531, 85, 30))
        {
            ClearDuelSelection();
            return;
        }
        if (list is not null)
        {
            DrawNativeSelectionNavigation(list);
            return;
        }
        int lastPage = (choice.Candidates.Count - 1) / CardSelectionPageSize;
        if (Button("< Prev", 339, 531, 90, 30, choice.Page > 0))
            choice.Page--;
        Label($"{choice.Page + 1}/{lastPage + 1}", 474, 537, 17, Gold);
        if (Button("Next >", 614, 531, 105, 30, choice.Page < lastPage))
            choice.Page++;
    }
    // Invoke the same hand and list actions used by live input without creating a raylib window.
    static bool CheckBanishSummonSelection()
    {
        foreach (int side in new[] { 0, 1 })
        foreach (bool defense in new[] { false, true })
        foreach (var (id, count) in new (ushort Id, int Count)[] { (1514, 3), (1515, 2), (1516, 1) })
        {
            const uint FiendProperties = (3u << 20) | (4u << 25) | (4u << 29);
            Card source = new(id, id, "Special Summon", "", FiendProperties | (1u << 18));
            Card material = new(24, 24, "Material", "", FiendProperties);
            Card[] graveyard = [material, material, material, material, material, material];
            duel = DemoDuel.FromPartialObservation(side, [source], new Card?[5], new Card?[5], 8000,
                side0Graveyard: side == 0 ? graveyard : [], side1Graveyard: side == 1 ? graveyard : []);
            alphaCpu = null;
            ClearDuelSelection();
            var queue = duel.CreatePrimitiveActionQueue();
            queuedDuel = queue;

            if (CurrentHandAction(0) != NativeCursorAction.Summon) return false;
            UseHandAction(0);
            if (banishSummonChoice is not { } canceled) return false;
            ClearDuelSelection();
            if (ChooseBanishSummonCard(canceled, canceled.Candidates[0]) || queue.IsBusy) return false;

            UseHandAction(0);
            if (banishSummonChoice is not { } choice || choice.Required != count ||
                choice.Candidates.Count != graveyard.Length) return false;
            choice.Defense = defense;
            var cost = choice.Candidates.Reverse().Take(count).ToArray();
            if (count > 1 &&
                (ChooseBanishSummonCard(choice, cost[0]) || ChooseBanishSummonCard(choice, cost[0]) ||
                 choice.Picks.Count != 0 || queue.IsBusy)) return false;

            for (int i = 0; i < count; i++)
            {
                if (ChooseBanishSummonCard(choice, cost[i]) != (i == count - 1) ||
                    duel.Hand.Count != 1 || duel.Graveyard(side).Count != graveyard.Length) return false;
            }
            if (!queue.IsBusy || banishSummonChoice is not null ||
                queue.Trace.Single().Entry.Origin != ActionOrigin.Player) return false;

            for (int tick = 0; queue.IsBusy && tick < 100; tick++) queue.Tick();
            if (queue.IsBusy || duel.Hand.Count != 0 || duel.MonsterFaceDown[side, 0] ||
                duel.DefensePosition[side, 0] != defense ||
                !duel.TryCaptureCardTarget(side, DuelZone.Monster, 0, out var summoned) ||
                summoned.Occurrence != choice.Source.Occurrence || duel.Banished(side).Count != count)
                return false;
            for (int i = 0; i < count; i++)
            {
                if (!duel.TryCaptureCardTarget(side, DuelZone.Banished, i, out var banished) ||
                    banished.Occurrence != cost[i].Target.Occurrence) return false;
            }
        }
        return true;
    }

}
