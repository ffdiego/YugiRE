namespace Joey.Core;

/// <summary>Collects the command plan for one effect in emission order.
/// The caller submits the plan to the action queue or returns it from a continuation.</summary>
internal sealed class EffectContext
{
    private readonly List<DuelCommand> commands = [];

    public IReadOnlyList<DuelCommand> Commands => commands;

    public void Draw(int side, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        for (int i = 0; i < count; i++)
            commands.Add(new DrawCommand(side));
    }

    public void ChangeLifePoints(int side, int delta)
    {
        if (delta > 0)
            commands.Add(new HealCommand(side, delta));
        else if (delta < 0)
            commands.Add(new DamageCommand(side, checked(-delta)));
    }
}
