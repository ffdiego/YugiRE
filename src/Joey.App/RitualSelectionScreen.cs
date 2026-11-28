using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;

static partial class Game
{
    sealed class RitualBoardChoice(CardTarget spell, int turn, QueuedRitualSelection selection)
    {
        public CardTarget Spell { get; } = spell;
        public int Turn { get; } = turn;
        public QueuedRitualSelection Selection { get; set; } = selection;
        public List<QueuedRitualMaterial> Picks { get; } = [];
        public bool Defense { get; set; }

        public QueuedRitualMaterial? At(DuelZone zone, int index) =>
            Picks.Concat(Selection.Choices).FirstOrDefault(item =>
                item.Target.Zone == zone && item.Index == index);
    }

    static RitualBoardChoice? ritualBoardChoice;

    static bool TryCaptureRitualSpell(DuelZone zone, int index, out CardTarget spell)
    {
        spell = default;
        return queuedDuel is { IsBusy: false } && duel.ActivePlayer == ViewPlayer &&
            duel.TryCaptureCardTarget(ViewPlayer, zone, index, out spell);
    }

    static bool CanStartRitualBoardChoice(DuelZone zone, int index) =>
        TryCaptureRitualSpell(zone, index, out var spell) && duel.CanStartRitualSelection(spell);

    static bool TryStartRitualBoardChoice(DuelZone zone, int index)
    {
        if (!TryCaptureRitualSpell(zone, index, out var spell) ||
            duel.GetRitualSelection(spell, null, []) is not { } selection) return false;
        ClearDuelSelection();
        ritualBoardChoice = new(spell, duel.Turn, selection);
        return true;
    }

    static bool ChooseRitualTribute(RitualBoardChoice choice, QueuedRitualMaterial material)
    {
        if (!ReferenceEquals(ritualBoardChoice, choice)) return false;
        if (!duel.TryCaptureCardTarget(material.Target.Side, material.Target.Zone, material.Index,
                out var target) || target != material.Target)
        {
            message = "Ritual materials changed. Choose the spell again.";
            ClearDuelSelection();
            return false;
        }
        if (!choice.Picks.Remove(material))
        {
            if (!choice.Selection.Choices.Contains(material)) return false;
            choice.Picks.Add(material);
        }

        var picks = choice.Picks.Select(item => item.Target).ToArray();
        var next = duel.GetRitualSelection(choice.Spell, choice.Selection.Monster, picks);
        if (next is null)
        {
            // Removing the initial field choice on a full board may leave only hand picks.
            // Restart selection with the same preserved result if the spell remains available.
            next = duel.GetRitualSelection(choice.Spell, choice.Selection.Monster, []);
            if (next is not null) choice.Picks.Clear();
        }
        if (next is null)
        {
            message = "Ritual selection unavailable.";
            ClearDuelSelection();
            return false;
        }
        if (next.Destination is not int slot)
        {
            choice.Selection = next;
            return false;
        }
        bool admitted = duel.TryQueueRitualSummon(choice.Spell, next.Monster, slot, picks,
            choice.Defense, out message);
        ClearDuelSelection();
        return admitted;
    }

    static void ReadRitualBoardChoice(RitualBoardChoice choice)
    {
        if (PointerPressed(MouseButton.Right))
            choice.Defense = !choice.Defense;
        if (!PointerPressed(MouseButton.Left)) return;

        if (hoverHand >= 0)
        {
            if (choice.At(DuelZone.Hand, hoverHand) is { } handMaterial)
                ChooseRitualTribute(choice, handMaterial);
            return;
        }
        for (int index = 0; index < 5; index++)
        {
            if (!Hit(ZoneBounds(ViewPlayer, index, false))) continue;
            if (choice.At(DuelZone.Monster, index) is { } fieldMaterial)
                ChooseRitualTribute(choice, fieldMaterial);
            return;
        }
    }

