namespace Joey.Core;

public enum DemoPhase { Main, Battle, End }

/// <summary>Explicit observed setup, not a gameplay action or full save-state.
/// Set cards here are assumed to have been set on an earlier turn.</summary>
public sealed record DuelCheckpoint(int Turn, DemoPhase Phase = DemoPhase.Main,
    bool DrawTaken = false, IReadOnlyList<Card?>? Side0SetSpells = null,
    IReadOnlyList<Card?>? Side1SetSpells = null);

public sealed record DemoMonsterDestruction(int Player, int Zone, Card Card, bool FaceDown, bool Defense)
{
    public RemovalCause Cause { get; init; } = RemovalCause.EffectDestruction;
}
/// <summary>A normal or ritual tribute, distinct from destruction.</summary>
public sealed record DemoMonsterTribute(int Player, int Zone, Card Card, bool FaceDown, bool Defense)
{
    public RemovalCause Cause { get; init; } = RemovalCause.Tribute;
}
/// <summary>Successfully destroyed S/T or FIELD; snapshot from before slot mutation.
/// Zone uses SpellTrapTargetRules.FieldZone for a Field Spell.</summary>
public sealed record DemoSpellTrapDestruction(int Player, int Zone, Card Card, bool FaceDown)
{
    public RemovalCause Cause => RemovalCause.EffectDestruction;
}
public sealed record DemoHandTransfer(int Player, int Index, Card Card, bool FromDeck);
/// <summary>Presentation-only notification, emitted after an effect places a monster.</summary>
public sealed record DemoSpecialSummon(int Player, int Zone, Card Card, bool FaceDownDefense,
    bool FaceUpDefense = false)
{
    public bool InDefense => FaceDownDefense || FaceUpDefense;
    // Snapshot only for presentation of a committed general Fusion.
    public IReadOnlyList<Card>? FusionMaterials { get; init; }
}
/// <summary>Committed normal summon or face-down Set; not a special summon or FLIP.</summary>
public sealed record DemoNormalSummon(int Player, int Zone, Card Card, bool FaceDown);
/// <summary>Observable Exodia win; five card snapshots in head, arms, legs order.</summary>
public sealed record DemoExodiaCompletion(int Player, IReadOnlyList<Card> Pieces);
/// <summary>Observed field placement of a Spirit Message during an opponent End Phase.</summary>
public sealed record DemoDestinyBoardMessagePlaced(int Player, int Zone, Card Card, bool FromDeck);
/// <summary>Five face-up field cards satisfy Destiny Board's F-I-N-A-L condition.</summary>
public sealed record DemoDestinyBoardCompletion(int Player, IReadOnlyList<Card> Pieces);

// Temporary local two-player turn engine. Deck order and progression are demo policy.
// Selected RE-backed battle/spell rules are marked individually; see src/README.md.
public sealed partial class DemoDuel
{
    public event Action<DemoMonsterDestruction>? MonsterDestroyed;
    public event Action<DemoSpellTrapDestruction>? SpellTrapDestroyed;
    /// <summary>Fired once after a player completes all five unique Exodia pieces.</summary>
    public event Action<DemoExodiaCompletion>? ExodiaCompleted;
    public event Action<DemoDestinyBoardMessagePlaced>? DestinyBoardMessagePlaced;
    public event Action<DemoDestinyBoardCompletion>? DestinyBoardCompleted;
    /// <summary>Observational visual event after a valid tribute reaches the Graveyard.</summary>
    public event Action<DemoMonsterTribute>? MonsterTributed;
    public event Action<DemoHandTransfer>? HandTransferred;
    public event Action<DemoSpecialSummon>? MonsterSpecialSummoned;
    /// <summary>Presentation-only notice of a successful normal summon or Set.</summary>
    public event Action<DemoNormalSummon>? MonsterNormalSummoned;
    /// <summary>Committed manual position change, not an attack reveal.</summary>
    public event Action<DemoMonsterPositionChange>? MonsterPositionChanged;
    /// <summary>Presentation-only: an effect has already changed a deck's order.</summary>
    public event Action<int>? DeckShuffled;
    private readonly List<Card>[] hands = [[], []];
    private readonly Queue<Card>[] decks = [new(), new()];
    // Seeded sandbox-only effect shuffling; separate from initial deck randomization.
    private Random effectRandom = new(1979);
    public IReadOnlyList<DemoExcavationResult> LastMorphingJar2 { get; private set; } = [];
    /// <summary>Last P082/P088 deck-effect audit (sandbox, not native transcript).</summary>
    public DemoDeckFlowResult? LastDeckFlow { get; private set; }
    public MonsterWipeResult? LastMonsterWipe { get; private set; }
    // Keep Graveyard choices behind a complete board wipe or Fusion transaction.
    private bool deferringFieldRemovalTriggers;
    private bool resolvingSummon;
    private readonly int[,] spellSetTurn = new int[2, 5];
    public DemoSummonResponse? PendingSummonResponse { get; private set; }
    public DemoSummonTrapResult? LastSummonTrap { get; private set; }
    public DemoAttackResponse? PendingAttackResponse { get; private set; }
    public DemoAttackTrapResult? LastAttackTrap { get; private set; }
    // Negate Attack ends the current Battle Phase; Main after Battle is
    // allowed but a second Battle Phase in the same turn is not.
    private bool battlePhaseEndedThisTurn;
    public IReadOnlyList<Card> DeckSnapshot(int player) => player is 0 or 1
        ? Array.AsReadOnly(decks[player].ToArray()) : throw new ArgumentOutOfRangeException(nameof(player));
    private readonly List<Card>[] graves = [[], []];
    // The banished zone is shared with Fusion Gate in DemoFusion.cs.
    // FLIP banishment reuses those same physical card occurrences.
    public DemoFlipBanishBurnResult? LastFlipBanishBurn { get; private set; }
    // Serial choice resolution shared by post-graveyard effects and P165
    // Normal/Flip Summon triggers. Preserve provenance: battle and Graveyard
    // events must not be interpreted as a Summon, or vice versa.
    private readonly Queue<(int Owner, Card Source, bool DestroyedByBattle,
        bool GraveyardRevival, bool NormalOrFlipSummon)> queuedDeckSearches = new();
    private readonly List<int> selectedBatchIndices = [];
    private readonly Queue<PendingEffectDecision> queuedEffectDecisions = new();
    private readonly List<PendingDelayedRevival> delayedRevivals = [];
    private readonly List<EffectDecisionResolution> effectDecisionHistory = [];
    private long nextEffectDecisionId;
    public PendingEffectDecision? PendingEffectDecision { get; private set; }
    public IReadOnlyList<PendingDelayedRevival> PendingDelayedRevivals => delayedRevivals.AsReadOnly();
    public IReadOnlyList<EffectDecisionResolution> EffectDecisionHistory => effectDecisionHistory.AsReadOnly();
    public record Equipment(Card Card, int Owner, int SourceZone, int TargetPlayer, int TargetZone, Card TargetCard);
    private readonly List<Equipment> equipment = [];
    public IReadOnlyList<Equipment> EquipmentCards => equipment.AsReadOnly();
    private readonly byte[,] attackDeclarations = new byte[2, 5];
    private Card? firstBattleAttacker;
    private readonly bool[,] changedPosition = new bool[2, 5];
    public bool[,] DefensePosition { get; } = new bool[2, 5];
    public bool[,] MonsterFaceDown { get; } = new bool[2, 5];
    /// <summary>Whether the current occupant has previously been flipped face-up.
    /// Cleared whenever an occupant leaves or a new monster is placed.</summary>
    public bool[,] MonsterWasFlipped { get; } = new bool[2, 5];
    public IReadOnlyList<int> SelectedBatchIndices => selectedBatchIndices.AsReadOnly();
    public bool[,] SpellFaceDown { get; } = new bool[2, 5];
    private bool summoned, drawn;
    private readonly List<DemoSelectedMonster> flipSelections = [];
    private readonly List<DemoSpellTrapTarget> flipSpellTrapSelections = [];
    public IReadOnlyList<DemoSpellTrapTarget> SelectedFlipSpellTrapTargets =>
        flipSpellTrapSelections.AsReadOnly();
    public PendingFlipSpellTrapTargets? PendingFlipSpellTrapTargets { get; private set; }
    public IReadOnlyList<DemoSelectedMonster> SelectedFlipTargets => flipSelections.AsReadOnly();
    public DemoFlipTarget? PendingFlipTarget { get; private set; }
    public PendingCardSelection? PendingCardSelection { get; private set; }
    public PendingSpellTrapTarget? PendingSpellTrapTarget { get; private set; }
    /// <summary>Live board predicate; hidden Royal Command does not suppress FLIPs.</summary>
    public bool FlipEffectsNegated => FlipEffectSuppressionRules.RoyalCommandEffective(
        Monsters, MonsterFaceDown, Spells, SpellFaceDown);

    private enum PendingFlipChoice { MonsterTarget, CardSelection, SpellTrapTargets, OptionalDraw }

    /// <summary>
    /// Cancel an already queued FLIP when Royal Command becomes effective
    /// before its resolver. A non-FLIP request (e.g. Sangan/Witch) is unaffected.
    /// The caller passes the pending-request family to avoid consuming a
    /// different kind of player interaction by mistake.
    /// </summary>
    private bool TryNegatePendingFlip(PendingFlipChoice expected, out string message)
    {
        message = "";
        if (!FlipEffectsNegated) return false;
        string? effectName = null;
        switch (expected)
        {
            case PendingFlipChoice.MonsterTarget
                when PendingFlipTarget is { Trigger: DemoMonsterEffectTrigger.Flip } target &&
                     FlipEffectRules.Supports(target.SourceCard.Id):
                effectName = target.SourceCard.Name;
                PendingFlipTarget = null;
                flipSelections.Clear();
                break;
            case PendingFlipChoice.CardSelection
                when PendingCardSelection is { } selection &&
                     FlipEffectRules.Supports(selection.EffectResourceId):
                effectName = $"card {selection.EffectResourceId}";
                PendingCardSelection = null;
                selectedBatchIndices.Clear();
                break;
            case PendingFlipChoice.SpellTrapTargets
                when PendingFlipSpellTrapTargets is { } targets &&
                     FlipEffectRules.Supports(targets.Source.Id):
                effectName = targets.Source.Name;
                PendingFlipSpellTrapTargets = null;
                flipSpellTrapSelections.Clear();
                break;
            case PendingFlipChoice.OptionalDraw
                when PendingEffectDecision is { Kind: EffectDecisionKind.DrawOneOnFlip } choice:
                effectName = choice.Source.Name;
                PendingEffectDecision = null;
                break;
        }
        if (effectName is null) return false;
        BeginNextEffectDecision();
        BeginNextQueuedDeckSearch();
        message = $"{effectName} FLIP effect negated by Royal Command; no effect applied.";
        return true;
    }
    /// <summary>
    /// Determined destroyed at damage calculation, but still present during FLIP resolution.
    /// Such monsters cannot be returned to the hand by Penguin Soldier.
    /// </summary>
    public bool IsMarkedForBattleDestruction(int player, int zone) => IsQueuedBattleMarked(player, zone);
    public bool IsDraw { get; private set; } // Sandbox draw when both decks cannot complete Morphing Jar.
    public bool GameOver => Winner is not null || IsDraw;
    private bool ActionsBlocked => LegacyActionsBlocked || primitiveActionQueue.IsBusy;
    private bool LegacyActionsBlocked => GameOver || PendingFlipTarget is not null ||
        PendingFlipSpellTrapTargets is not null ||
        PendingCardSelection is not null || PendingSummonResponse is not null ||
        PendingAttackResponse is not null || PendingEffectDecision is not null ||
        PendingSpellTrapTarget is not null || PendingFusionSummon is not null || deferringFieldRemovalTriggers;
    private bool equipmentContextResolved = true; // Closed sandbox: static properties, no generic blockers.
    public IReadOnlyList<Card> Hand => hands[ActivePlayer];
    public IReadOnlyList<Card> HandFor(int player) => player is 0 or 1
        ? hands[player].AsReadOnly() : throw new ArgumentOutOfRangeException(nameof(player));
    public IReadOnlyList<Card> Graveyard(int player) => graves[player].AsReadOnly();
    public int DeckCount(int player) => decks[player].Count;
    public Card?[,] Monsters { get; } = new Card?[2, 5];
    public Card?[,] Spells { get; } = new Card?[2, 5];
    // One dedicated field-spell zone per player; at most one is face-up.
    // These arrays are also writable by bounded sandbox fixtures, like Spells.
    public Card?[] FieldSpells { get; } = new Card?[2];
    public bool[] FieldSpellFaceDown { get; } = new bool[2];
    public Card? ActiveFieldSpell => Enumerable.Range(0, 2)
        .Where(p => !FieldSpellFaceDown[p])
        .Select(p => FieldSpells[p]).FirstOrDefault(card => card is not null);
    public int[] Life { get; } = [8000, 8000];
    public int Turn { get; private set; } = 1;
    public int ActivePlayer => (Turn - 1) % 2;
    public DemoPhase Phase { get; private set; }
    public bool IsMainPhase2 => Phase == DemoPhase.Main && battlePhaseEndedThisTurn;
    public int? Winner { get; private set; }
    public int? ExodiaWinner { get; private set; }
    public int? DestinyBoardWinner { get; private set; }

    private void CheckDestinyBoard(int player)
    {
        if (GameOver) return;
        var complete = DestinyBoardRules.CompleteField(player, Spells, SpellFaceDown);
        if (complete is null) return;
        DestinyBoardWinner = Winner = player;
        DestinyBoardCompleted?.Invoke(new(player, complete));
    }

