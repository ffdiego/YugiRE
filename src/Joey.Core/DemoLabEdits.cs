namespace Joey.Core;

public sealed partial class DemoDuel
{
    // Setup-only operations: no summon allowances, FLIP, graveyard triggers or
    // Exodia check. Gameplay movements must use the existing semantic commands.
    // The queue authorizes these internal types before execution.
    private bool CanLabPlace(int side, Card card, DuelZone zone, int slot) => zone switch
    {
        DuelZone.Hand => hands[side].Count < 80,
        DuelZone.Deck => decks[side].Count < 80,
        DuelZone.Graveyard => graves[side].Count < 160,
        DuelZone.Banished => banished[side].Count < 160,
        DuelZone.Monster => card.IsMonster && Monsters[side, slot] is null,
        DuelZone.SpellTrap => card.ClassCode is 21 or 22 or 23 && !FieldSpellRules.Supports(card.Id) &&
            !DestinyBoardRules.IsMessage(card.Id) && Spells[side, slot] is null,
        DuelZone.FieldSpell => FieldSpellRules.Supports(card.Id) && FieldSpells[side] is null,
        _ => false
    };

    private void LabPlace(int side, Card card, DuelZone zone, int slot, bool faceDown, bool defense)
    {
        switch (zone)
        {
            case DuelZone.Hand: hands[side].Add(card); break;
            case DuelZone.Deck: decks[side].Enqueue(card); break;
            case DuelZone.Graveyard: graves[side].Add(card); break;
            case DuelZone.Banished: banished[side].Add(card); break;
            case DuelZone.Monster:
                Monsters[side, slot] = card;
                MonsterFaceDown[side, slot] = faceDown;
                DefensePosition[side, slot] = defense;
                MonsterWasFlipped[side, slot] = false;
                changedPosition[side, slot] = false;
                attackDeclarations[side, slot] = 0;
                break;
            case DuelZone.SpellTrap:
                Spells[side, slot] = card;
                SpellFaceDown[side, slot] = true;
                spellSetTurn[side, slot] = Turn;
                break;
            case DuelZone.FieldSpell:
                FieldSpells[side] = card;
                FieldSpellFaceDown[side] = true;
                break;
            default: throw new InvalidOperationException("Invalid lab destination.");
        }
    }

    private CommandResult ExecuteLabSpawn(LabSpawnCommand command)
    {
        if (!CanLabPlace(command.Side, command.Template, command.Destination, command.Slot))
            return new(CommandOutcome.Rejected, "Occupied, incompatible or full destination.");
        var card = NewOccurrence(command.Template);
        LabPlace(command.Side, card, command.Destination, command.Slot, command.FaceDown, command.Defense);
        return new(CommandOutcome.Applied, $"Spawned resource {card.Id} as occurrence {occurrenceIds[card].Value}.");
    }

    private CommandResult ExecuteLabMove(LabMoveCommand command)
    {
        if (!TryResolveCardTarget(command.Source, out int index, out var card))
            return new(CommandOutcome.Skipped, "Source occurrence gone.");
        int side = command.Side;
        if (!CanLabPlace(side, card, command.Destination, command.Slot))
            return new(CommandOutcome.Rejected, "Occupied, incompatible or full destination.");
        // Do not silently tear down continuous/equipment relationships or
        // invalidate a delayed revival's source through a setup edit.
        if (PendingDelayedRevivals.Any(r => ReferenceEquals(r.Source, card)) ||
            (command.Source.Zone == DuelZone.Monster && equipment.Any(e => e.TargetPlayer == side && e.TargetZone == index)) ||
            (command.Source.Zone == DuelZone.SpellTrap && (!SpellFaceDown[side,index] ||
                equipment.Any(e => e.Owner == side && e.SourceZone == index))) ||
            (command.Source.Zone == DuelZone.FieldSpell && !FieldSpellFaceDown[side]))
            return new(CommandOutcome.Rejected, "Linked or active source; use its semantic gameplay operation.");
        switch (command.Source.Zone)
        {
            case DuelZone.Hand: hands[side].RemoveAt(index); break;
            case DuelZone.Graveyard: graves[side].RemoveAt(index); break;
            case DuelZone.Banished: banished[side].RemoveAt(index); break;
            case DuelZone.Deck:
                var remaining = decks[side].Where((_, i) => i != index).ToArray();
                decks[side].Clear();
                foreach (var item in remaining) decks[side].Enqueue(item);
                break;
            case DuelZone.Monster: ClearMonsterSlot(side, index); break;
            case DuelZone.SpellTrap: ClearSpellTrapSlot(side, index); break;
            case DuelZone.FieldSpell: FieldSpells[side] = null; FieldSpellFaceDown[side] = false; break;
            default: throw new InvalidOperationException("Invalid lab source.");
        }
        LabPlace(side, card, command.Destination, command.Slot, command.FaceDown, command.Defense);
        return new(CommandOutcome.Applied, $"Moved occurrence {command.Source.Occurrence.Value}.");
    }
}