    static bool CheckRitualBoardSelection()
    {
        Card spell = new(670, 670, "Ritual spell", "", 22u << 20);
        Card ritual = new(359, 359, "Ritual monster", "", (3u << 20) | (8u << 25) | (3u << 18));
        Card handTribute = new(29, 29, "Hand tribute", "", (3u << 20) | (5u << 25));
        Card fieldTribute = new(30, 30, "Field tribute", "", (3u << 20) | (3u << 25));
        Card filler = new(31, 31, "Field filler", "", (3u << 20) | (1u << 25));

        foreach (int side in new[] { 0, 1 })
        foreach (bool fromSet in new[] { false, true })
        foreach (bool fullField in new[] { false, true })
        foreach (bool defense in new[] { false, true })
        {
            Card?[] ownField = fullField
                ? [fieldTribute, filler, filler, filler, filler]
                : [fieldTribute, null, null, null, null];
            duel = DemoDuel.FromPartialObservation(side, [spell, ritual, handTribute],
                side == 0 ? ownField : new Card?[5], side == 1 ? ownField : new Card?[5],
                8000, equipmentContextResolved: true);
            alphaCpu = null;
            ClearDuelSelection();
            var queue = duel.Actions;
            queuedDuel = queue;

            if (fromSet)
            {
                if (!duel.TryQueueSpellTrapSet(0, out _)) return false;
                for (int tick = 0; queue.IsBusy && tick < 100; tick++) queue.Tick();
                if (queue.IsBusy || duel.Spells[side, 0] != spell) return false;
            }
            var zone = fromSet ? DuelZone.SpellTrap : DuelZone.Hand;
            if (!CanStartRitualBoardChoice(zone, 0)) return false;
            if (fromSet)
                TryStartRitualBoardChoice(zone, 0);
            else
            {
                if (CurrentHandAction(0) != NativeCursorAction.Activate) return false;
                UseHandAction(0);
            }
            if (ritualBoardChoice is not { } canceled ||
                canceled.At(DuelZone.Monster, 0) is not { } canceledTribute) return false;
            ClearDuelSelection();
            if (ChooseRitualTribute(canceled, canceledTribute) || queue.IsBusy) return false;

            if (!TryStartRitualBoardChoice(zone, 0) || ritualBoardChoice is not { } choice ||
                choice.Selection.RemainingLevel != 8 ||
                choice.At(DuelZone.Hand, fromSet ? 0 : 1) is not null ||
                choice.At(DuelZone.Monster, 0) is not { } field) return false;
            int handIndex = fromSet ? 1 : 2;
            if (fullField && choice.At(DuelZone.Hand, handIndex) is not null) return false;
            choice.Defense = defense;
            int initialHandCount = duel.Hand.Count;
            if (ChooseRitualTribute(choice, field) || queue.IsBusy ||
                choice.Selection.RemainingLevel != 5 || duel.Hand.Count != initialHandCount ||
                duel.Monsters[side, 0] != fieldTribute ||
                choice.At(DuelZone.Hand, handIndex) is not { } handMaterial) return false;
            if (!ChooseRitualTribute(choice, handMaterial) || !queue.IsBusy ||
                ritualBoardChoice is not null || duel.Hand.Count != initialHandCount ||
                duel.Monsters[side, 0] != fieldTribute ||
                queue.Trace.LastOrDefault()?.Entry.Origin != ActionOrigin.Player) return false;

            for (int tick = 0; queue.IsBusy && tick < 100; tick++) queue.Tick();
            int slot = fullField ? 0 : 1;
            if (queue.IsBusy || duel.Hand.Count != 0 || duel.MonsterFaceDown[side, slot] ||
                duel.DefensePosition[side, slot] != defense ||
                !duel.TryCaptureCardTarget(side, DuelZone.Monster, slot, out var summoned) ||
                summoned.Occurrence != choice.Selection.Monster.Occurrence ||
                !duel.Graveyard(side).SequenceEqual([spell, fieldTribute, handTribute])) return false;
        }
        return true;
    }

    static bool IsRitualPick(DuelZone zone, int index) =>
        ritualBoardChoice is { } choice &&
        choice.Picks.Exists(item => item.Target.Zone == zone && item.Index == index);
}