    private (DuelZone Source, int Index, int Slot)? FindEndPhaseMessage(int endingPlayer)
    {
        int owner = 1 - endingPlayer;
        if (AreTrapsSuppressed || DestinyBoardRules.NextMessage(owner, Spells, SpellFaceDown) is not ushort resourceId)
            return null;
        int slot = Enumerable.Range(0, 5).FirstOrDefault(z => Spells[owner, z] is null, -1);
        if (slot < 0) return null;

        int index = hands[owner].FindIndex(card => card.Id == resourceId);
        if (index >= 0) return (DuelZone.Hand, index, slot);
        int deckIndex = 0;
        foreach (var card in decks[owner])
        {
            if (card.Id == resourceId) return (DuelZone.Deck, deckIndex, slot);
            deckIndex++;
        }
        return null;
    }

    private void PlaceEndPhaseMessage(int owner, DuelZone source, int index, int slot)
    {
        bool fromDeck = source == DuelZone.Deck;
        Card chosen;
        if (fromDeck)
        {
            // Keep the deck's relative order and consume no RNG.
            Card[] cards = decks[owner].ToArray();
            chosen = cards[index];
            decks[owner].Clear();
            for (int i = 0; i < cards.Length; i++)
                if (i != index) decks[owner].Enqueue(cards[i]);
        }
        else
        {
            chosen = hands[owner][index];
            hands[owner].RemoveAt(index);
        }
        Spells[owner, slot] = chosen;
        SpellFaceDown[owner, slot] = false;
        DestinyBoardMessagePlaced?.Invoke(new(owner, slot, chosen, fromDeck));
        CheckDestinyBoard(owner);
    }

    private void CheckExodia(int player)
    {
        if (GameOver) return;
        var complete = ExodiaRules.CompleteHand(hands[player]);
        if (complete is null) return;
        ExodiaWinner = Winner = player;
        ExodiaCompleted?.Invoke(new(player, complete));
    }
    public const int EndTurnHandLimit = 6;
    // Native suppression/negation is not modeled; setting a spell does not activate it.
    public bool HandLimitDisabled => Enumerable.Range(0, 2).Any(p =>
        Enumerable.Range(0, 5).Any(z => Spells[p, z]?.Id == 0x593 && !SpellFaceDown[p, z]));
    public int PendingEndDiscards => Phase == DemoPhase.End && queuedEndReturn is null ? ExcessEndTurnCards : 0;
    private int ExcessEndTurnCards => HandLimitDisabled ? 0 : Math.Max(0, Hand.Count - EndTurnHandLimit);
    private DemoDuel() { primitiveActionQueue = BuildActionQueue(); }
    // Demo orchestration: seeded shuffle and identical-deck local matches are not native RE claims.
    public static DemoDuel FromDecks(IReadOnlyList<Card> first, IReadOnlyList<Card> second, int seed = 1979,
        IReadOnlyList<Card>? firstFusionDeck = null, IReadOnlyList<Card>? secondFusionDeck = null)
    {
        if (first.Count is < 40 or > 80 || second.Count is < 40 or > 80)
            throw new ArgumentException("Imported main decks must contain 40 to 80 cards");
        var duel = new DemoDuel { effectRandom = new Random(unchecked(seed ^ 0x4D4A3220)) };
        duel.LoadFusionDeck(0, firstFusionDeck);
        duel.LoadFusionDeck(1, secondFusionDeck);
        var random = new Random(seed);
        for (int player = 0; player < 2; player++)
        {
            Card[] shuffled = (player == 0 ? first : second).ToArray();
            random.Shuffle(shuffled);
            duel.hands[player].AddRange(shuffled.Take(5).Select(duel.NewOccurrence));
            foreach (var card in shuffled.Skip(5)) duel.decks[player].Enqueue(duel.NewOccurrence(card));
        }
        // Opening-hand win. Simultaneous Exodia opening hands are outside
        // the observed Joey replay corpus; the seeded sandbox resolves the
        // first player's hand first rather than claiming native priority.
        duel.CheckExodia(0);
        duel.CheckExodia(1);
        return duel;
    }
    /// <summary>
    /// Minimal state hydration for differential tests against partially observed
    /// Joey snapshots. This is NOT a native save/load implementation: unknown
    /// deck/graveyard/flags/chain state is deliberately left at demo defaults.
    /// Only use fixtures that explicitly bound which fields are compared. Equipment
    /// context stays unknown unless the caller explicitly opts into closed sandbox policy.
    /// </summary>
    public static DemoDuel FromPartialObservation(
        int activeSide,
        IReadOnlyList<Card> activeHand,
        IReadOnlyList<Card?> side0Monsters,
        IReadOnlyList<Card?> side1Monsters,
        int activeLifePoints,
        bool equipmentContextResolved = false,
        IReadOnlyList<Card>? side0Graveyard = null,
        IReadOnlyList<Card>? side1Graveyard = null,
        IReadOnlyList<Card>? side0Deck = null,
        IReadOnlyList<Card>? side1Deck = null,
        int effectShuffleSeed = 1979,
        IReadOnlyList<Card>? side0FusionDeck = null,
        IReadOnlyList<Card>? side1FusionDeck = null,
        DuelCheckpoint? checkpoint = null)
    {
        if (activeSide is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(activeSide));
        ArgumentNullException.ThrowIfNull(activeHand);
        ArgumentNullException.ThrowIfNull(side0Monsters);
        ArgumentNullException.ThrowIfNull(side1Monsters);
        if (side0Monsters.Count != 5 || side1Monsters.Count != 5)
            throw new ArgumentException("A partial observation must specify five monster slots per side.");
        if (activeLifePoints <= 0)
            throw new ArgumentOutOfRangeException(nameof(activeLifePoints),
                "This adapter only models an ongoing main phase.");
        if (activeHand.Any(card => card is null))
            throw new ArgumentException("Active hand must not contain null cards.", nameof(activeHand));

        int turn = checkpoint?.Turn ?? activeSide + 1;
        var phase = checkpoint?.Phase ?? DemoPhase.Main;
        if (turn < 1 || (turn - 1) % 2 != activeSide ||
            phase is not (DemoPhase.Main or DemoPhase.Battle) ||
            phase == DemoPhase.Battle && turn == 1)
            throw new ArgumentException("Checkpoint turn, side or phase is inconsistent.", nameof(checkpoint));
        var duel = new DemoDuel
        {
            Turn = turn,
            Phase = phase,
            drawn = checkpoint?.DrawTaken ?? false,
            equipmentContextResolved = equipmentContextResolved,
            effectRandom = new Random(effectShuffleSeed)
        };
        duel.LoadFusionDeck(0, side0FusionDeck);
        duel.LoadFusionDeck(1, side1FusionDeck);
        duel.Life[activeSide] = activeLifePoints;
        duel.hands[activeSide].AddRange(activeHand.Select(duel.NewOccurrence));
        if (side0Graveyard is not null) duel.graves[0].AddRange(side0Graveyard.Select(duel.NewOccurrence));
        if (side1Graveyard is not null) duel.graves[1].AddRange(side1Graveyard.Select(duel.NewOccurrence));
        if (side0Deck is not null) foreach (var c in side0Deck) duel.decks[0].Enqueue(duel.NewOccurrence(c));
        if (side1Deck is not null) foreach (var c in side1Deck) duel.decks[1].Enqueue(duel.NewOccurrence(c));
        for (int slot = 0; slot < 5; slot++)
        {
            duel.Monsters[0, slot] = side0Monsters[slot] is Card a ? duel.NewOccurrence(a) : null;
            duel.Monsters[1, slot] = side1Monsters[slot] is Card b ? duel.NewOccurrence(b) : null;
        }
        void LoadSetSpells(int side, IReadOnlyList<Card?>? setSpells)
        {
            if (setSpells is null) return;
            if (setSpells.Count != 5 || setSpells.Any(c => c is not null && c.ClassCode is not (21 or 22 or 23)) ||
                turn == 1 && setSpells.Any(c => c is not null))
                throw new ArgumentException("Checkpoint requires five legal previously Set Spell/Trap slots.", nameof(checkpoint));
            for (int slot = 0; slot < 5; slot++)
            {
                if (setSpells[slot] is not Card spell) continue;
                duel.Spells[side, slot] = duel.NewOccurrence(spell);
                duel.SpellFaceDown[side, slot] = true;
                duel.spellSetTurn[side, slot] = turn - 1;
            }
        }
        LoadSetSpells(0, checkpoint?.Side0SetSpells);
        LoadSetSpells(1, checkpoint?.Side1SetSpells);
        duel.CheckExodia(activeSide);
        return duel;
    }

    public DemoDuel(IEnumerable<Card> cards) : this()
    {
        var all = cards.Where(c => c.Name.Length > 0).ToList();
        var monsters = all.Where(c => c.IsMonster).Take(12).ToList();
        var warrior = all.FirstOrDefault(c => c.ClassCode == 15);
        if (warrior != null && !monsters.Take(3).Contains(warrior)) monsters.Insert(0, warrior);
        // Prioritize featured FLIP monsters in the local demo's opening hand.
        foreach (ushort id in new ushort[] { FlipEffectRules.PenguinSoldier, FlipEffectRules.MorphingJar,
                                             FlipEffectRules.ManEaterBug, FlipEffectRules.HaneHane,
                                             FlipEffectRules.MorphingJar2,
                                              FlipEffectRules.MagicianOfFaith, FlipEffectRules.MaskOfDarkness })
        {
            var flip = all.FirstOrDefault(c => c.Id == id);
            if (flip is null) continue;
            monsters.Remove(flip);
            monsters.Insert(0, flip);
        }
        var spells = all.Where(c => EquipmentRules.Supports(c.Id) ||
            LifePointSpellRules.Supports(c.Id) || FieldSpellRules.Supports(c.Id) ||
            (c.ClassCode == 22 && (MonsterWipeRules.Scope(c.Id) is not null ||
                SpellTrapWipeRules.Supports(c.Id) || SpellTrapTargetRules.Supports(c.Id))))
            .OrderBy(c => c.Id == 300 ? 1 : 0).ToList();
        // Fixed reproducible fixture; both players have an opening hand and a small deck.
        for (int p = 0; p < 2; p++)
        {
            hands[p].AddRange(monsters.Take(3).Select(NewOccurrence));
            hands[p].AddRange(spells.Select(NewOccurrence));
            foreach (var c in monsters.Skip(3).Concat(spells)) decks[p].Enqueue(NewOccurrence(c));
        }
        CheckExodia(0);
        CheckExodia(1);
    }
    public static int NormalTributes(Card card) => card.Level <= 4 ? 0 : card.Level <= 6 ? 1 : 2;
    private bool CanAttemptNormalSummon(int index) => !ActionsBlocked && Phase == DemoPhase.Main && !summoned &&
        index >= 0 && index < Hand.Count && Hand[index].IsMonster &&
        !NormalSummonRules.RequiresSpecialSummon(Hand[index]);

    private int AvailableNormalTributes() =>
        Enumerable.Range(0, 5).Count(z => Monsters[ActivePlayer, z] != null);

    public bool CanSummonFromHand(int index) => CanAttemptNormalSummon(index) &&
        AvailableNormalTributes() >= NormalTributes(Hand[index]) &&
        (NormalTributes(Hand[index]) > 0 || FirstFreeMonsterZone() >= 0);

    // Read-only reason from the same normal-placement eligibility checks.
    // Does not admit a command or duplicate the summon rule in presentation.
    public bool HasInsufficientNormalSummonTributes(int index) =>
        CanAttemptNormalSummon(index) && AvailableNormalTributes() < NormalTributes(Hand[index]);
    /// <summary>Inherent Special Summon: P252 cost only. This does not grant
    /// the delayed Equip/control effect or model summon negation chains.</summary>
    public bool CanSpecialSummonByGraveyardBanish(int handIndex) =>
        !ActionsBlocked && Phase == DemoPhase.Main &&
        handIndex >= 0 && handIndex < Hand.Count &&
        Hand[handIndex].IsMonster &&
        GraveyardBanishSummonRules.RequiredCount(Hand[handIndex].Id) is int required &&
        required > 0 && FirstFreeMonsterZone() >= 0 &&
        GraveyardBanishSummonRules.Candidates(Hand[handIndex].Id, graves[ActivePlayer]).Count >= required;

    public IReadOnlyList<CardSelectionCandidate> GraveyardBanishSummonCandidates(int handIndex) =>
        CanSpecialSummonByGraveyardBanish(handIndex)
            ? GraveyardBanishSummonRules.Candidates(Hand[handIndex].Id, graves[ActivePlayer])
            : Array.Empty<CardSelectionCandidate>();

    internal bool SpecialSummonByGraveyardBanish(int handIndex, int zone,
        Card expectedHandCard, IReadOnlyList<CardSelectionCandidate> cost,
        bool defense, out string message)
    {
        message = "Choose a valid Special Summon, three distinct Fiends in own Graveyard and a free zone.";
        int owner = ActivePlayer;
        if (!CanSpecialSummonByGraveyardBanish(handIndex) ||
            !ReferenceEquals(Hand[handIndex], expectedHandCard) ||
            zone is < 0 or >= 5 || Monsters[owner, zone] is not null ||
            !GraveyardBanishSummonRules.CanPay(expectedHandCard.Id, graves[owner], cost))
            return false;
        // Commit after the full cost, source and destination are checked.
        // This does not consume the normal summon allowance.
        foreach (var candidate in cost.OrderByDescending(c => c.SourceIndex))
            graves[owner].RemoveAt(candidate.SourceIndex);
        foreach (var candidate in cost)
            banished[owner].Add(candidate.Card);
        hands[owner].RemoveAt(handIndex);
        PlaceSelectedMonster(owner, zone, expectedHandCard, false, defense);
        message = $"{expectedHandCard.Name}: banished {cost.Count} Graveyard monsters and Special Summoned.";
        return true;
    }

