namespace Joey.Core;

public sealed partial class DemoDuel
{
    private const ushort InvitationToADarkSleep = 1332;
    private readonly Dictionary<Card, Card> restrictedAttackTargets = new(ReferenceEqualityComparer.Instance);

    private bool AttackRestrictedByMonster(Card attacker)
    {
        if (restrictedAttackTargets.Count == 0) return false;
        for (int side = 0; side < 2; side++)
        for (int slot = 0; slot < 5; slot++)
        {
            if (Monsters[side, slot] is { } source &&
                (source.Id != InvitationToADarkSleep || !MonsterFaceDown[side, slot]) &&
                restrictedAttackTargets.TryGetValue(source, out var target) && ReferenceEquals(target, attacker))
                return true;
        }
        return false;
    }

    private void ClearMonsterAttackRestrictions(Card departing)
    {
        restrictedAttackTargets.Remove(departing);
        foreach (var (source, _) in restrictedAttackTargets.Where(link => ReferenceEquals(link.Value, departing)).ToArray())
            restrictedAttackTargets.Remove(source);
    }

    private CommandResult ApplyMonsterAttackRestriction(BindMonsterAttackCommand command)
    {
        if (!TryResolveCardTarget(command.Source, out int slot, out var source) ||
            source.Id is not (FlipEffectRules.DarkEyesIllusionist or InvitationToADarkSleep) ||
            MonsterFaceDown[command.Side, slot] ||
            (source.Id == InvitationToADarkSleep && command.Target.Side == command.Side) ||
            !TryResolveCardTarget(command.Target, out _, out var target))
            return new(CommandOutcome.Skipped, "The attack restriction source or target is no longer eligible.");

        restrictedAttackTargets[source] = target;
        return new(CommandOutcome.Applied);
    }
}
