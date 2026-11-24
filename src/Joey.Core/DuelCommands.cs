namespace Joey.Core;

// These are logical operations, not native opcode numbers. Admission (player,
// CPU, effect, lab) is separate from execution; a queue accepts admitted commands.
public enum ActionOrigin { Player, Cpu, Effect, Console, Test }
public enum DuelZone { Deck, Hand, Monster, SpellTrap, FieldSpell, Graveyard, Banished }

// The state adapter owns allocation and resolution of occurrence IDs. Never use
// resource ID, catalogue object reference, or current slot as an occurrence ID.
public readonly record struct CardOccurrenceId(long Value);
public readonly record struct CardTarget(CardOccurrenceId Occurrence, int Side, DuelZone Zone);

public abstract record DuelCommand(int Side);
public sealed record DrawCommand(int Side) : DuelCommand(Side); // exactly one card
public sealed record SummonCommand(int Side, CardTarget Source, int Slot) : DuelCommand(Side);
public sealed record SetCommand(int Side, CardTarget Source, DuelZone Destination, int Slot) : DuelCommand(Side);
public sealed record SendToGraveyardCommand(int Side, CardTarget Target) : DuelCommand(Side);
public sealed record DiscardCommand(int Side, CardTarget Target) : DuelCommand(Side);
public sealed record DestroyCommand(int Side, CardTarget Target) : DuelCommand(Side);
public sealed record DamageCommand(int Side, int Amount) : DuelCommand(Side);
public sealed record HealCommand(int Side, int Amount) : DuelCommand(Side);
public sealed record FlipCommand(int Side, CardTarget Target) : DuelCommand(Side);
public sealed record ChangePositionCommand(int Side, CardTarget Target, bool Defense) : DuelCommand(Side);

// Pilot orchestration only; A08 moves card-specific producers out of Core.
// Neither of these stages claims a one-to-one native opcode mapping.
internal sealed record ActivateRaigekiCommand(int Side, CardTarget Source) : DuelCommand(Side);
internal sealed record FinishFieldRemovalCommand(int Side) : DuelCommand(Side);

internal static class DuelCommandValidation
{
    public static void Validate(DuelCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Side is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(command), "Invalid side.");
        switch (command)
        {
            case DrawCommand:
            case FinishFieldRemovalCommand:
                break;
            case ActivateRaigekiCommand activate:
                Target(activate.Source, activate.Side, DuelZone.Hand);
                break;
            case DamageCommand damage when damage.Amount > 0:
            case HealCommand { Amount: > 0 }:
                break;
            case SummonCommand summon when summon.Slot is >= 0 and < 5:
                Target(summon.Source, summon.Side, DuelZone.Hand);
                break;
            case SetCommand set when
                (set.Destination is DuelZone.Monster or DuelZone.SpellTrap && set.Slot is >= 0 and < 5)
                || (set.Destination == DuelZone.FieldSpell && set.Slot == 0):
                Target(set.Source, set.Side, DuelZone.Hand);
                break;
            case SendToGraveyardCommand send:
                Target(send.Target, send.Side, DuelZone.Deck, DuelZone.Hand, DuelZone.Monster,
                    DuelZone.SpellTrap, DuelZone.FieldSpell, DuelZone.Banished);
                break;
            case DiscardCommand discard:
                Target(discard.Target, discard.Side, DuelZone.Hand);
                break;
            case DestroyCommand destroy:
                Target(destroy.Target, destroy.Side, DuelZone.Monster, DuelZone.SpellTrap, DuelZone.FieldSpell);
                break;
            case FlipCommand flip:
                Target(flip.Target, flip.Side, DuelZone.Monster);
                break;
            case ChangePositionCommand position:
                Target(position.Target, position.Side, DuelZone.Monster);
                break;
            default:
                throw new ArgumentException("Unknown command or invalid arguments.", nameof(command));
        }
    }

    private static void Target(CardTarget target, int side, params DuelZone[] allowed)
    {
        if (target.Occurrence.Value <= 0 || target.Side != side || !allowed.Contains(target.Zone))
            throw new ArgumentException("Invalid occurrence, side or expected zone.", nameof(target));
    }
}