    public int FirstFreeMonsterZone(IReadOnlyList<int>? tributes = null) => Enumerable.Range(0, 5)
        .FirstOrDefault(z => Monsters[ActivePlayer, z] == null || tributes?.Contains(z) == true, -1);
    public int FirstFreeSpellZone() => Enumerable.Range(0, 5).FirstOrDefault(z => Spells[ActivePlayer, z] == null, -1);
    public bool CanSetFromHand(int index) => index >= 0 && index < Hand.Count && (Hand[index].IsMonster
        ? CanSummonFromHand(index)
        : !ActionsBlocked && Phase == DemoPhase.Main && Hand[index].ClassCode is 21 or 22 or 23 &&
           !DestinyBoardRules.IsMessage(Hand[index].Id) &&
          (FieldSpellRules.Supports(Hand[index].Id) ? FieldSpells[ActivePlayer] is null : FirstFreeSpellZone() >= 0));
    public bool CanActivateFromHand(int index) => !ActionsBlocked && Phase == DemoPhase.Main &&
        index >= 0 && index < Hand.Count &&
        (FieldSpellRules.Supports(Hand[index].Id) ||
        (FirstFreeSpellZone() >= 0 && ((Hand[index].ClassCode == 22 && EquipmentRules.Supports(Hand[index].Id)) ||
            (FusionRules.Supports(Hand[index]) && HasFusionCombination()) ||
            Hand[index].Id == 0x593 || LifePointSpellRules.Supports(Hand[index].Id) ||
            (DrawSpellRules.Effect(Hand[index]) is { } draw && DeckCount(ActivePlayer) >= draw.Cards) ||
            (Hand[index].ClassCode == 22 && SpellTrapWipeRules.Supports(Hand[index].Id) &&
                HasSpellTrapWipeTargets(Hand[index].Id)) ||
            (Hand[index].ClassCode == 22 && SpellTrapTargetRules.Supports(Hand[index].Id) &&
                SpellTrapTargets(Hand[index].Id).Any()) ||
            (Hand[index].ClassCode == 22 && MonsterWipeRules.Targets(
                Hand[index].Id, ActivePlayer, Monsters, MonsterFaceDown).Count > 0))));
    internal bool SetFromHand(int index, out string message, IReadOnlyList<int>? tributes = null)
    {
        message = "Set unavailable: choose a card in main phase with a free matching zone.";
        if (!CanSetFromHand(index)) return false;
        if (Hand[index].IsMonster)
        {
            message = "Monster Set requires queued normal placement.";
            return false;
        }
        if (FieldSpellRules.Supports(Hand[index].Id))
        {
            // Setting a new field spell does not replace the opponent's active one.
            CommitSpellTrapSet(ActivePlayer, index, DuelZone.FieldSpell, 0);
            message = "Field Spell set face-down in its dedicated zone.";
            return true;
        }
        int zone = FirstFreeSpellZone();
        CommitSpellTrapSet(ActivePlayer, index, DuelZone.SpellTrap, zone);
        message = "Card set face-down in the leftmost free spell/trap zone.";
        return true;
    }
    private void CommitNormalPlacement(int index, int player, int zone, bool faceDown)
    {
        Monsters[player, zone] = hands[player][index];
        hands[player].RemoveAt(index);
        summoned = true;
        DefensePosition[player, zone] = faceDown;
        MonsterFaceDown[player, zone] = faceDown;
        MonsterWasFlipped[player, zone] = false;
        MonsterNormalSummoned?.Invoke(new(player, zone, Monsters[player, zone]!, faceDown));
        // Native placement 0x00568610 sets the same turn lock read by 0x0056B5A0.
        changedPosition[player, zone] = true;
    }

    private void FinishNormalPlacement(int player, int zone, bool faceDown, ref string message)
    {
        if (!faceDown)
        {
            // Normal Summon response takes precedence over the optional P165
            // search; the latter remains queued even if Trap Hole destroys
            // its source immediately after the successful Summon.
            var summonedMonster = Monsters[player, zone]!;
            resolvingSummon = true;
            try { QueueRitualSummonSearch(player, summonedMonster); }
            finally { resolvingSummon = false; }
            BeginSummonResponse(new(player, zone, summonedMonster, DemoSummonKind.Normal));
            if (PendingSummonResponse is not null)
                message += " Summon response: choose a set Trap or Pass.";
            else if (PendingCardSelection is not null)
                message += " Resolve the pending card search.";
        }
        else BeginNextQueuedDeckSearch();
    }
    public bool CanDraw => !ActionsBlocked && AllowsTurnDraw;
    private bool AllowsTurnDraw => Phase == DemoPhase.Main && !battlePhaseEndedThisTurn && !drawn;
    public bool CanBeginBattle => !ActionsBlocked && AllowsBattlePhaseChange(enterBattle: true);
    public bool CanChangePosition(int zone) => !ActionsBlocked && Phase == DemoPhase.Main &&
        zone is >= 0 and < 5 && Monsters[ActivePlayer, zone] is { } card &&
        !IsManualPositionLocked(ActivePlayer, zone, card);
    public bool CanAttack(int zone) => !ActionsBlocked && Phase == DemoPhase.Battle &&
        zone is >= 0 and < 5 && Monsters[ActivePlayer, zone] is { } attacker &&
        !DefensePosition[ActivePlayer, zone] && !MonsterFaceDown[ActivePlayer, zone] &&
        (!HasDeclaredAttack(ActivePlayer, zone) ||
            AllowsQueuedAttackUse(ActivePlayer, zone, attacker, declared: false)) &&
        (attacker.Id != ZombyraTheDark || CountOccupiedMonsters(1 - ActivePlayer) > 0) &&
        !AttackRestrictedByContinuousSpells(ActivePlayer, attacker) && !AttackRestrictedByMonster(attacker);
    public bool CanAttackDirectly(int zone) => CanAttack(zone) &&
        DirectAttackAllowed(ActivePlayer, Monsters[ActivePlayer, zone]!);

    private bool DirectAttackAllowed(int player, Card attacker)
    {
        if (attacker.Id == ZombyraTheDark) return false;
        if (DirectAttackRules.AttacksThroughAnyMonster(attacker.Id)) return true;
        int opponent = 1 - player;
        for (int zone = 0; zone < 5; zone++)
        {
            if (Monsters[opponent, zone] is not Card defender) continue;
            if (attacker.Id != DirectAttackRules.AlligatorsSwordDragon) return false;
            if (!MonsterFaceDown[opponent, zone] &&
                !DirectAttackRules.AllowsAlligatorDefender((byte)(defender.Properties >> 29))) return false;
        }
        return true;
    }

    private bool TakeTurnDraw(out bool skipped)
    {
        skipped = ConsumeDueDrawPhaseSkip();
        if (skipped)
        {
            drawn = true; // the turn-draw opportunity is consumed, without movement
            return true;
        }
        if (decks[ActivePlayer].Count == 0)
        {
            Winner = 1 - ActivePlayer;
            return false;
        }
        DrawToHand(ActivePlayer);
        drawn = true;
        return true;
    }

    internal bool Play(int index, out string message)
    {
        message = "Choose a supported spell. Equipment needs a compatible face-up monster.";
        if (ActionsBlocked || Phase != DemoPhase.Main || index < 0 || index >= Hand.Count) return false;
        if (FieldSpellRules.Supports(Hand[index].Id))
        {
            Card field = Hand[index];
            // A face-up Field Spell on either side is replaced; a set card on
            // the opponent's side remains face-down until they activate it.
            ReplaceActiveFields();
            if (FieldSpells[ActivePlayer] is not null) SendFieldSpellToGraveyard(ActivePlayer);
            CommitFieldActivation(ActivePlayer, index);
            message = $"{field.Name}: {FieldSpellRules.EffectSummary(field.Id)}";
            return true;
        }
        if (FirstFreeSpellZone() < 0) { message = "Activation needs a free spell/trap zone."; return false; }
        if (FusionRules.Supports(Hand[index]))
            return BeginFusionSummon(Hand[index], index, fromSet: false, out message);
        if (Hand[index].ClassCode == 22 && SpellTrapTargetRules.Supports(Hand[index].Id))
            return BeginSpellTrapTarget(Hand[index], index, fromSet: false, out message);
        if (Hand[index].ClassCode == 22 && SpellTrapWipeRules.Supports(Hand[index].Id))
            return ActivateSpellTrapWipe(Hand[index], index, fromSet: false, out message);
        if (Hand[index].ClassCode == 22 &&
            MonsterWipeRules.Scope(Hand[index].Id) is not null)
        {
            Card spell = Hand[index];
            if (MonsterWipeRules.Targets(spell.Id, ActivePlayer, Monsters,
                MonsterFaceDown).Count == 0)
            {
                message = "No eligible monsters can be destroyed by this spell.";
                return false;
            }

            // Once validated, this normal Spell is spent. Resolve the whole
            // non-targeting wipe before opening queued monster triggers.
            hands[ActivePlayer].RemoveAt(index);
            graves[ActivePlayer].Add(spell);
            LastMonsterWipe = ResolveMonsterWipe(spell.Id, ActivePlayer);
            message = $"{spell.Name}: destroyed {LastMonsterWipe.TotalDestroyed} monster(s).";
            return true;
        }
        if (Hand[index].Id == 0x593)
        {
            int zone = Enumerable.Range(0, 5).FirstOrDefault(z => Spells[ActivePlayer, z] == null, -1);
            if (zone < 0) { message = "Infinite Cards needs a free spell zone."; return false; }
            CommitContinuousSpellActivation(ActivePlayer, zone, index);
            message = "Infinite Cards active: neither player has an end-turn hand limit.";
            return true;
        }
        if (DrawSpellRules.Effect(Hand[index]) is { } draw)
        {
            if (!CanResolveDrawSpell(draw, out message)) return false;
            var spell = Hand[index];
            hands[ActivePlayer].RemoveAt(index);
            ResolveDrawSpell(spell, draw, out message);
            return true;
        }
        // Demo policy: immediate LP effect and graveyard movement, without chains.
        // Resolve before spending the card, so unsupported spells are atomic.
        var card = Hand[index];
        var change = LifePointSpellRules.ResolveDemo(card.Id, ActivePlayer);
        if (change is null) return false;
        hands[ActivePlayer].RemoveAt(index); graves[ActivePlayer].Add(card);
        ApplyLifePointSpell(change.Value);
        message = $"{card.Name}: LP changed (self {change.Value.CasterDelta}, opponent {change.Value.OpponentDelta}).";
        return true;
    }
    internal bool ActivateSetSpell(int zone, out string message)
    {
        message = "This set card's activation is not implemented in the demo.";
        int p = ActivePlayer;
        if (ActionsBlocked || Phase != DemoPhase.Main || zone < 0 || zone >= 5 ||
            !SpellFaceDown[p, zone] || Spells[p, zone] is not Card card) return false;
        if (FusionRules.Supports(card))
            return BeginFusionSummon(card, zone, fromSet: true, out message);
        if (card.ClassCode == 22 && SpellTrapTargetRules.Supports(card.Id))
        {
            if (card.Id == SpellTrapTargetRules.MysticalSpaceTyphoon && spellSetTurn[p, zone] >= Turn)
            {
                message = "Mystical Space Typhoon cannot activate on its Set turn.";
                return false;
            }
            return BeginSpellTrapTarget(card, zone, fromSet: true, out message);
        }
        if (card.ClassCode == 22 && SpellTrapWipeRules.Supports(card.Id))
            return ActivateSpellTrapWipe(card, zone, fromSet: true, out message);
        if (card.Id == FlipEffectSuppressionRules.RoyalCommand && card.ClassCode == 21)
        {
            // This bounded trap window follows Destiny Board: owner Main Phase
            // on a later turn. Opponent response/chains remain unimplemented.
            if (spellSetTurn[p, zone] >= Turn)
            {
                message = "Royal Command cannot activate on its Set turn.";
                return false;
            }
            if (FlipEffectSuppressionRules.JinzoFaceUp(Monsters, MonsterFaceDown))
            {
                message = "Jinzo prevents activation of Royal Command.";
                return false;
            }
            SpellFaceDown[p, zone] = false;
            message = FlipEffectsNegated
                ? "Royal Command face-up: FLIP effects are negated."
                : "Royal Command face-up: its Trap effect is currently negated.";
            return true;
        }
        if (card.Id == DestinyBoardRules.BoardResourceId)
        {
            // Sandbox constraint: activation from an already-set trap is
            // offered in the owner's Main Phase from a later turn.
            // Opponent-response timing is not yet emulated.
            if (spellSetTurn[p, zone] >= Turn)
            {
                message = "Destiny Board cannot activate on its Set turn.";
                return false;
            }
            SpellFaceDown[p, zone] = false;
            message = "Destiny Board face-up: Spirit Messages resolve on opponent End Phases.";
            CheckDestinyBoard(p);
            return true;
        }
        if (card.ClassCode == 22 && MonsterWipeRules.Scope(card.Id) is not null)
        {
            if (MonsterWipeRules.Targets(card.Id, p, Monsters, MonsterFaceDown).Count == 0)
            {
                message = "No eligible monsters can be destroyed by this set spell.";
                return false;
            }
            Spells[p, zone] = null;
            SpellFaceDown[p, zone] = false;
            graves[p].Add(card);
            LastMonsterWipe = ResolveMonsterWipe(card.Id, p);
            message = $"{card.Name}: destroyed {LastMonsterWipe.TotalDestroyed} monster(s).";
            return true;
        }
        if (card.Id == InfiniteCardsId)
        {
            CommitContinuousSpellActivation(p, zone, null);
            message = "Infinite Cards activated."; return true;
        }
        if (DrawSpellRules.Effect(card) is { } draw)
        {
            if (!CanResolveDrawSpell(draw, out message)) return false;
            Spells[p, zone] = null;
            SpellFaceDown[p, zone] = false;
            ResolveDrawSpell(card, draw, out message);
            return true;
        }
        // Same bounded plan as hand activation; native suppression is not modeled.
        var change = LifePointSpellRules.ResolveDemo(card.Id, p);
        if (change is null) return false;
        Spells[p, zone] = null; SpellFaceDown[p, zone] = false; graves[p].Add(card);
        ApplyLifePointSpell(change.Value);
        message = $"{card.Name}: LP changed (self {change.Value.CasterDelta}, opponent {change.Value.OpponentDelta}).";
        return true;
    }
    internal IEnumerable<DemoSpellTrapTarget> SpellTrapTargets(ushort resourceId, int sourceZone = -1)
    {
        for (int player = 0; player < 2; player++)
        for (int zone = 0; zone <= SpellTrapTargetRules.FieldZone; zone++)
        {
            if (player == ActivePlayer && zone == sourceZone) continue;
            if (SpellTrapCardAt(player, zone) is Card card &&
                SpellTrapTargetRules.Accepts(resourceId, card, IsSpellTrapFaceDown(player, zone)))
                yield return new(player, zone, card);
        }
    }

