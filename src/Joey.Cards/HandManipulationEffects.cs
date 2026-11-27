namespace Joey.Cards;

public enum HandManipulationKind { DrawThenDiscard, RandomThenChosenDiscard, OpponentChosenDiscard, OpponentChosenReturn }

public readonly record struct HandManipulationEffect(HandManipulationKind Kind, int LifeCost = 0,
    int DrawCount = 0, int ChosenCount = 1);

/// <summary>Card definitions only; Core owns payment, occurrence movement, and choices.</summary>
public static class HandManipulationEffects
{
    public const ushort GracefulCharity = 968, Confiscation = 1070, DelinquentDuo = 1071,
        TheForcefulSentry = 1077;

    public static HandManipulationEffect? For(ushort id) => id switch
    {
        GracefulCharity => new(HandManipulationKind.DrawThenDiscard, DrawCount: 3, ChosenCount: 2),
        DelinquentDuo => new(HandManipulationKind.RandomThenChosenDiscard, LifeCost: 1000),
        Confiscation => new(HandManipulationKind.OpponentChosenDiscard, LifeCost: 1000),
        TheForcefulSentry => new(HandManipulationKind.OpponentChosenReturn),
        _ => null
    };
}
