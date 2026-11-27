namespace Joey.Cards;

public enum HandManipulationKind { DrawThenDiscard, RandomThenChosenDiscard, OpponentChosenDiscard, OpponentChosenReturn,
    OwnMonsterDiscard, OpponentGraveBanish, EitherGraveBanish }

public readonly record struct HandManipulationEffect(HandManipulationKind Kind, int LifeCost = 0,
    int DrawCount = 0, int ChosenCount = 1, bool OptionalAfterFirst = false);

/// <summary>Card definitions only; Core owns payment, occurrence movement, and choices.</summary>
public static class HandManipulationEffects
{
    public const ushort GracefulCharity = 968, Confiscation = 1070, DelinquentDuo = 1071,
        TheForcefulSentry = 1077;

    public const ushort TheCheerfulCoffin = 1025, GravediggerGhoul = 1011, SoulRelease = 1024;

    public static HandManipulationEffect? For(ushort id) => id switch
    {
        GracefulCharity => new(HandManipulationKind.DrawThenDiscard, DrawCount: 3, ChosenCount: 2),
        DelinquentDuo => new(HandManipulationKind.RandomThenChosenDiscard, LifeCost: 1000),
        Confiscation => new(HandManipulationKind.OpponentChosenDiscard, LifeCost: 1000),
        TheForcefulSentry => new(HandManipulationKind.OpponentChosenReturn),
        TheCheerfulCoffin => new(HandManipulationKind.OwnMonsterDiscard, ChosenCount: 3, OptionalAfterFirst: true),
        GravediggerGhoul => new(HandManipulationKind.OpponentGraveBanish, ChosenCount: 2, OptionalAfterFirst: true),
        SoulRelease => new(HandManipulationKind.EitherGraveBanish, ChosenCount: 5, OptionalAfterFirst: true),
        _ => null
    };
}