    /// <summary>
    /// Select a mandatory pair of FLIP S/T targets. Picks do not mutate the
    /// field. Final resolution rechecks both occurrences before moving either.
    /// The same zone can be clicked again to deselect it.
    /// </summary>
    internal bool SelectFlipSpellTrapTarget(int player, int zone, out string message)
    {
        message = "No eligible FLIP Spell/Trap target is pending.";
        if (primitiveActionQueue?.IsBusy == true) return false;
        if (TryNegatePendingFlip(PendingFlipChoice.SpellTrapTargets, out message)) return true;
        if (PendingFlipSpellTrapTargets is not { } pending || GameOver ||
            player is < 0 or > 1 || zone is < 0 or > SpellTrapTargetRules.FieldZone)
            return false;

        int selectedIndex = flipSpellTrapSelections.FindIndex(x =>
            x.Player == player && x.Zone == zone);
        if (selectedIndex >= 0)
        {
            flipSpellTrapSelections.RemoveAt(selectedIndex);
            message = $"Deselected Spell/Trap target ({flipSpellTrapSelections.Count}/2).";
            return true;
        }

        Card? current = SpellTrapCardAt(player, zone);
        if (current is null ||
            !pending.EligibleTargets.Any(t => t.Player == player && t.Zone == zone &&
                ReferenceEquals(t.Card, current)) ||
            !FlipSpellTrapSelectionRules.IsEligible(pending.Source.Id, current,
                IsSpellTrapFaceDown(player, zone)))
        {
            message = "This Spell/Trap occurrence or face state is no longer eligible.";
            return false;
        }

        flipSpellTrapSelections.Add(new(player, zone, current));
        if (flipSpellTrapSelections.Count < PendingFlipSpellTrapTargets.RequiredCount)
        {
            message = "Choose one more distinct Spell/Trap target.";
            return true;
        }

        // Transactional validation: do not remove either card when a previous
        // pick disappeared or was replaced before the second click.
        bool valid = flipSpellTrapSelections.Count == PendingFlipSpellTrapTargets.RequiredCount &&
            flipSpellTrapSelections.All(t =>
                ReferenceEquals(SpellTrapCardAt(t.Player, t.Zone), t.Card) &&
                FlipSpellTrapSelectionRules.IsEligible(pending.Source.Id, t.Card,
                    IsSpellTrapFaceDown(t.Player, t.Zone)));
        if (!valid)
        {
            flipSpellTrapSelections.RemoveAt(flipSpellTrapSelections.Count - 1);
            message = "A previously selected target changed; deselect it and choose again.";
            return false;
        }

        DemoSpellTrapTarget[] targets = flipSpellTrapSelections.ToArray();
        bool[] activeBoardLeaves = new bool[2];
        foreach (var t in targets)
        {
            if (!IsSpellTrapFaceDown(t.Player, t.Zone) &&
                t.Card.Id == DestinyBoardRules.BoardResourceId &&
                DestinyBoardRules.HasActiveBoard(t.Player, Spells, SpellFaceDown))
                activeBoardLeaves[t.Player] = true;
        }
        PendingFlipSpellTrapTargets = null;
        flipSpellTrapSelections.Clear();

        foreach (var t in targets)
            if (pending.Operation == FlipSpellTrapOperation.Destroy)
                DestroySpellTrapCard(t.Player, t.Zone);
            else ReturnSpellTrapCardToOwnerHand(t.Player, t.Zone);

        // Mirror the existing MST cleanup for a face-up Destiny Board leaving.
        // Full chained-resolution equivalence remains pending native evidence.
        for (int side = 0; side < 2; side++)
        {
            if (!activeBoardLeaves[side]) continue;
            for (int z = 0; z < 5; z++)
                if (Spells[side, z] is Card piece &&
                    !SpellFaceDown[side, z] && IsDestinyBoardPiece(piece))
                    DestroySpellTrapCard(side, z);
        }

        BeginNextQueuedDeckSearch();
        message = pending.Operation == FlipSpellTrapOperation.Destroy
            ? $"{pending.Source.Name} FLIP: destroyed two Set Spell/Trap Cards."
            : $"{pending.Source.Name} FLIP: returned two Spell/Trap Cards to their owners' hands.";
        return true;
    }

    /// <summary>Unlike RemoveEquipmentCore, a bounced equip does not enter Graveyard.</summary>
    private void ReturnSpellTrapCardToOwnerHand(int player, int zone)
    {
        Card? card = SpellTrapCardAt(player, zone);
        if (card is null) return;
        if (zone == SpellTrapTargetRules.FieldZone)
        {
            FieldSpells[player] = null;
            FieldSpellFaceDown[player] = false;
        }
        else
        {
            var link = equipment.SingleOrDefault(e => e.Owner == player &&
                e.SourceZone == zone && ReferenceEquals(e.Card, card));
            if (link is not null) equipment.Remove(link);
            ClearSpellTrapSlot(player, zone);
        }
        hands[player].Add(card);
    }

    private Card? SpellTrapCardAt(int player, int zone) => zone == SpellTrapTargetRules.FieldZone
        ? FieldSpells[player] : Spells[player, zone];

    private bool IsSpellTrapFaceDown(int player, int zone) => zone == SpellTrapTargetRules.FieldZone
        ? FieldSpellFaceDown[player] : SpellFaceDown[player, zone];

    private bool BeginSpellTrapTarget(Card spell, int source, bool fromSet, out string message)
    {
        var candidates = SpellTrapTargets(spell.Id, fromSet ? source : -1).ToArray();
        if (candidates.Length == 0)
        {
            message = "No eligible Spell or Trap to target.";
            return false;
        }
        PendingSpellTrapTarget = new(ActivePlayer, spell, source, fromSet, Array.AsReadOnly(candidates));
        message = $"{spell.Name}: choose a Spell or Trap to destroy.";
        return true;
    }

    public bool CancelSpellTrapTarget(out string message)
    {
        message = "No Spell or Trap target choice is pending.";
        if (primitiveActionQueue?.IsBusy == true) return false;
        if (PendingSpellTrapTarget is null) return false;
        PendingSpellTrapTarget = null;
        message = "Target selection canceled.";
        return true;
    }

    internal bool ResolveSpellTrapTarget(int player, int zone, out string message)
    {
        message = "Choose an eligible Spell or Trap still in its original zone.";
        if (primitiveActionQueue?.IsBusy == true) return false;
        if (GameOver || Phase != DemoPhase.Main || PendingSpellTrapTarget is not { } pending ||
            pending.Owner != ActivePlayer || player is < 0 or > 1 ||
            zone is < 0 or > SpellTrapTargetRules.FieldZone ||
            SpellTrapCardAt(player, zone) is not Card target ||
            !pending.EligibleTargets.Any(candidate => candidate.Player == player &&
                candidate.Zone == zone && ReferenceEquals(candidate.Card, target)) ||
            !SpellTrapTargetRules.Accepts(pending.Spell.Id, target, IsSpellTrapFaceDown(player, zone)))
            return false;

        int source = pending.SourceIndex;
        if (pending.FromSet
            ? !SpellFaceDown[pending.Owner, source] || !ReferenceEquals(Spells[pending.Owner, source], pending.Spell)
            : source >= Hand.Count || !ReferenceEquals(Hand[source], pending.Spell) || FirstFreeSpellZone() < 0)
        {
            message = "The activating Spell is no longer available.";
            return false;
        }

        bool clearBoard = !IsSpellTrapFaceDown(player, zone) && IsDestinyBoardPiece(target) &&
            DestinyBoardRules.HasActiveBoard(player, Spells, SpellFaceDown);
        PendingSpellTrapTarget = null;
        if (pending.FromSet) ClearSpellTrapSlot(pending.Owner, source);
        else hands[pending.Owner].RemoveAt(source);
        graves[pending.Owner].Add(pending.Spell);
        DestroySpellTrapCard(player, zone);
        if (clearBoard)
        {
            // 00576360/00577280 remove other face-up pieces on the same side.
            for (int slot = 0; slot < 5; slot++)
                if (Spells[player, slot] is Card piece && !SpellFaceDown[player, slot] && IsDestinyBoardPiece(piece))
                    DestroySpellTrapCard(player, slot);
        }
        message = $"{pending.Spell.Name}: destroyed a Spell or Trap.";
        return true;
    }

    private static bool IsDestinyBoardPiece(Card card) =>
        card.Id == DestinyBoardRules.BoardResourceId || DestinyBoardRules.IsMessage(card.Id);

    private bool HasSpellTrapWipeTargets(ushort resourceId, int sourceZone = -1)
    {
        int caster = ActivePlayer;
        return SpellTrapWipeRules.CountOnSide(1 - caster, Spells, FieldSpells) > 0 ||
            SpellTrapWipeRules.HitsOwnSide(resourceId) &&
            SpellTrapWipeRules.CountOnSide(caster, Spells, FieldSpells, sourceZone) > 0;
    }

    private bool ActivateSpellTrapWipe(Card spell, int source, bool fromSet, out string message)
    {
        int caster = ActivePlayer;
        if (!HasSpellTrapWipeTargets(spell.Id, fromSet ? source : -1))
        {
            message = "No eligible Spell or Trap Cards can be destroyed.";
            return false;
        }

        if (fromSet) ClearSpellTrapSlot(caster, source);
        else hands[caster].RemoveAt(source);
        graves[caster].Add(spell);

        int destroyed = DestroySpellTrapSide(1 - caster);
        if (SpellTrapWipeRules.HitsOwnSide(spell.Id)) destroyed += DestroySpellTrapSide(caster);
        message = $"{spell.Name}: destroyed {destroyed} Spell or Trap Card(s).";
        return true;
    }

    private int DestroySpellTrapSide(int owner)
    {
        int count = 0;
        for (int zone = 0; zone <= SpellTrapTargetRules.FieldZone; zone++)
        {
            if (SpellTrapCardAt(owner, zone) is null) continue;
            DestroySpellTrapCard(owner, zone);
            count++;
        }
        return count;
    }

    private void DestroySpellTrapCard(int owner, int zone)
    {
        if (zone == SpellTrapTargetRules.FieldZone)
        {
            SendFieldSpellToGraveyard(owner);
            return;
        }
        if (Spells[owner, zone] is not Card card) return;
        RemoveEquipmentCore(owner, zone);
        if (Spells[owner, zone] is null) return;
        ClearSpellTrapSlot(owner, zone);
        graves[owner].Add(card);
    }

    private void ClearSpellTrapSlot(int owner, int zone)
    {
        Spells[owner, zone] = null;
        SpellFaceDown[owner, zone] = false;
        spellSetTurn[owner, zone] = 0;
    }

    private bool CanResolveDrawSpell(DrawSpellEffect effect, out string message)
    {
        // No chain can change the Deck between activation and resolution in this sandbox.
        message = $"This spell needs {effect.Cards} card(s) in your deck.";
        return DeckCount(ActivePlayer) >= effect.Cards;
    }

    private void ResolveDrawSpell(Card spell, DrawSpellEffect effect, out string message)
    {
        int player = ActivePlayer;
        graves[player].Add(spell);
        for (int i = 0; i < effect.Cards; i++) DrawToHand(player);
        if (effect.OpponentLifeGain > 0) ChangeLife(1 - player, effect.OpponentLifeGain);
        message = $"{spell.Name}: drew {effect.Cards}." + (effect.OpponentLifeGain > 0
            ? $" Opponent gained {effect.OpponentLifeGain} LP." : "");
    }

    internal bool ActivateSetFieldSpell(out string message)
    {
        message = "Only a set Field Spell in your own zone can activate during Main Phase.";
        int owner = ActivePlayer;
        if (ActionsBlocked || Phase != DemoPhase.Main ||
            FieldSpells[owner] is not Card field || !FieldSpellFaceDown[owner] ||
            !FieldSpellRules.Supports(field.Id)) return false;
        ReplaceActiveFields();
        CommitFieldActivation(owner, null);
        message = $"{field.Name}: {FieldSpellRules.EffectSummary(field.Id)}";
        return true;
    }

    private void ReplaceActiveFields()
    {
        // One active Field Spell globally in this 2004-style demo.
        for (int p = 0; p < 2; p++)
            if (FieldSpells[p] is not null && !FieldSpellFaceDown[p])
                SendFieldSpellToGraveyard(p);
    }

    private void SendFieldSpellToGraveyard(int owner)
    {
        if (FieldSpells[owner] is not Card field) return;
        graves[owner].Add(field);
        FieldSpells[owner] = null;
        FieldSpellFaceDown[owner] = false;
    }

    /// <summary>Sandbox removal hook; not a claim about native destruction timing.</summary>
    internal bool RemoveFieldSpell(int owner)
    {
        if (ActionsBlocked || owner is < 0 or > 1 || FieldSpells[owner] is null) return false;
        SendFieldSpellToGraveyard(owner);
        return true;
    }

