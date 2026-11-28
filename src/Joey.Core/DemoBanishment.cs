namespace Joey.Core;

/// <summary>Observation of a successful BanishCommand. The graveyard
/// is public; other sources are conservatively masked until the rules
/// carry an explicit banished face-up/face-down fact.</summary>
public sealed record DemoBanishment(int Side,DuelZone Source,Card? VisibleCard);
