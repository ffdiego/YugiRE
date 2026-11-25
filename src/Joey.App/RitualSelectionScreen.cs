using Joey.Core;
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
            //Removing the initial field choice on a full board may leave only hand picks.
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

    static bool IsRitualPick(DuelZone zone, int index) =>
        ritualBoardChoice is { } choice &&
        choice.Picks.Exists(item => item.Target.Zone == zone && item.Index == index);
}