    private void ApplyLifePointSpell(LifePointSpellChange change)
    {
        ChangeLife(ActivePlayer, change.CasterDelta);
        ChangeLife(1 - ActivePlayer, change.OpponentDelta);
    }
    private void ChangeLife(int player, int delta, bool deferDuelResult = false)
    {
        // Demo policy: LP cannot be negative; safe int ceiling for repeated healing.
        Life[player] = (int)Math.Clamp((long)Life[player] + delta, 0L, int.MaxValue);
        if (!deferDuelResult && Life[player] == 0 && delta < 0) Winner = 1 - player;
    }
    public int AttackValue(int p, int z) => EquipmentStats(p, z).Attack;
    public int DefenseValue(int p, int z) => EquipmentStats(p, z).Defense;
    private NativeAiStats EquipmentStats(int p, int z, NativeAiAttributeDelta battleBonus = default)
    {
        if (Monsters[p, z] is not Card monster) return default;
        // This sandbox uses DAT attributes. Field, equipment and aura contributions
        // are recalculated on demand before the final clamp.
        var stats = new NativeAiStats((ushort)monster.Index, (byte)monster.ClassCode,
            (byte)(monster.Properties >> 29), monster.Attack, monster.Defense);
        if (MonsterFaceDown[p, z]) return stats; // Native calculation returns before equipment deltas.
        var sum = CountedMonsterAttributes(p, monster.Id);
        foreach (var e in equipment.Where(e => e.TargetPlayer == p && e.TargetZone == z))
        {
            // A sandbox identity check, not a reconstruction of the native selection key.
            if (!ReferenceEquals(e.TargetCard, monster) || !ReferenceEquals(Spells[e.Owner, e.SourceZone], e.Card) ||
                SpellFaceDown[e.Owner, e.SourceZone]) continue;
            if (DynamicEquipmentRules.Supports(e.Card.Id))
            {
                int faceUpMonsters = Enumerable.Range(0, 5).Count(slot =>
                    Monsters[e.Owner, slot] is not null && !MonsterFaceDown[e.Owner, slot]);
                int spellTraps = Enumerable.Range(0, 5).Count(slot => Spells[e.Owner, slot] is not null)
                    + (FieldSpells[e.Owner] is null ? 0 : 1);
                var contribution = DynamicEquipmentRules.Evaluate(e.Card.Id, monster.Attack,
                    Life[e.Owner], Life[1 - e.Owner], faceUpMonsters, spellTraps);
                if (contribution.BaseAttack is int replacement) stats = stats with { Attack = replacement };
                sum = new(unchecked(sum.Attack + contribution.Delta.Attack),
                    unchecked(sum.Defense + contribution.Delta.Defense));
                continue;
            }
            if (Joey.Cards.EquipmentEffects.For(e.Card.Id) is { } definition)
            {
                sum = new(sum.Attack + definition.Attack, sum.Defense + definition.Defense);
                continue;
            }
            if (e.Card.Id == EquipmentRules.FairyMeteorCrush) continue;
            var delta = NativeAiAttributes.EquipmentDelta(e.Card.Id, stats, monster.Id);
            if (!delta.IsSupported) throw new InvalidOperationException("Unsupported equipment in demo link list");
            sum = new(unchecked(sum.Attack + delta.Value.Attack), unchecked(sum.Defense + delta.Value.Defense));
        }
        // Global effects are derived from live links, never from a detached
        // card or a cached modifier. This includes the equipped WATER monster.
        foreach (var e in equipment)
        {
            if (!ReferenceEquals(Spells[e.Owner, e.SourceZone], e.Card) ||
                SpellFaceDown[e.Owner, e.SourceZone] ||
                !ReferenceEquals(Monsters[e.TargetPlayer, e.TargetZone], e.TargetCard) ||
                MonsterFaceDown[e.TargetPlayer, e.TargetZone]) continue;
            sum = new(sum.Attack + Joey.Cards.EquipmentEffects.GlobalAttackDelta(
                e.Card.Id, stats.AttributeCode), sum.Defense);
        }
        // Field Spell is a separate global modifier, not an equipment link.
        // Do not cache: activation, replacement or removal changes stats immediately.
        if (ActiveFieldSpell is Card field)
        {
            var bonus = FieldSpellRules.Bonus(field.Id, monster.ClassCode, (byte)(monster.Properties >> 29), DefensePosition[p, z]);
            sum = new(sum.Attack + bonus.Attack, sum.Defense + bonus.Defense);
        }
        for (int side = 0; side < 2; side++)
        for (int zone = 0; zone < 5; zone++)
        {
            if (Monsters[side, zone] is Card source && !MonsterFaceDown[side, zone])
                sum = new(sum.Attack + MonsterAuraRules.AttackDelta(source.Id, stats.AttributeCode), sum.Defense);
        }
        // Native 0056DD30: phase-dependent elemental summon effects.
        int opposingSouls = Enumerable.Range(0,5).Count(slot =>
            Monsters[1-p,slot]?.Id == ElementalBattleAttributeRules.SoulOfPurityAndLight &&
            !MonsterFaceDown[1-p,slot] && !IsMarkedForBattleDestruction(1-p,slot));
        sum = new(sum.Attack + ElementalBattleAttributeRules.AttackDelta(
            monster.Id, p, ActivePlayer, Phase == DemoPhase.Battle, opposingSouls), sum.Defense);
        var temporary = temporaryStats.GetValueOrDefault(monster);
        var persistent = persistentMonsterStats.GetValueOrDefault(monster);
        sum = new(sum.Attack + temporary.Attack + battleBonus.Attack + persistent.Attack,
            sum.Defense + temporary.Defense + battleBonus.Defense + persistent.Defense);
        // Accumulate all contributions and clamp once. Other native stages are absent.
        return NativeAiAttributes.ApplyResolvedTail(stats, sum, 0, 0);
    }
    private EquipmentTargetContext DemoEquipmentTarget(int player, int zone)
    {
        if (player is < 0 or > 1 || zone is < 0 or >= 5)
            return new(Player: player, Zone: zone);
        var card = Monsters[player, zone];
        return new(player, zone, card?.Index ?? 0, card?.Id ?? 0, !MonsterFaceDown[player, zone],
            equipmentContextResolved ? (byte)(card?.ClassCode ?? 0) : null,
            equipmentContextResolved ? (byte)((card?.Properties ?? 0) >> 29) : null,
            equipmentContextResolved ? true : null);
    }
    public NativeAiResult<bool> EquipmentEligibility(int index, int targetPlayer, int targetZone)
    {
        if (ActionsBlocked || Phase != DemoPhase.Main || index < 0 || index >= Hand.Count ||
            targetPlayer is < 0 or > 1 || targetZone is < 0 or >= 5 || FirstFreeSpellZone() < 0 ||
            Hand[index].ClassCode != 22 || Monsters[targetPlayer,targetZone] is not { IsMonster: true })
            return NativeAiResult<bool>.Supported(false);
        return EquipmentRules.Eligibility(Hand[index].Id, DemoEquipmentTarget(targetPlayer, targetZone));
    }
    internal bool Equip(int index, int targetPlayer, int targetZone, out string message)
    {
        message = "Equipment needs a compatible face-up monster and a free own spell zone.";
        // Re-read the live target and hand at resolution, after UI selection. No mutation before validation.
        var eligibility = EquipmentEligibility(index, targetPlayer, targetZone);
        if (!eligibility.IsSupported) { message = string.Join("; ", eligibility.UnsupportedReasons); return false; }
        if (!eligibility.Value) return false;
        int sourceZone = FirstFreeSpellZone();
        var card = Hand[index];
        CommitEquipmentActivation(ActivePlayer, sourceZone, index, card,
            targetPlayer, targetZone, Monsters[targetPlayer, targetZone]!);
        message = $"{card.Name} equipped."; return true;
    }
    /// <summary>Explicit sandbox removal, not an activation, native movement rule or trigger.</summary>
    internal bool RemoveEquipment(int owner, int sourceZone) =>
        !ActionsBlocked && RemoveEquipmentCore(owner, sourceZone);

    // Cleanup from combat/returns is mandatory even while a duel is ending
    // or a FLIP is pending. Only external actions use the interaction guard.
    private bool RemoveEquipmentCore(int owner, int sourceZone)
    {
        if (owner is < 0 or > 1 || sourceZone is < 0 or >= 5) return false;
        var e = equipment.SingleOrDefault(e => e.Owner == owner && e.SourceZone == sourceZone);
        if (e is null) return false;
        equipment.Remove(e);
        // Preserve an unrelated occupant if a caller directly replaced a public fixture slot.
        if (ReferenceEquals(Spells[owner, sourceZone], e.Card))
            ClearSpellTrapSlot(owner, sourceZone);
        graves[owner].Add(e.Card);
        return true;
    }
    /// <summary>
    /// Sandbox hook for cards that reveal a monster by an external effect.
    /// It does not impose a Flip Summon or change its battle position.
    /// </summary>
    internal bool RevealMonsterByEffect(int player, int zone, out string message)
    {
        message = "The monster must be face-down on the field.";
        if (ActionsBlocked || player is < 0 or > 1 || zone is < 0 or >= 5 ||
            Monsters[player, zone] is not Card monster || !MonsterFaceDown[player, zone])
            return false;
        MonsterFaceDown[player, zone] = false;
        MonsterWasFlipped[player, zone] = true;
        TriggerFlip(player, zone, monster, out message);
        BeginNextEffectDecision();
        if (message.Length == 0) message = "Monster revealed by an effect (no supported FLIP).";
        return true;
    }

    internal bool ChangePosition(int zone, out string message)
    {
        message = "Change position in main phase, once per turn, after the monster's summon turn.";
        int owner = ActivePlayer;
        if (!CanChangePosition(zone)) return false;
        var change = CommitManualPosition(owner, zone);
        FinishManualPosition(change, resolveFlip: true, out message);
        return true;
    }

    private DemoMonsterPositionChange CommitManualPosition(int owner, int zone, bool countsAsManualChange = true)
    {
        var monster = Monsters[owner, zone]!;
        bool flipped = MonsterFaceDown[owner, zone];
        bool oldDefense = DefensePosition[owner, zone];
        DefensePosition[owner, zone] = !oldDefense;
        MonsterFaceDown[owner, zone] = false;
        if (countsAsManualChange) changedPosition[owner, zone] = true;
        if (flipped) MonsterWasFlipped[owner, zone] = true;
        var change = new DemoMonsterPositionChange(owner, zone, monster,
            flipped, oldDefense, false, DefensePosition[owner, zone]);
        MonsterPositionChanged?.Invoke(change);
        return change;
    }

    private void FinishManualPosition(DemoMonsterPositionChange change, bool resolveFlip, out string message)
    {
        int owner = change.Player, zone = change.Zone;
        Card monster = change.Card;
        message = "";
        if (change.FlipSummoned)
        {
            if (resolveFlip) TriggerFlip(owner, zone, monster, out message);
            // This route is a real Flip Summon, unlike an attack reveal or
            // RevealMonsterByEffect. P165 searches must only use this path.
            resolvingSummon = true;
            try { QueueRitualSummonSearch(owner, monster); }
            finally { resolvingSummon = false; }
            BeginSummonResponse(new(owner, zone, monster, DemoSummonKind.Flip));
            BeginNextEffectDecision();
            if (PendingSummonResponse is not null)
                message = $"{monster.Name} Flip Summoned: choose a set Trap or Pass.";
            else if (PendingCardSelection is not null)
                message = $"{monster.Name} Flip Summoned: resolve its pending effect.";
            else if (message.Length == 0)
                message = "Flip Summon: face-up attack position.";
        }
        else if (MonsterPositionEffectRules.Trigger(change) ==
                 MonsterPositionTriggeredEffect.DestroyOpponentMonster)
        {
            if (Enumerable.Range(0, 5).Any(z => Monsters[1 - owner, z] is not null))
            {
                PendingFlipTarget = new(owner, zone, monster, DemoFlipEffect.DestroyOpponentMonster,
                    DemoMonsterEffectTrigger.PositionChange);
                message = $"{monster.Name}: choose an opposing monster to destroy.";
            }
            else message = $"{monster.Name}: no opposing monster to destroy.";
        }
        else message = DefensePosition[owner, zone] ?
            "Monster in face-up defense (demo)." : "Monster in attack position (demo).";
    }
    private bool AllowsBattlePhaseChange(bool enterBattle) => enterBattle
        ? Turn > 1 && Phase == DemoPhase.Main && !battlePhaseEndedThisTurn
        : Phase == DemoPhase.Battle;

    private void ApplyBattlePhaseChange(bool enterBattle)
    {
        if (enterBattle) Phase = DemoPhase.Battle;
        else EnterMainPhase2();
    }

    private void EnterMainPhase2()
    {
        Phase = DemoPhase.Main;
        battlePhaseEndedThisTurn = true;
        firstBattleAttacker = null;
    }
    private void TriggerFlip(int owner, int zone, Card card, out string message)
    {
        message = "";
        if (FlipEffectRules.Supports(card.Id) && FlipEffectsNegated)
        {
            message = $"{card.Name} revealed; FLIP effect negated by Royal Command.";
            return;
        }
        switch (FlipEffectRules.Effect(card.Id))
        {
            case DemoFlipEffect.DestroyMonster:
                PendingFlipTarget = new(owner, zone, card, DemoFlipEffect.DestroyMonster);
                message = $"{card.Name} FLIP (P{owner + 1}): choose a monster on either side to destroy.";
                break;
            case DemoFlipEffect.ReturnUpToTwo:
                PendingFlipTarget = new(owner, zone, card, DemoFlipEffect.ReturnUpToTwo);
                flipSelections.Clear();
                message = $"{card.Name} FLIP (P{owner + 1}): choose up to two monsters to return to hand, then Done.";
                break;
            case DemoFlipEffect.ReturnOne:
                // An obligatory single-target effect only starts target selection
                // when a monster can legally return. Both battle-destroyed
                // combatants may still occupy the field during the Damage Step.
                bool anyEligible = Enumerable.Range(0, 2).Any(p =>
                    Enumerable.Range(0, 5).Any(z =>
                        Monsters[p, z] is not null && !IsMarkedForBattleDestruction(p, z)));
                if (!anyEligible)
                {
                    message = $"{card.Name} FLIP: no eligible monster remains to return.";
                    break;
                }
                PendingFlipTarget = new(owner, zone, card, DemoFlipEffect.ReturnOne);
                flipSelections.Clear();
                message = $"{card.Name} FLIP (P{owner + 1}): choose exactly one monster to return to its owner's hand.";
                break;
            case DemoFlipEffect.DestroyTwoSetSpellTraps:
            case DemoFlipEffect.ReturnTwoSpellTraps:
                var effect = FlipSpellTrapSelectionRules.Effect(card.Id);
                if (effect is null) break;
                var spellTargets = FlipSpellTrapSelectionRules.Candidates(card.Id, Spells,
                    SpellFaceDown, FieldSpells, FieldSpellFaceDown);
                if (spellTargets.Count < PendingFlipSpellTrapTargets.RequiredCount)
                {
                    message = $"{card.Name} FLIP: fewer than two eligible Spell/Trap Cards.";
                    break;
                }
                flipSpellTrapSelections.Clear();
                PendingFlipSpellTrapTargets = new(owner, card, effect.Value,
                    card.Id == FlipEffectRules.Greenkappa, spellTargets);
                message = $"{card.Name} FLIP (P{owner + 1}): select exactly two " +
                    (card.Id == FlipEffectRules.Greenkappa ? "face-down " : "") +
                    "Spell/Trap Cards on either side.";
                break;
            case DemoFlipEffect.RecruitSameNameFaceDown:
                // Bubonic Vermin: optional same-name FLIP recruit, not a battle-to-GY trigger.
                if (!Enumerable.Range(0, 5).Any(z => Monsters[owner, z] is null))
                {
                    message = $"{card.Name} FLIP: no free monster zone for a recruit.";
                    break;
                }
                var sameNameCandidates = CardSelectionRules.Candidates(
                    decks[owner].ToArray(), CardSelectionFilter.SameNameMonster,
                    requiredResourceId: card.Id);
                if (sameNameCandidates.Count == 0)
                {
                    message = $"{card.Name} FLIP: no same-name monster in your Deck.";
                    break;
                }
                selectedBatchIndices.Clear();
                PendingCardSelection = new(owner, CardSelectionSource.Deck,
                    CardSelectionDestination.Field, CardSelectionFilter.SameNameMonster,
                    sameNameCandidates, 0, 1, card.Id,
                    RequiredResourceId: card.Id,
                    SummonPosition: CardSelectionPosition.FaceDownDefense);
                message = $"{card.Name} FLIP: optionally select one same-name monster from your Deck.";
                break;
            case DemoFlipEffect.RecoverFromGraveyard:
                var filter = CardSelectionRules.FlipGraveyardFilter(card.Id);
                if (filter is null) break;
                var candidates = CardSelectionRules.Candidates(graves[owner], filter.Value);
                if (candidates.Count == 0)
                {
                    message = $"{card.Name} FLIP: no eligible card in P{owner + 1}'s graveyard.";
                    break;
                }
                PendingCardSelection = new(owner, CardSelectionSource.Graveyard,
                    CardSelectionDestination.Hand, filter.Value, candidates, 1, 1, card.Id);
                message = $"{card.Name} FLIP (P{owner + 1}): choose one {filter.Value} from your graveyard.";
                break;
            case DemoFlipEffect.Gain3000LifePoints:
                // P065: this is the FLIP half of the original card.
                // Its separate field-to-Graveyard LP loss is handled centrally
                // by RemoveMonster, conditional on this occurrence's flip history.
                ChangeLife(owner, ImmortalThunderRules.FlipLifeGain);
                message = $"{card.Name} FLIP: P{owner + 1} gained {ImmortalThunderRules.FlipLifeGain} LP.";
                break;
            case DemoFlipEffect.BanishThreeDeckBurn800:
            case DemoFlipEffect.BanishTwoRandomHandBurn800:
                // P051/P052 share native 00584E60 (800 LP), not their payment
                // producer. Do not represent banishment as Graveyard movement.
                LastFlipBanishBurn = FlipBanishBurnRules.Resolve(
                    card.Id, owner, decks[owner], hands[owner], banished[owner], effectRandom);
                if (LastFlipBanishBurn.Applied)
                {
                    Damage(1 - owner, LastFlipBanishBurn.Damage);
                    message = $"{card.Name} FLIP: banished " +
                        $"{LastFlipBanishBurn.BanishedCards.Count} cards from " +
                        $"{LastFlipBanishBurn.CostSource} and inflicted {LastFlipBanishBurn.Damage} damage.";
                }
                else
                    message = $"{card.Name} FLIP: unavailable banishment cost; no damage.";
                break;
            case DemoFlipEffect.MillOpponentDeck:
                // P082: sending cards straight from deck to GY is not a draw.
                // Missing cards simply end the mill; deck-out is checked on draw.
                LastDeckFlow = DeckFlowRules.SendTopToGraveyard(
                    card.Id, owner, 1 - owner, decks[1 - owner], graves[1 - owner], 5);
                message = $"{card.Name} FLIP: sent {LastDeckFlow.MovedFromDeck} " +
                    $"of 5 top cards from P{2 - owner}'s Deck to their Graveyard.";
                break;
            case DemoFlipEffect.DestroyOpponentFaceUpLevelFour:
                var targets = MonsterWipeRules.LevelFourOpponentTargets(owner, Monsters, MonsterFaceDown)
                    .Where(target => !IsMarkedForBattleDestruction(target.Player, target.Zone)).ToArray();
                LastMonsterWipe = ResolveMonsterWipe(card.Id, owner, explicitTargets: targets);
                message = $"{card.Name} FLIP: destroyed {LastMonsterWipe.TotalDestroyed} face-up Level 4 monster(s).";
                break;
            case DemoFlipEffect.OptionalDrawOne:
                if (decks[owner].Count == 0 || GameOver)
                {
                    message = $"{card.Name} FLIP: no optional draw available.";
                    break;
                }
                queuedEffectDecisions.Enqueue(new(++nextEffectDecisionId, owner, card,
                    EffectDecisionKind.DrawOneOnFlip, LifePointCost: 0, CreatedTurn: Turn));
                message = $"{card.Name} FLIP: P{owner + 1} may draw one card.";
                break;
            case DemoFlipEffect.DrawAndDiscardOpponentSpells:
                // P088: draw exactly three (if available), then discard only
                // Spells among those just drawn. Existing hand cards are untouched.
                LastDeckFlow = DeckFlowRules.DrawThenDiscardNewSpells(
                    card.Id, owner, 1 - owner, decks[1 - owner],
                    hands[1 - owner], graves[1 - owner], 3);
                CheckExodia(1 - owner);
                // Bounded demo policy, not an observed native deck-out sequence:
                // a forced draw shortfall is a loss for the drawing player.
                if (LastDeckFlow.DrawFailed && Winner is null)
                    Winner = owner;
                message = $"{card.Name} FLIP: P{2 - owner} drew " +
                    $"{LastDeckFlow.MovedFromDeck}/3, discarded " +
                    $"{LastDeckFlow.NewlyDrawnSpellsDiscarded} newly drawn Spells." +
                    (LastDeckFlow.DrawFailed ? " Forced draw failed: duel ended (sandbox)." : "");
                break;
            case DemoFlipEffect.ShuffleAndExcavate:
                ResolveMorphingJar2(out message);
                break;
            case DemoFlipEffect.ReplaceHands:
                for (int p = 0; p < 2; p++)
                {
                    while (hands[p].Count > 0)
                    {
                        Card discarded = hands[p][0];
                        hands[p].RemoveAt(0);
                        graves[p].Add(discarded);
                        HandTransferred?.Invoke(new(p, 0, discarded, FromDeck: false));
                    }
                }
                bool[] deckOut = [decks[0].Count < 5, decks[1].Count < 5];
                for (int p = 0; p < 2; p++)
                    for (int i = 0; i < 5 && decks[p].Count > 0; i++)
                        DrawToHand(p);
                if (!GameOver)
                {
                    if (deckOut[0] && deckOut[1]) IsDraw = true;
                    else if (deckOut[0]) Winner = 1;
                    else if (deckOut[1]) Winner = 0;
                }
                message = deckOut[0] || deckOut[1]
                    ? $"{card.Name} FLIP: both discard and draw; insufficient deck cards end the demo duel."
                    : $"{card.Name} FLIP: both discard their hands and draw 5.";
                break;
        }
    }

    private void DrawToHand(int player, bool checkExodia = true)
    {
        Card card = decks[player].Dequeue();
        int index = hands[player].Count;
        hands[player].Add(card);
        HandTransferred?.Invoke(new(player, index, card, FromDeck: true));
        if (checkExodia) CheckExodia(player);
    }

    /// <summary>
    /// Morphing Jar #2 (PSV-era sandbox): return unmarked Main Deck monsters,
    /// independently shuffle each owner's deck, then excavate until that side
    /// has found as many monsters as it returned. High-level/special-only
    /// monsters and non-monsters are sent to the graveyard. Fusion monsters
    /// return to their Fusion Deck and do not add to the Main Deck quota.
    /// Tokens, field control changes and chain windows remain outside this model.
    /// </summary>
    private void ResolveMorphingJar2(out string message)
    {
        int[] returned = [0, 0];
        LastMorphingJar2 = [];

        // Move all eligible monsters before shuffling either deck. A monster
        // already marked to die in the Damage Step remains for burial later.
        for (int p = 0; p < 2; p++)
        {
            var additions = new List<Card>();
            for (int z = 0; z < 5; z++)
            {
                if (Monsters[p, z] is not Card card || IsMarkedForBattleDestruction(p, z))
                    continue;
                if (FusionRules.IsFusionMonster(card)) fusionDecks[p].Add(card);
                else additions.Add(card);
                ClearMonsterSlot(p, z);
            }
            returned[p] = additions.Count;
            // A player whose field sent no monsters back keeps the deck's order.
            if (additions.Count == 0) continue;
            var shuffled = decks[p].Concat(additions).ToArray();
            effectRandom.Shuffle(shuffled);
            decks[p].Clear();
            foreach (var card in shuffled) decks[p].Enqueue(card);
            DeckShuffled?.Invoke(p);
        }

        var results = new List<DemoExcavationResult>(2);
        for (int p = 0; p < 2; p++)
        {
            int found = 0;
            var excavated = new List<Card>();
            while (found < returned[p] && decks[p].TryDequeue(out Card? card))
            {
                excavated.Add(card);
                if (card.IsMonster) found++;
            }
            int summoned = 0, sent = 0;
            foreach (var card in excavated)
            {
                int zone = Enumerable.Range(0, 5)
                    .FirstOrDefault(z => Monsters[p, z] is null, -1);
                // Summons from excavation are not Normal Summons: no tributes,
                // but level >4 and cards requiring a different summon cannot go.
                if (card.IsMonster && card.Level is >= 1 and <= 4 &&
                    !NormalSummonRules.RequiresSpecialSummon(card) && !NormalSummonRules.IsFlipOnlySummon(card) && zone >= 0)
                {
                    Monsters[p, zone] = card;
                    MonsterWasFlipped[p, zone] = false;
                    MonsterFaceDown[p, zone] = DefensePosition[p, zone] = true;
                    changedPosition[p, zone] = true;
                    attackDeclarations[p, zone] = 0;
                    MonsterSpecialSummoned?.Invoke(new(p, zone, card, true));
                    summoned++;
                }
                else
                {
                    graves[p].Add(card);
                    sent++;
                }
            }
            results.Add(new(p, returned[p], excavated.Count, found, summoned,
                sent, found < returned[p]));
        }
        LastMorphingJar2 = results.AsReadOnly();
        message = $"Morphing Jar #2 FLIP: returned {returned[0]}/{returned[1]} " +
            $"monsters to decks; excavated {results[0].Excavated}/{results[1].Excavated}; " +
            $"set {results[0].SpecialSummoned}/{results[1].SpecialSummoned} face-down.";
    }

    /// <summary>
    /// P018/P006: GY/deck to hand; P164/P168: deck to field; P169:
    /// graveyard to field. A single card is selected by its source-zone
    /// occurrence, never its catalog index. P168 with two copies has a
    /// separate atomic batch path.
    /// </summary>
    internal bool ResolveCardSelection(int sourceIndex, out string message) =>
        ResolveCardSelection(sourceIndex, null, false, out message);

    internal bool ResolveCardSelection(int sourceIndex, int? destinationZone, out string message) =>
        ResolveCardSelection(sourceIndex, destinationZone, false, out message);

    internal bool ResolveCardSelection(int sourceIndex, int? destinationZone, bool faceDownDefense,
        out string message)
    {
        message = "Choose an eligible card in the pending source zone.";
        if (primitiveActionQueue?.IsBusy == true || HasQueuedBattleRemovalSelectionChoice || HasQueuedBattleRecoveryChoice) return false;
        if (TryNegatePendingFlip(PendingFlipChoice.CardSelection, out message)) return true;
        if (PendingCardSelection is not { MaxSelections: 1 } pending ||
            pending.MinSelections is < 0 or > 1 || pending.Owner is < 0 or > 1)
            return false;

        // Queue-only search spells must resolve through their occurrence-based producer.
        if (DeckSearchSpellRules.Filter(pending.EffectResourceId) is not null) return false;
        int owner = pending.Owner;
        Card[] deckSnapshot = pending.Source == CardSelectionSource.Deck
            ? decks[owner].ToArray() : [];
        IReadOnlyList<Card> source = pending.Source switch
        {
            CardSelectionSource.Deck => deckSnapshot,
            CardSelectionSource.Graveyard => graves[owner],
            _ => []
        };
        CardSelectionCandidate? option = pending.EligibleCards.FirstOrDefault(c => c.SourceIndex == sourceIndex);
        if (option is null || sourceIndex < 0 || sourceIndex >= source.Count ||
            !ReferenceEquals(option.Card, source[sourceIndex]) ||
            !CardSelectionRules.IsEligible(option.Card, pending.Filter,
                pending.RequiredAttributeCode, pending.RequiredResourceId))
        {
            message = "Selected card is no longer eligible at that source position.";
            return false;
        }

        int placementZone = -1;
        if (pending.Destination == CardSelectionDestination.Field)
        {
            if (faceDownDefense && !pending.MaySelectFaceDownDefense)
                return false;
            placementZone = destinationZone ??
                Enumerable.Range(0, 5).FirstOrDefault(z => Monsters[owner, z] is null, -1);
            if (placementZone is < 0 or >= 5 || Monsters[owner, placementZone] is not null)
            {
                message = "Select a free monster zone for this Special Summon.";
                return false;
            }
        }
        else if (pending.Destination != CardSelectionDestination.Hand ||
                 destinationZone is not null || faceDownDefense)
            return false;

        if (pending.Source == CardSelectionSource.Graveyard)
            graves[owner].RemoveAt(sourceIndex);
        else
        {
            // No native shuffle parity is claimed for this seeded sandbox.
            var remaining = deckSnapshot.Where((_, index) => index != sourceIndex).ToArray();
            effectRandom.Shuffle(remaining);
            decks[owner].Clear();
            foreach (var card in remaining) decks[owner].Enqueue(card);
            DeckShuffled?.Invoke(owner);
        }

        if (pending.Destination == CardSelectionDestination.Hand)
        {
            hands[owner].Add(option.Card);
            CheckExodia(owner);
        }
        else
        {
            bool set = faceDownDefense ||
                pending.SummonPosition == CardSelectionPosition.FaceDownDefense;
            PlaceSelectedMonster(owner, placementZone, option.Card, set);
        }

        PendingCardSelection = null;
        selectedBatchIndices.Clear();
        BeginNextQueuedDeckSearch();
        message = pending.Destination == CardSelectionDestination.Field
            ? $"{option.Card.Name} Special Summoned on P{owner + 1}'s field."
            : $"{option.Card.Name} transferred to P{owner + 1}'s hand.";
        if (PendingCardSelection is not null) message += " Another card selection is pending.";
        return true;
    }

    /// <summary>
    /// P168 permits zero, one or two same-name cards from a single Deck.
    /// Accumulate source occurrence indices; no deck or field mutation occurs
    /// until Done (or the maximum is selected), then shuffle exactly once.
    /// </summary>
    internal bool SelectBatchCard(int sourceIndex, out string message)
    {
        message = "A multiple-card recruitment is not pending.";
        if (primitiveActionQueue?.IsBusy == true || HasQueuedBattleRemovalSelectionChoice) return false;
        if (TryNegatePendingFlip(PendingFlipChoice.CardSelection, out message)) return true;
        if (PendingCardSelection is not
            { Source: CardSelectionSource.Deck, Destination: CardSelectionDestination.Field,
              Filter: CardSelectionFilter.SameNameMonster, MinSelections: 0, MaxSelections: > 1 } pending)
            return false;
        Card[] deck = decks[pending.Owner].ToArray();
        var option = pending.EligibleCards.FirstOrDefault(x => x.SourceIndex == sourceIndex);
        if (option is null || sourceIndex < 0 || sourceIndex >= deck.Length ||
            !ReferenceEquals(option.Card, deck[sourceIndex]) ||
            !CardSelectionRules.IsEligible(option.Card, pending.Filter,
                pending.RequiredAttributeCode, pending.RequiredResourceId) ||
            selectedBatchIndices.Contains(sourceIndex) ||
            selectedBatchIndices.Count >= pending.MaxSelections)
            return false;

        selectedBatchIndices.Add(sourceIndex);
        message = $"Selected {selectedBatchIndices.Count}/{pending.MaxSelections} copies.";
        return selectedBatchIndices.Count == pending.MaxSelections
            ? FinishOptionalCardSelection(out message) : true;
    }

    /// <summary>Commit selected P168 same-name copies as a single transaction.</summary>
    internal bool FinishOptionalCardSelection(out string message)
    {
        message = "No multiple same-name recruitment is pending.";
        if (primitiveActionQueue?.IsBusy == true || HasQueuedBattleRemovalSelectionChoice) return false;
        if (TryNegatePendingFlip(PendingFlipChoice.CardSelection, out message)) return true;
        if (PendingCardSelection is not
            { Source: CardSelectionSource.Deck, Destination: CardSelectionDestination.Field,
              Filter: CardSelectionFilter.SameNameMonster, MinSelections: 0, MaxSelections: > 1 } pending)
            return false;
        if (selectedBatchIndices.Count == 0)
            return SkipOptionalCardSelection(out message);

        int owner = pending.Owner;
        int[] choices = selectedBatchIndices.ToArray();
        Card[] deck = decks[owner].ToArray();
        int[] zones = Enumerable.Range(0, 5)
            .Where(z => Monsters[owner, z] is null).Take(choices.Length).ToArray();
        if (choices.Length > pending.MaxSelections || zones.Length < choices.Length ||
            choices.Distinct().Count() != choices.Length ||
            choices.Any(index => index < 0 || index >= deck.Length ||
                !pending.EligibleCards.Any(option => option.SourceIndex == index &&
                    ReferenceEquals(option.Card, deck[index])) ||
                !CardSelectionRules.IsEligible(deck[index], pending.Filter,
                    pending.RequiredAttributeCode, pending.RequiredResourceId)))
            return false;

        var chosen = choices.Select(index => deck[index]).ToArray();
        var omitted = choices.ToHashSet();
        Card[] remainder = deck.Where((_, index) => !omitted.Contains(index)).ToArray();
        effectRandom.Shuffle(remainder);
        decks[owner].Clear();
        foreach (Card card in remainder) decks[owner].Enqueue(card);
        DeckShuffled?.Invoke(owner);
        for (int i = 0; i < chosen.Length; i++)
            PlaceSelectedMonster(owner, zones[i], chosen[i],
                pending.SummonPosition == CardSelectionPosition.FaceDownDefense);

        PendingCardSelection = null;
        selectedBatchIndices.Clear();
        BeginNextQueuedDeckSearch();
        message = $"P{owner + 1} Special Summoned {chosen.Length} copies; Main Deck shuffled once.";
        if (PendingCardSelection is not null) message += " Another card selection is pending.";
        return true;
    }

    private void PlaceSelectedMonster(int owner, int zone, Card card, bool faceDownDefense, bool faceUpDefense = false,
        IReadOnlyList<Card>? fusionMaterials = null)
    {
        Monsters[owner, zone] = card;
        MonsterFaceDown[owner, zone] = faceDownDefense;
        DefensePosition[owner, zone] = faceDownDefense || faceUpDefense;
        MonsterWasFlipped[owner, zone] = false;
        changedPosition[owner, zone] = true;
        attackDeclarations[owner, zone] = 0;
        MonsterSpecialSummoned?.Invoke(new(owner, zone, card, faceDownDefense, faceUpDefense) { FusionMaterials = fusionMaterials });
    }

    internal bool SkipOptionalCardSelection(out string message)
    {
        message = "No optional card selection is pending.";
        if (primitiveActionQueue?.IsBusy == true || HasQueuedBattleRemovalSelectionChoice) return false;
        if (TryNegatePendingFlip(PendingFlipChoice.CardSelection, out message)) return true;
        if (PendingCardSelection is not { MinSelections: 0 } pending)
            return false;
        // Zero is the only skip option: do not silently lose already chosen copies.
        if (selectedBatchIndices.Count != 0) return false;
        PendingCardSelection = null;
        selectedBatchIndices.Clear();
        BeginNextQueuedDeckSearch();
        message = pending.Destination == CardSelectionDestination.Hand
            ? $"P{pending.Owner + 1} declined optional card search; deck unchanged."
            : $"P{pending.Owner + 1} declined optional Special Summon; deck unchanged.";
        if (PendingCardSelection is not null) message += " Another card selection is pending.";
        return true;
    }

    /// <summary>
    /// Open a bounded response only to a successful Normal/Flip Summon.
    /// We do not treat attack/effect reveals, Sets or damage-step recruits as
    /// Summons that can open this specific window.
    /// </summary>
    private void BeginSummonResponse(DemoSummonEvent summon)
    {
        if (GameOver || !ReferenceEquals(Monsters[summon.Player, summon.Zone], summon.Monster))
        {
            BeginNextQueuedDeckSearch();
            return;
        }
        var choices = new List<DemoSummonTrapOption>();
        int atk = AttackValue(summon.Player, summon.Zone);
        for (int p = 0; p < 2; p++)
        for (int z = 0; z < 5; z++)
        {
            if (Spells[p, z] is not Card trap || !SpellFaceDown[p, z] ||
                spellSetTurn[p, z] >= Turn ||
                !SummonTrapRules.IsEligible(trap, p, summon, atk))
                continue;
            choices.Add(new(p, z, trap));
        }
        if (choices.Count > 0)
            PendingSummonResponse = new(summon, choices.AsReadOnly());
        else
            BeginNextQueuedDeckSearch();
    }

    /// <summary>Pass the only response window, without spending either Trap.</summary>
    internal bool PassSummonResponse(out string message)
    {
        message = "No summon response is awaiting a decision.";
        if (primitiveActionQueue?.IsBusy == true) return false;
        if (PendingSummonResponse is not { } response || UsesQueuedSummonResponse(response)) return false;
        PendingSummonResponse = null;
        BeginNextQueuedDeckSearch();
        message = "Summon response passed.";
        return true;
    }

    internal bool ActivateSummonTrap(int owner, int zone, out string message)
    {
        message = "Choose an eligible set Trap during a summon response.";
        if (primitiveActionQueue?.IsBusy == true) return false;
        if (PendingSummonResponse is not DemoSummonResponse response || UsesQueuedSummonResponse(response) ||
            owner is < 0 or > 1 || zone is < 0 or >= 5 ||
            Spells[owner, zone] is not Card trap || !IsEligibleSummonTrap(response, owner, zone, trap))
            return false;

        var e = response.Summon;
        if (trap.Id == SummonTrapRules.TorrentialTribute &&
            MonsterWipeRules.TargetsForScope(MonsterWipeScope.BothPlayers,
                owner, Monsters, MonsterFaceDown).Count == 0)
            return false;

        // Consumed exactly once; no chain or subsequent trap activation
        // is permitted for this response in the bounded sandbox.
        Spells[owner, zone] = null;
        SpellFaceDown[owner, zone] = false;
        spellSetTurn[owner, zone] = 0;
        graves[owner].Add(trap);
        PendingSummonResponse = null;

        int destroyed = 0;
        if (trap.Id == SummonTrapRules.TrapHole)
        {
            RemoveMonster(e.Player, e.Zone);
            destroyed = 1;
        }
        else
        {
            var wipe = ResolveMonsterWipe(trap.Id, owner, MonsterWipeScope.BothPlayers);
            LastMonsterWipe = wipe;
            destroyed = wipe.TotalDestroyed;
        }

        LastSummonTrap = new(trap.Id, owner, e.Player, e.Kind, destroyed);
        BeginNextQueuedDeckSearch();
        message = $"{trap.Name} activated in response to {e.Kind} Summon; destroyed {destroyed} monster(s).";
        return true;
    }

    private void QueueRitualSummonSearch(int owner, Card card)
    {
        if (RitualSearchRules.OnNormalOrFlipSummon(card.Id) is null) return;
        queuedDeckSearches.Enqueue((owner, card, false, false, true));
        BeginNextQueuedDeckSearch();
    }

    private void BeginNextQueuedDeckSearch()
    {
        if (PendingCardSelection is not null || PendingFlipTarget is not null ||
            PendingFlipSpellTrapTargets is not null || PendingSummonResponse is not null || PendingAttackResponse is not null ||
            resolvingSummon || GameOver)
            return;
        BeginNextEffectDecision();
        if (PendingEffectDecision is not null) return;
        while (queuedDeckSearches.Count > 0)
        {
            var (owner, source, destroyedByBattle, graveyardRevival,
                normalOrFlipSummon) = queuedDeckSearches.Dequeue();

            if (normalOrFlipSummon)
            {
                var summonFilter = RitualSearchRules.OnNormalOrFlipSummon(source.Id);
                if (summonFilter is null) continue;
                var ritualCandidates = CardSelectionRules.Candidates(
                    decks[owner].ToArray(), summonFilter.Value);
                if (ritualCandidates.Count == 0) continue;
                // The search is optional in the historical card text.
                // There is no effect or shuffle when declined or no targets exist.
                selectedBatchIndices.Clear();
                PendingCardSelection = new(owner, CardSelectionSource.Deck,
                    CardSelectionDestination.Hand, summonFilter.Value, ritualCandidates,
                    0, 1, source.Id);
                return;
            }

            int empty = Enumerable.Range(0, 5).Count(z => Monsters[owner, z] is null);
            if (graveyardRevival)
            {
                if (empty == 0) continue;
                // Spear Cretin never resurrects its very same card occurrence.
                var monsters = CardSelectionRules.Candidates(
                    graves[owner], CardSelectionFilter.AnyMonster)
                    .Where(c => !ReferenceEquals(c.Card, source) && !NormalSummonRules.IsFlipOnlySummon(c.Card)).ToArray();
                if (monsters.Length == 0) continue;
                selectedBatchIndices.Clear();
                PendingCardSelection = new(owner, CardSelectionSource.Graveyard,
                    CardSelectionDestination.Field, CardSelectionFilter.AnyMonster,
                    monsters, 1, 1, CardSelectionRules.SpearCretin,
                    MaySelectFaceDownDefense: true);
                return;
            }

            var searchFilter = CardSelectionRules.OnFieldToGraveyardFilter(source.Id);
            byte? attribute = destroyedByBattle
                ? CardSelectionRules.BattleRecruitAttribute(source.Id) : null;
            bool sameName = destroyedByBattle &&
                CardSelectionRules.BattleSameNameRecruiter(source.Id);
            CardSelectionFilter filter;
            CardSelectionDestination destination;
            int minimum;
            ushort? requiredId = null;
            CardSelectionPosition summonPosition = CardSelectionPosition.FaceUpAttack;
            if (searchFilter is not null)
            {
                filter = searchFilter.Value;
                destination = CardSelectionDestination.Hand;
                minimum = 1;
            }
            else if (attribute is not null && empty > 0)
            {
                filter = CardSelectionFilter.MonsterAttributeAttackAtMost1500;
                destination = CardSelectionDestination.Field;
                minimum = 0;
            }
            else if (sameName && empty > 0)
            {
                filter = CardSelectionFilter.SameNameMonster;
                destination = CardSelectionDestination.Field;
                minimum = 0;
                requiredId = source.Id;
                if (source.Id == CardSelectionRules.NimbleMomonga)
                    summonPosition = CardSelectionPosition.FaceDownDefense;
            }
            else continue;

            var eligible = CardSelectionRules.Candidates(decks[owner].ToArray(),
                filter, attribute, requiredId);
            if (eligible.Count == 0) continue;
            int maximum = sameName ? Math.Min(2, Math.Min(empty, eligible.Count)) : 1;
            if (maximum == 0) continue;
            selectedBatchIndices.Clear();
            PendingCardSelection = new(owner, CardSelectionSource.Deck, destination,
                filter, eligible, minimum, maximum, source.Id, attribute,
                RequiredResourceId: requiredId, SummonPosition: summonPosition);
            return;
        }
    }

    /// <summary>
    /// Select a FLIP target. Penguin Soldier accepts 0-2 optional targets;
    /// Hane-Hane requires exactly one if any target is eligible. A monster
    /// already marked for battle destruction cannot be returned to hand.
    /// </summary>
    internal bool ResolveFlipTarget(int targetPlayer, int targetZone, out string message)
    {
        message = "Choose an eligible monster in either monster zone.";
        if (primitiveActionQueue?.IsBusy == true || HasQueuedBattleTargetChoice ||
            PendingFlipTarget is { Effect: DemoFlipEffect.ChangeFaceUpPosition or DemoFlipEffect.PreventMonsterAttack } ||
            PendingFlipTarget is { Trigger: DemoMonsterEffectTrigger.PositionChange } position &&
                UsesQueuedMonsterFlipSelection(position)) return false;
        if (TryNegatePendingFlip(PendingFlipChoice.MonsterTarget, out message)) return true;
        if (PendingFlipTarget is not DemoFlipTarget pending ||
            targetPlayer is < 0 or > 1 || targetZone is < 0 or >= 5 ||
            Monsters[targetPlayer, targetZone] is not Card target) return false;

        if (pending.Effect == DemoFlipEffect.DestroyOpponentMonster && targetPlayer == pending.Owner)
        {
            message = "Select an opposing monster for this position-triggered effect.";
            return false;
        }
        if (pending.Effect is DemoFlipEffect.ReturnUpToTwo or DemoFlipEffect.ReturnOne)
        {
            if (IsMarkedForBattleDestruction(targetPlayer, targetZone))
            {
                message = "Battle-destroyed monsters cannot return to hand.";
                return false;
            }
            if (flipSelections.Any(x => x.Player == targetPlayer && x.Zone == targetZone))
            {
                message = "Choose distinct targets.";
                return false;
            }
            flipSelections.Add(new(targetPlayer, targetZone, target));
            if (pending.Effect == DemoFlipEffect.ReturnOne)
                return CompleteReturnTargets(out message);
            message = $"{pending.SourceCard.Name}: selected {flipSelections.Count}/2 target(s). Choose another or Done.";
            if (flipSelections.Count == 2)
                return FinishFlipTargets(out message);
            return true;
        }

        PendingFlipTarget = null;
        RemoveMonster(targetPlayer, targetZone);
        message = pending.Trigger == DemoMonsterEffectTrigger.PositionChange
            ? $"{pending.SourceCard.Name}: destroyed {target.Name} after changing position."
            : $"{pending.SourceCard.Name} FLIP: destroyed {target.Name}.";
        return true;
    }

    /// <summary>Finish Penguin Soldier with zero or one selected target.</summary>
    internal bool FinishFlipTargets(out string message)
    {
        message = "No optional FLIP target selection is pending.";
        if (primitiveActionQueue?.IsBusy == true || HasQueuedBattleTargetChoice) return false;
        if (PendingFlipTarget is not { Effect: DemoFlipEffect.ReturnUpToTwo }) return false;
        return CompleteReturnTargets(out message);
    }

    private bool CompleteReturnTargets(out string message)
    {
        message = "No return-target FLIP effect is pending.";
        if (TryNegatePendingFlip(PendingFlipChoice.MonsterTarget, out message)) return true;
        if (PendingFlipTarget is not { Effect: DemoFlipEffect.ReturnOne or DemoFlipEffect.ReturnUpToTwo } pending)
            return false;
        if (pending.Effect == DemoFlipEffect.ReturnOne && flipSelections.Count != 1)
            return false;
        // Clear the pending interaction before cleaning up equipped monsters.
        var targets = flipSelections.ToArray();
        flipSelections.Clear();
        PendingFlipTarget = null;
        int returned = 0;
        foreach (var target in targets)
        {
            if (ReferenceEquals(Monsters[target.Player, target.Zone], target.Card) &&
                !IsMarkedForBattleDestruction(target.Player, target.Zone))
            {
                ReturnMonsterToHand(target.Player, target.Zone);
                returned++;
            }
        }
        message = $"{pending.SourceCard.Name} FLIP: returned {returned} monster(s).";
        return true;
    }

    /// <summary>
    /// Destroy a snapshot of all matching monster occurrences. Processing
    /// is all-at-once from the player's perspective: deferred graveyard
    /// triggers cannot prompt midway through the wipe.
    /// </summary>
    private MonsterWipeResult ResolveMonsterWipe(ushort resourceId, int caster,
        MonsterWipeScope? overrideScope = null,
        IReadOnlyList<MonsterWipeTarget>? explicitTargets = null)
    {
        var targets = explicitTargets ?? (overrideScope is MonsterWipeScope scope
            ? MonsterWipeRules.TargetsForScope(scope, caster, Monsters, MonsterFaceDown)
            : MonsterWipeRules.Targets(resourceId, caster, Monsters, MonsterFaceDown));
        int[] counts = [0, 0];
        int faceDownDestroyed = 0;
        deferringFieldRemovalTriggers = true;
        try
        {
            foreach (var target in targets)
            {
                if (!ReferenceEquals(Monsters[target.Player, target.Zone], target.Card))
                    continue;
                RemoveMonster(target.Player, target.Zone);
                counts[target.Player]++;
                if (target.FaceDown) faceDownDestroyed++;
            }
        }
        finally { deferringFieldRemovalTriggers = false; }

        // Includes Sangan/Witch and previously flipped Spear Cretin.
        // P164/P168 do not fire: this was NOT battle destruction.
        BeginNextQueuedDeckSearch();
        return new(resourceId, caster, counts[0], counts[1], faceDownDestroyed);
    }

    private void RemoveMonster(int p, int z, bool destroyedByBattle = false, bool isTribute = false,
        bool isFusionMaterial = false, bool queueLinkedConsequences = false,
        RemovalCause? removalCause = null)
    {
        Card removed = Monsters[p, z]!;
        bool faceDown = MonsterFaceDown[p, z];
        bool defense = DefensePosition[p, z];
        bool previouslyFlipped = MonsterWasFlipped[p, z];
        graves[p].Add(removed);
        ClearMonsterSlot(p, z, removeEquipment: !queueLinkedConsequences);

        // A separate sent-from-field-to-Graveyard effect. The legacy P065
        // description conditions this on the same card having been flipped.
        // Never apply it to a normal face-up summon, an unflipped Set card,
        // bounce to hand, or Fusion Gate's banishment.
        if (!queueLinkedConsequences && ImmortalThunderRules.LosesLifeOnFieldToGraveyard(removed.Id, previouslyFlipped))
            ChangeLife(p, -ImmortalThunderRules.GraveyardLifeLoss);

        if (destroyedByBattle && CardSelectionRules.BattleSameNameLifeChange(removed.Id, p) is { } lifeChange)
            ChangeLife(lifeChange.Side, lifeChange.Delta);

        if (CardSelectionRules.OnFieldToGraveyardFilter(removed.Id) is not null ||
            destroyedByBattle && (CardSelectionRules.BattleRecruitAttribute(removed.Id) is not null ||
                                  CardSelectionRules.BattleSameNameRecruiter(removed.Id)))
            queuedDeckSearches.Enqueue((p, removed, destroyedByBattle, false, false));

        QueueSpearCretinRevival(p, removed, previouslyFlipped, destroyedByBattle);
        if (destroyedByBattle && removed.Id == RevivalJamRules.ResourceId &&
            !GameOver && RevivalJamRules.MayPay(Life[p]))
        {
            // Joey's exact optional-prompt routing (human vs demo/AI) is
            // still under native investigation. This is sandbox policy.
            queuedEffectDecisions.Enqueue(new(++nextEffectDecisionId, p, removed,
                EffectDecisionKind.PayLifePointsForRevival,
                RevivalJamRules.LifePointCost, Turn));
        }

        if (!deferringFieldRemovalTriggers)
        {
            BeginNextEffectDecision();
            BeginNextQueuedDeckSearch();
        }
        if (isTribute)
            MonsterTributed?.Invoke(new(p, z, removed, faceDown, defense)
                { Cause = removalCause ?? RemovalCause.Tribute });
        else if (!isFusionMaterial)
            MonsterDestroyed?.Invoke(new(p, z, removed, faceDown, defense)
                { Cause = destroyedByBattle ? RemovalCause.BattleDestruction :
                    removalCause ?? RemovalCause.EffectDestruction });
    }

    private void ReturnMonsterToHand(int player, int zone, bool removeEquipment = true)
    {
        Card card = Monsters[player, zone]!;
        ClearMonsterSlot(player, zone, removeEquipment);
        if (FusionRules.IsFusionMonster(card)) fusionDecks[player].Add(card);
        else
        {
            hands[player].Add(card);
            CheckExodia(player);
        }
    }

    private void ClearMonsterSlot(int player, int zone, bool removeEquipment = true)
    {
        if (Monsters[player, zone] is Card departing)
        {
            temporaryStats.Remove(departing);
            persistentMonsterStats.Remove(departing);
            positionLockedThroughTurn.Remove(departing);
            ClearMonsterAttackRestrictions(departing);
        }
        Monsters[player, zone] = null;
        DefensePosition[player, zone] = MonsterFaceDown[player, zone] = false;
        MonsterWasFlipped[player, zone] = false;
        changedPosition[player, zone] = false;
        attackDeclarations[player, zone] = 0;
        // Attached equips go to their owners' Graveyards when the monster leaves.
        if (removeEquipment)
            foreach (var e in equipment.Where(e => e.TargetPlayer == player && e.TargetZone == zone).ToArray())
                RemoveEquipmentCore(e.Owner, e.SourceZone);
    }
    private void Damage(int p, int value) => ChangeLife(p, -value);
    private void AdvanceTurn()
    {
        ExpireDrawPhaseSkipOnTurnExit();
        temporaryStats.Clear();
        Turn++; Phase = DemoPhase.Main; summoned = drawn = false;
        battlePhaseEndedThisTurn = false;
        firstBattleAttacker = null;
        Array.Clear(attackDeclarations); Array.Clear(changedPosition);
        // A Standby opportunity follows the turn-draw opportunity (including a skipped Draw Phase); see
        // ExecuteQueuedTurnDraw, not the turn increment itself.
    }

    private void BeginNextEffectDecision()
    {
        if (PendingEffectDecision is not null || PendingCardSelection is not null ||
            PendingFlipTarget is not null || PendingFlipSpellTrapTargets is not null ||
            PendingSummonResponse is not null ||
            PendingAttackResponse is not null || GameOver)
            return;
        if (queuedEffectDecisions.TryDequeue(out var request))
            PendingEffectDecision = request;
    }

    /// <summary>
    /// Consumes one optional cost request by stable ID; no UI dependency and no
    /// mutation on a stale request or invalid payment.
    /// </summary>
    internal bool ResolveEffectDecision(long id, EffectDecisionAnswer answer, out string message)
    {
        message = "No matching optional effect decision is pending.";
        if (primitiveActionQueue?.IsBusy == true) return false;
        if (PendingEffectDecision is not { } request || request.Id != id ||
            !Enum.IsDefined(answer) || GameOver) return false;
        if (TryNegatePendingFlip(PendingFlipChoice.OptionalDraw, out message)) return true;
        if (request.Kind == EffectDecisionKind.DrawOneOnFlip)
            return ResolveFlipDrawDecision(request, answer, out message);
        if (answer == EffectDecisionAnswer.Accept &&
            (!RevivalJamRules.MayPay(Life[request.Owner]) ||
             !graves[request.Owner].Any(c => ReferenceEquals(c, request.Source))))
        {
            message = "Cannot pay: the source left the graveyard or LP are insufficient.";
            return false;
        }

        int before = Life[request.Owner];
        PendingEffectDecision = null;
        if (answer == EffectDecisionAnswer.Accept)
        {
            ChangeLife(request.Owner, -request.LifePointCost);
            int dueTurn = Turn + (ActivePlayer == request.Owner ? 2 : 1);
            delayedRevivals.Add(new(request.Id, request.Owner, request.Source, dueTurn));
        }
        effectDecisionHistory.Add(new(request.Id, request.Owner, request.Kind,
            answer, Turn, before, Life[request.Owner]));
        message = answer == EffectDecisionAnswer.Accept
            ? $"P{request.Owner + 1} paid {request.LifePointCost} LP; Revival Jam scheduled."
            : $"P{request.Owner + 1} declined Revival Jam's optional cost.";
        BeginNextEffectDecision();
        BeginNextQueuedDeckSearch();
        return true;
    }

    private bool ResolveFlipDrawDecision(PendingEffectDecision request, EffectDecisionAnswer answer, out string message)
    {
        bool draw = answer == EffectDecisionAnswer.Accept;
        if (draw && decks[request.Owner].Count == 0)
        {
            message = "The deck is empty.";
            return false;
        }
        PendingEffectDecision = null;
        int life = Life[request.Owner];
        effectDecisionHistory.Add(new(request.Id, request.Owner, request.Kind, answer, Turn, life, life));
        if (draw) DrawToHand(request.Owner);
        message = draw ? $"P{request.Owner + 1} drew one card with {request.Source.Name}." :
            $"P{request.Owner + 1} declined the draw from {request.Source.Name}.";
        BeginNextQueuedDeckSearch();
        return true;
    }
}
