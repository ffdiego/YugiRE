namespace Joey.Core;

// These are logical operations, not native opcode numbers. Admission (player,
// CPU, effect, lab) is separate from execution; a queue accepts admitted commands.
public enum ActionOrigin { Player, Cpu, Effect, Console, Test }
public enum DuelZone { Deck, Hand, Monster, SpellTrap, FieldSpell, Graveyard, Banished, FusionDeck }

// The state adapter owns allocation and resolution of occurrence IDs. Never use
// resource ID, catalogue object reference, or current slot as an occurrence ID.
public readonly record struct CardOccurrenceId(long Value);
public readonly record struct CardTarget(CardOccurrenceId Occurrence, int Side, DuelZone Zone);

public abstract record DuelCommand(int Side);
public sealed record DeclareDirectAttackCommand(int Side, CardTarget Attacker) : DuelCommand(Side);
public sealed record DeclareTargetedAttackCommand(int Side, CardTarget Attacker, CardTarget Defender) : DuelCommand(Side);
internal sealed record RevealBattleDefenderCommand(int Side, CardTarget Attacker, CardTarget Defender) : DuelCommand(Side);
internal sealed record ResolveRevealedBattleCommand(int Side, CardTarget Attacker, CardTarget Defender) : DuelCommand(Side);
internal sealed record OpenBattleFlipDrawCommand(int Side, CardTarget Defender) : DuelCommand(Side);
internal sealed record OpenBattleFlipRecoveryCommand(int Side, CardTarget Defender) : DuelCommand(Side);
internal sealed record OpenBattleFlipDeckFlowCommand(int Side, CardTarget Defender) : DuelCommand(Side);
internal sealed record ResolveBattleFlipDamageCommand(int Side, CardTarget Defender) : DuelCommand(Side);
internal sealed record OpenBattleFlipTargetsCommand(int Side, CardTarget Defender) : DuelCommand(Side);
internal sealed record FinishBattleFlipCommand(int Side, CardTarget Defender) : DuelCommand(Side);
internal sealed record SnapshotBattleRemovalCommand(int Side, CardTarget Target, int Role) : DuelCommand(Side);
internal sealed record ResolveBattleDamageDrawCommand(int Side, BattleDamageEffectIntent Intent) : DuelCommand(Side);
internal sealed record ResolveBattleDamageDiscardCommand(int Side, int DamagedSide) : DuelCommand(Side);
internal sealed record FinishBattleDamageDrawCommand(int Side, BattleDamageEffectIntent Intent, int Drawn) : DuelCommand(Side);
internal sealed record OpenBattleRecruitmentCommand(int Side) : DuelCommand(Side);
internal sealed record FinishBattleRemovalCommand(int Side, CardTarget Target, int Role) : DuelCommand(Side);
internal sealed record ApplyBattleDestructionStatChangeCommand(int Side, CardTarget Survivor, CardTarget Destroyed) : DuelCommand(Side);
internal sealed record ResolveSlateWarriorFlipCommand(int Side, CardTarget Target) : DuelCommand(Side);
public sealed record ChangeBattlePhaseCommand(int Side, int ExpectedTurn, bool EnterBattle) : DuelCommand(Side);
internal sealed record FinishBattlePhaseCommand(int Side, int ExpectedTurn) : DuelCommand(Side);
internal sealed record GoblinAttackForceDefenseCommand(int Side, int ExpectedTurn, CardTarget Source) : DuelCommand(Side);
public sealed record EndTurnCommand(int Side, int ExpectedTurn) : DuelCommand(Side);
internal sealed record FinishEndPhaseReturnCommand(int Side, int ExpectedTurn, CardTarget Source) : DuelCommand(Side);
public sealed record EndTurnDiscardCommand(int Side, int ExpectedTurn, CardTarget Target) : DuelCommand(Side);
internal sealed record PlaceEndPhaseMessageCommand(int Side, int ExpectedTurn, CardTarget Source, int Slot) : DuelCommand(Side);
internal sealed record AdvanceTurnCommand(int Side, int ExpectedTurn) : DuelCommand(Side);
public sealed record TurnDrawCommand(int Side, int ExpectedTurn) : DuelCommand(Side);
internal sealed record ResolveStandbyRevivalCommand(int Side, int ExpectedTurn) : DuelCommand(Side);
internal sealed record StandbyDefenseCommand(int Side, int ExpectedTurn, CardTarget Source) : DuelCommand(Side);
public sealed record DrawCommand(int Side) : DuelCommand(Side); // exactly one card
internal sealed record FinishCardDestructionCommand(int Side, bool OpponentDeckOut) : DuelCommand(Side);
public sealed record SummonCommand(int Side, CardTarget Source, int Slot) : DuelCommand(Side);
public sealed record SetCommand(int Side, CardTarget Source, DuelZone Destination, int Slot) : DuelCommand(Side);
public sealed record SendToGraveyardCommand(int Side, CardTarget Target) : DuelCommand(Side);
public sealed record BanishCommand(int Side, CardTarget Target) : DuelCommand(Side);
public sealed record DiscardCommand(int Side, CardTarget Target) : DuelCommand(Side);
public sealed record DestroyCommand(int Side, CardTarget Target) : DuelCommand(Side);
public sealed record DamageCommand(int Side, int Amount) : DuelCommand(Side)
{
    public int? AffectedCombatantSlot { get; internal init; }
    internal bool DeferDuelResult { get; init; }
}
internal sealed record FinishJointDamageCommand(int Side) : DuelCommand(Side);
public sealed record HealCommand(int Side, int Amount) : DuelCommand(Side);
public sealed record FlipCommand(int Side, CardTarget Target) : DuelCommand(Side);
public sealed record ChangePositionCommand(int Side, CardTarget Target, bool Defense) : DuelCommand(Side);
internal sealed record ChangePositionByFlipCommand(int Side, CardTarget Target, bool Defense,
    bool FromPositionSpell = false) : DuelCommand(Side);
public sealed record SpecialSummonCommand(int Side, CardTarget Source, int Slot, bool FaceDownDefense = false, bool FaceUpDefense = false) : DuelCommand(Side);
public sealed record ShuffleDeckCommand(int Side) : DuelCommand(Side);
public sealed record MoveToHandCommand(int Side, CardTarget Source) : DuelCommand(Side);
public sealed record ReturnToDeckCommand(int Side, CardTarget Target) : DuelCommand(Side);
public sealed record ReturnToHandCommand(int Side, CardTarget Target) : DuelCommand(Side);
public sealed record EquipCommand(int Side, CardTarget Source, CardTarget Target, int SpellSlot) : DuelCommand(Side);

// Explicit setup edits, separate from gameplay commands.
internal sealed record LabSpawnCommand(int Side, Card Template, DuelZone Destination, int Slot, bool FaceDown, bool Defense) : DuelCommand(Side);
internal sealed record LabMoveCommand(int Side, CardTarget Source, DuelZone Destination, int Slot, bool FaceDown, bool Defense) : DuelCommand(Side);

// Pilot orchestration only; A08 moves card-specific producers out of Core.
// Neither of these stages claims a one-to-one native opcode mapping.
internal sealed record PrepareFusionSummonCommand(int Side, PendingFusionSummon Request, int DeckIndex,
    IReadOnlyList<FusionMaterial> Materials, CardTarget Source, CardTarget Result,
    IReadOnlyList<CardTarget> Payments, int Slot, bool Defense) : DuelCommand(Side);
internal sealed record FinishFusionSummonCommand(int Side) : DuelCommand(Side);
internal sealed record PrepareRitualSummonCommand(int Side, CardTarget Spell, CardTarget Monster,
    int Slot, bool Defense, IReadOnlyList<CardTarget> Materials) : DuelCommand(Side);
internal sealed record FinishRitualSummonCommand(int Side) : DuelCommand(Side);
internal sealed record PrepareGraveyardBanishSummonCommand(int Side, CardTarget Source, int Slot, bool Defense, CardTarget First, CardTarget? Second, CardTarget? Third) : DuelCommand(Side);
internal sealed record FinishGraveyardBanishSummonCommand(int Side) : DuelCommand(Side);
internal sealed record ResolveSameNameRecruitmentCommand(int Side, PendingCardSelection Request, CardTarget? First, CardTarget? Second, int? SingleSlot) : DuelCommand(Side);
internal sealed record ResolveDeckRecruitmentCommand(int Side, PendingCardSelection Request, CardTarget? Target, int? Slot) : DuelCommand(Side);
internal sealed record ResolveDeckSearchCommand(int Side, PendingCardSelection Request, CardTarget? Target) : DuelCommand(Side);
internal sealed record FinishDeckSearchCommand(int Side) : DuelCommand(Side);
internal sealed record ResolveGraveyardRevivalCommand(int Side, PendingCardSelection Request, CardTarget Target, int Slot, bool FaceDownDefense) : DuelCommand(Side);
internal sealed record ResolveGraveyardRecoveryCommand(int Side, PendingCardSelection Request, CardTarget? Target) : DuelCommand(Side);
internal sealed record FinishGraveyardRecoveryCommand(int Side) : DuelCommand(Side);
internal sealed record ResolveFlipSpellTrapPairCommand(int Side, PendingFlipSpellTrapTargets Request, CardTarget? First, CardTarget? Second) : DuelCommand(Side);
internal sealed record ResolveMonsterFlipTargetsCommand(int Side, CardTarget Source, CardTarget? First, CardTarget? Second) : DuelCommand(Side);
internal sealed record ResolveMonsterPositionTargetCommand(int Side, CardTarget Source, CardTarget Target) : DuelCommand(Side);
internal sealed record ResolveMonsterSummonTargetCommand(int Side, CardTarget Source, CardTarget Target) : DuelCommand(Side);
internal sealed record BindMonsterAttackCommand(int Side, CardTarget Source, CardTarget Target) : DuelCommand(Side);
internal sealed record ActivateTemporaryStatCommand(int Side, CardTarget Source, CardTarget Target,
    int ExpectedTurn) : DuelCommand(Side);
internal sealed record ResolveTemporaryStatCommand(int Side, CardTarget Source, CardTarget Target) : DuelCommand(Side);
internal sealed record ActivateDrawSpellCommand(int Side, CardTarget Source) : DuelCommand(Side);
internal sealed record ActivateDeckSearchSpellCommand(int Side, CardTarget Source) : DuelCommand(Side);
internal sealed record OpenDeckSearchSpellCommand(int Side, CardTarget Source, ushort ResourceId) : DuelCommand(Side);
internal sealed record ActivateLifePointSpellCommand(int Side, CardTarget Source) : DuelCommand(Side);
internal sealed record ActivateTargetedSpellTrapCommand(int Side, CardTarget Source, CardTarget Target) : DuelCommand(Side);
internal sealed record ActivateSpellTrapWipeCommand(int Side, CardTarget Source) : DuelCommand(Side);
internal sealed record PrepareFieldSpellActivationCommand(int Side, CardTarget Source) : DuelCommand(Side);
internal sealed record ActivateFieldSpellCommand(int Side, CardTarget Source) : DuelCommand(Side);
internal sealed record ActivateContinuousSpellCommand(int Side, CardTarget Source, int Slot) : DuelCommand(Side);
internal sealed record ActivateFinalDestinyCommand(int Side, CardTarget Source, CardTarget First,
    CardTarget Second, CardTarget Third, CardTarget Fourth, CardTarget Fifth) : DuelCommand(Side)
{
    public CardTarget[] Costs => [First,Second,Third,Fourth,Fifth];
}
internal sealed record ResolveFinalDestinyCommand(int Side, CardTarget Source) : DuelCommand(Side);
internal sealed record ActivateMonsterWipeCommand(int Side, CardTarget Source) : DuelCommand(Side);
internal sealed record ActivateDiscardMonsterSpellCommand(int Side, CardTarget Source, CardTarget Cost, CardTarget Target) : DuelCommand(Side);
internal sealed record ActivateConditionalMonsterSpellCommand(int Side, CardTarget Source, CardTarget Target) : DuelCommand(Side);
internal sealed record ResolveTargetedMonsterSpellCommand(int Side, CardTarget Source, CardTarget Target) : DuelCommand(Side);
internal sealed record ActivateRaigekiCommand(int Side, CardTarget Source) : DuelCommand(Side);
internal sealed record FinishFieldRemovalCommand(int Side) : DuelCommand(Side);
internal sealed record PrepareNormalPlacementCommand(int Side, CardTarget Source, int Slot, bool FaceDown,
    CardTarget? FirstTribute, CardTarget? SecondTribute) : DuelCommand(Side);
internal sealed record TributeCommand(int Side, CardTarget Target) : DuelCommand(Side);
internal sealed record FinishNormalPlacementCommand(int Side, CardTarget Target, bool FaceDown) : DuelCommand(Side);
internal sealed record OpenAttackResponseCommand(int Side, CardTarget Attacker, CardTarget? Defender, ActionOrigin DeclarationOrigin) : DuelCommand(Side);
internal sealed record ResolveAttackResponseCommand(int Side, DemoAttackResponse Request, CardTarget? Source) : DuelCommand(Side);
internal sealed record ResolveSpentAttackTrapCommand(int Side, CardTarget Source) : DuelCommand(Side);
internal sealed record ResolveSummonResponseCommand(int Side, DemoSummonResponse Request, CardTarget? Source) : DuelCommand(Side);
internal sealed record FinishSummonFlipDrawCommand(int Side) : DuelCommand(Side);
internal sealed record ResolvePositionCommand(int Side, CardTarget Target, bool WasFaceDown, bool WasInDefense) : DuelCommand(Side);
internal sealed record FinishFlipBatchCommand(int Side, CardTarget Target, bool WasInDefense) : DuelCommand(Side);
internal sealed record FinishMorphingJarCommand(int Side, bool DeckOut0, bool DeckOut1) : DuelCommand(Side);
internal sealed record FinishFlipDeckFlowCommand(int Side) : DuelCommand(Side);
internal sealed record RecordFlipDrawDecisionCommand(int Side, long DecisionId, EffectDecisionAnswer Answer) : DuelCommand(Side);

internal static class DuelCommandValidation
{
    public static void Validate(DuelCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Side is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(command), "Invalid side.");
        switch (command)
        {
            case ResolveBattleDamageDiscardCommand discard when discard.DamagedSide == 1 - discard.Side:
                break;
            case ResolveBattleDamageDrawCommand battleDraw when IsValidBattleDamageDraw(battleDraw.Intent, battleDraw.Side):
            case FinishBattleDamageDrawCommand finishDraw when IsValidBattleDamageDraw(finishDraw.Intent, finishDraw.Side) &&
                finishDraw.Drawn >= 0 && finishDraw.Drawn <= finishDraw.Intent.MaximumCount:
                break;
            case OpenAttackResponseCommand response:
                Target(response.Attacker, response.Side, DuelZone.Monster);
                if (response.Defender is { } attackDefender)
                    Target(attackDefender, 1 - response.Side, DuelZone.Monster);
                break;
            case ResolveAttackResponseCommand response when response.Request is not null:
                if (response.Source is { } attackTrap) Target(attackTrap, response.Side, DuelZone.SpellTrap);
                break;
            case ResolveSpentAttackTrapCommand response:
                Target(response.Source, response.Side, DuelZone.SpellTrap);
                break;
            case ResolveSummonResponseCommand response when response.Request is not null:
                if (response.Source is { } trapSource) Target(trapSource, response.Side, DuelZone.SpellTrap);
                break;
            case LabSpawnCommand spawn when spawn.Template is not null:
                LabDestination(spawn.Destination, spawn.Slot, spawn.FaceDown, spawn.Defense);
                break;
            case LabMoveCommand move:
                Target(move.Source, move.Side, DuelZone.Hand, DuelZone.Deck, DuelZone.Graveyard, DuelZone.Banished,
                    DuelZone.Monster, DuelZone.SpellTrap, DuelZone.FieldSpell);
                LabDestination(move.Destination, move.Slot, move.FaceDown, move.Defense);
                if (move.Source.Zone == move.Destination) throw new ArgumentException("Move requires distinct zones.");
                break;
            case DeclareDirectAttackCommand attack:
                Target(attack.Attacker, attack.Side, DuelZone.Monster);
                break;
            case DeclareTargetedAttackCommand attack:
                Target(attack.Attacker, attack.Side, DuelZone.Monster);
                Target(attack.Defender, 1-attack.Side, DuelZone.Monster);
                break;
            case RevealBattleDefenderCommand reveal:
                Target(reveal.Attacker,1-reveal.Side,DuelZone.Monster);
                Target(reveal.Defender,reveal.Side,DuelZone.Monster);
                break;
            case ResolveRevealedBattleCommand resolve:
                Target(resolve.Attacker,resolve.Side,DuelZone.Monster);
                Target(resolve.Defender,1-resolve.Side,DuelZone.Monster);
                break;
            case OpenBattleFlipDrawCommand openBattleFlip:
                Target(openBattleFlip.Defender,openBattleFlip.Side,DuelZone.Monster);
                break;
            case OpenBattleFlipRecoveryCommand recoveryFlip:
                Target(recoveryFlip.Defender, recoveryFlip.Side, DuelZone.Monster);
                break;
            case OpenBattleFlipDeckFlowCommand deckFlip:
                Target(deckFlip.Defender, deckFlip.Side, DuelZone.Monster);
                break;
            case ResolveBattleFlipDamageCommand damageFlip:
                Target(damageFlip.Defender, damageFlip.Side, DuelZone.Monster);
                break;
            case OpenBattleFlipTargetsCommand openBattleTargets:
                Target(openBattleTargets.Defender,openBattleTargets.Side,DuelZone.Monster);
                break;
            case FinishBattleFlipCommand finishBattleFlip:
                Target(finishBattleFlip.Defender,finishBattleFlip.Side,DuelZone.Monster);
                break;
            case SnapshotBattleRemovalCommand snapshot when snapshot.Role is 0 or 1:
                Target(snapshot.Target,snapshot.Side,DuelZone.Monster);
                break;
            case FinishBattleRemovalCommand finishBattle when finishBattle.Role is 0 or 1:
                Target(finishBattle.Target,finishBattle.Side,DuelZone.Monster);
                break;
            case ApplyBattleDestructionStatChangeCommand statChange:
                Target(statChange.Survivor, statChange.Side, DuelZone.Monster);
                Target(statChange.Destroyed, 1 - statChange.Side, DuelZone.Graveyard);
                break;
            case ResolveSlateWarriorFlipCommand slateFlip:
                Target(slateFlip.Target, slateFlip.Side, DuelZone.Monster);
                break;
            case GoblinAttackForceDefenseCommand forcedDefense when forcedDefense.ExpectedTurn > 0:
                Target(forcedDefense.Source, forcedDefense.Side, DuelZone.Monster);
                break;
            case RecordFlipDrawDecisionCommand record when record.DecisionId > 0 && Enum.IsDefined(record.Answer):
            case ChangeBattlePhaseCommand { ExpectedTurn: > 0 }:
            case FinishBattlePhaseCommand { ExpectedTurn: > 0 }:
            case EndTurnCommand { ExpectedTurn: > 0 }:
            case AdvanceTurnCommand { ExpectedTurn: > 0 }:
            case TurnDrawCommand { ExpectedTurn: > 0 }:
            case ResolveStandbyRevivalCommand { ExpectedTurn: > 0 }:
            case DrawCommand:
            case FinishFieldRemovalCommand:
            case FinishSummonFlipDrawCommand:
            case OpenBattleRecruitmentCommand:
                break;
            case StandbyDefenseCommand position when position.ExpectedTurn > 0:
                Target(position.Source, position.Side, DuelZone.Monster);
                break;
            case FinishEndPhaseReturnCommand endReturn when endReturn.ExpectedTurn > 0:
                Target(endReturn.Source, endReturn.Side, DuelZone.Monster);
                break;
            case ShuffleDeckCommand:
            case FinishDeckSearchCommand:
            case FinishGraveyardBanishSummonCommand:
            case FinishGraveyardRecoveryCommand:
            case FinishFusionSummonCommand:
            case FinishRitualSummonCommand:
                break;
            case SpecialSummonCommand summonFromDeck when summonFromDeck.Slot is >= 0 and < 5 && !(summonFromDeck.FaceDownDefense && summonFromDeck.FaceUpDefense):
                Target(summonFromDeck.Source, summonFromDeck.Side, DuelZone.Deck, DuelZone.Graveyard, DuelZone.Hand, DuelZone.FusionDeck);
                break;
            case PrepareFusionSummonCommand fusion when fusion.Slot is >= 0 and < 5 &&
                fusion.Request.Owner == fusion.Side && fusion.Payments.Count == fusion.Materials.Count &&
                fusion.Payments.Count > 0 && fusion.Payments.Distinct().Count() == fusion.Payments.Count:
                Target(fusion.Source, fusion.Request.FieldOwner ?? fusion.Side,
                    fusion.Request.FieldOwner is null ? (fusion.Request.FromSet ? DuelZone.SpellTrap : DuelZone.Hand) : DuelZone.FieldSpell);
                Target(fusion.Result, fusion.Side, DuelZone.FusionDeck);
                foreach (var payment in fusion.Payments) Target(payment, fusion.Side, DuelZone.Hand, DuelZone.Monster);
                break;
            case PrepareRitualSummonCommand ritual when ritual.Slot is >= 0 and < 5:
                Target(ritual.Spell, ritual.Side, DuelZone.Hand, DuelZone.SpellTrap);
                Target(ritual.Monster, ritual.Side, DuelZone.Hand);
                foreach (var material in ritual.Materials)
                    Target(material, ritual.Side, DuelZone.Hand, DuelZone.Monster);
                break;
            case PrepareGraveyardBanishSummonCommand prepareBanish when prepareBanish.Slot is >= 0 and < 5:
                Target(prepareBanish.Source, prepareBanish.Side, DuelZone.Hand);
                if (prepareBanish.Third is not null && prepareBanish.Second is null)
                    throw new ArgumentException("Missing cost occurrence.");
                var costCopies = new CardTarget?[] { prepareBanish.First, prepareBanish.Second, prepareBanish.Third }.OfType<CardTarget>().ToArray();
                if (costCopies.Distinct().Count() != costCopies.Length)
                    throw new ArgumentException("Duplicate cost occurrence.");
                foreach (var costCopy in costCopies) Target(costCopy, prepareBanish.Side, DuelZone.Graveyard);
                break;
            case ResolveSameNameRecruitmentCommand sameName when sameName.Request is not null && sameName.Request.Owner == sameName.Side:
                if (sameName.Second is not null && sameName.First is null ||
                    sameName.First is { } firstCopy && sameName.Second == firstCopy ||
                    sameName.SingleSlot is int chosenSlot && (chosenSlot is < 0 or >= 5 ||
                        sameName.First is null || sameName.Second is not null))
                    throw new ArgumentException("Invalid same-name recruitment choice.");
                foreach (var copy in new CardTarget?[] { sameName.First, sameName.Second }.OfType<CardTarget>())
                    Target(copy, sameName.Side, DuelZone.Deck);
                break;
            case ResolveDeckRecruitmentCommand recruit when recruit.Request is not null && recruit.Request.Owner == recruit.Side:
                if (recruit.Target is { } recruitPick && recruit.Slot is >= 0 and < 5)
                    Target(recruitPick, recruit.Side, DuelZone.Deck);
                else if (recruit.Target is not null || recruit.Slot is not null)
                    throw new ArgumentException("Invalid recruitment choice.");
                break;
            case MoveToHandCommand move:
                Target(move.Source, move.Side, DuelZone.Graveyard, DuelZone.Deck);
                break;
            case ResolveDeckSearchCommand search when search.Request is not null && search.Request.Owner == search.Side:
                if (search.Target is { } searchPick) Target(searchPick, search.Side, DuelZone.Deck);
                break;
            case ResolveGraveyardRevivalCommand revive when revive.Request is not null && revive.Request.Owner == revive.Side && revive.Slot is >= 0 and < 5:
                Target(revive.Target, revive.Side, DuelZone.Graveyard);
                break;
            case ResolveGraveyardRecoveryCommand recover when recover.Request is not null && recover.Request.Owner == recover.Side:
                if (recover.Target is { } selected) Target(selected, recover.Side, DuelZone.Graveyard);
                break;
            case ReturnToDeckCommand returnDeck:
                Target(returnDeck.Target, returnDeck.Side, DuelZone.Monster);
                break;
            case ReturnToHandCommand bounce:
                Target(bounce.Target, bounce.Side, DuelZone.Monster, DuelZone.SpellTrap, DuelZone.FieldSpell);
                break;
            case ResolveFlipSpellTrapPairCommand pair when pair.Request is not null && pair.Request.Owner == pair.Side:
                if ((pair.First is null) != (pair.Second is null) ||
                    pair.First is { } firstPair && pair.Second == firstPair)
                    throw new ArgumentException("Invalid FLIP Spell/Trap pair.");
                foreach (var pairPick in new CardTarget?[] { pair.First, pair.Second }.OfType<CardTarget>())
                    Target(pairPick, pairPick.Side, DuelZone.SpellTrap, DuelZone.FieldSpell);
                break;
            case ResolveMonsterPositionTargetCommand positionTarget:
                Target(positionTarget.Source, positionTarget.Side, DuelZone.Monster);
                Target(positionTarget.Target, 1 - positionTarget.Side, DuelZone.Monster);
                break;
            case ResolveMonsterSummonTargetCommand summonTarget:
                Target(summonTarget.Source, summonTarget.Side, DuelZone.Monster);
                Target(summonTarget.Target, 1 - summonTarget.Side, DuelZone.Monster);
                break;
            case BindMonsterAttackCommand restriction:
                Target(restriction.Source, restriction.Side, DuelZone.Monster);
                Target(restriction.Target, restriction.Target.Side, DuelZone.Monster);
                break;
            case ResolveMonsterFlipTargetsCommand flipTargets:
                Target(flipTargets.Source, flipTargets.Side, DuelZone.Monster);
                if (flipTargets.Second is not null && flipTargets.First is null ||
                    flipTargets.First is { } firstPick && flipTargets.Second == firstPick)
                    throw new ArgumentException("Invalid FLIP target list.");
                foreach (var pick in new CardTarget?[] { flipTargets.First, flipTargets.Second }.OfType<CardTarget>())
                {
                    if (pick.Side is not (0 or 1)) throw new ArgumentException("Invalid target side.");
                    Target(pick, pick.Side, DuelZone.Monster);
                }
                break;
            case EquipCommand equip when equip.SpellSlot is >= 0 and < 5:
                Target(equip.Source, equip.Side, DuelZone.Hand, DuelZone.SpellTrap);
                if (equip.Target.Side is not (0 or 1)) throw new ArgumentException("Invalid equipment target side.");
                Target(equip.Target, equip.Target.Side, DuelZone.Monster);
                break;
            case ActivateTemporaryStatCommand modifier when modifier.ExpectedTurn > 0:
                Target(modifier.Source, modifier.Side, DuelZone.Hand, DuelZone.SpellTrap);
                Target(modifier.Target, modifier.Target.Side, DuelZone.Monster);
                break;
            case ResolveTemporaryStatCommand modifier:
                Target(modifier.Source, modifier.Side, DuelZone.Hand, DuelZone.SpellTrap);
                Target(modifier.Target, modifier.Target.Side, DuelZone.Monster);
                break;
            case ActivateDrawSpellCommand drawSpell:
                Target(drawSpell.Source, drawSpell.Side, DuelZone.Hand, DuelZone.SpellTrap);
                break;
            case ActivateDeckSearchSpellCommand searchSpell:
                Target(searchSpell.Source, searchSpell.Side, DuelZone.Hand, DuelZone.SpellTrap);
                break;
            case OpenDeckSearchSpellCommand openSearch when DeckSearchSpellRules.Filter(openSearch.ResourceId) is not null:
                Target(openSearch.Source, openSearch.Side, DuelZone.Hand, DuelZone.SpellTrap);
                break;
            case ActivateLifePointSpellCommand lp:
                Target(lp.Source, lp.Side, DuelZone.Hand, DuelZone.SpellTrap);
                break;
            case ActivateTargetedSpellTrapCommand targeted:
                Target(targeted.Source, targeted.Side, DuelZone.Hand, DuelZone.SpellTrap);
                if (targeted.Target.Side is not (0 or 1) || targeted.Source == targeted.Target)
                    throw new ArgumentException("Invalid targeted Spell/Trap occurrence.");
                Target(targeted.Target, targeted.Target.Side, DuelZone.SpellTrap, DuelZone.FieldSpell);
                break;
            case ActivateFinalDestinyCommand finalDestiny:
                Target(finalDestiny.Source,finalDestiny.Side,DuelZone.Hand,DuelZone.SpellTrap);
                if(finalDestiny.Costs.Distinct().Count()!=5 || finalDestiny.Costs.Contains(finalDestiny.Source))
                    throw new ArgumentException("Invalid Final Destiny costs.");
                foreach(var payment in finalDestiny.Costs) Target(payment,finalDestiny.Side,DuelZone.Hand);
                break;
            case ResolveFinalDestinyCommand finalDestinyResolve:
                Target(finalDestinyResolve.Source,finalDestinyResolve.Side,DuelZone.Hand,DuelZone.SpellTrap);
                break;
            case PrepareFieldSpellActivationCommand fieldPrepare:
                Target(fieldPrepare.Source,fieldPrepare.Side,DuelZone.Hand,DuelZone.FieldSpell);
                break;
            case ActivateFieldSpellCommand fieldActivate:
                Target(fieldActivate.Source,fieldActivate.Side,DuelZone.Hand,DuelZone.FieldSpell);
                break;
            case ActivateContinuousSpellCommand continuous when continuous.Slot is >=0 and <5:
                Target(continuous.Source,continuous.Side,DuelZone.Hand,DuelZone.SpellTrap);
                break;
            case ActivateSpellTrapWipeCommand spellTrapWipe:
                Target(spellTrapWipe.Source, spellTrapWipe.Side, DuelZone.Hand, DuelZone.SpellTrap);
                break;
            case ActivateMonsterWipeCommand monsterWipe:
                Target(monsterWipe.Source, monsterWipe.Side, DuelZone.Hand, DuelZone.SpellTrap);
                break;
            case ActivateDiscardMonsterSpellCommand paidSpell when paidSpell.Source != paidSpell.Cost:
                Target(paidSpell.Source, paidSpell.Side, DuelZone.Hand, DuelZone.SpellTrap);
                Target(paidSpell.Cost, paidSpell.Side, DuelZone.Hand);
                Target(paidSpell.Target, paidSpell.Target.Side, DuelZone.Monster);
                break;
            case ActivateConditionalMonsterSpellCommand conditionalSpell:
                Target(conditionalSpell.Source, conditionalSpell.Side, DuelZone.Hand, DuelZone.SpellTrap);
                Target(conditionalSpell.Target, conditionalSpell.Target.Side, DuelZone.Monster);
                break;
            case ResolveTargetedMonsterSpellCommand paidResolution:
                Target(paidResolution.Source, paidResolution.Side, DuelZone.Hand, DuelZone.SpellTrap);
                Target(paidResolution.Target, paidResolution.Target.Side, DuelZone.Monster);
                break;
            case ActivateRaigekiCommand activate:
                Target(activate.Source, activate.Side, DuelZone.Hand);
                break;
            case PrepareNormalPlacementCommand prepare when prepare.Slot is >= 0 and < 5:
                Target(prepare.Source, prepare.Side, DuelZone.Hand);
                if (prepare.FirstTribute is { } first) Target(first, prepare.Side, DuelZone.Monster);
                if (prepare.SecondTribute is { } second) Target(second, prepare.Side, DuelZone.Monster);
                break;
            case TributeCommand tribute:
                Target(tribute.Target, tribute.Side, DuelZone.Hand, DuelZone.Monster);
                break;
            case FinishNormalPlacementCommand finish:
                Target(finish.Target, finish.Side, DuelZone.Monster);
                break;
            case ResolvePositionCommand position:
                Target(position.Target, position.Side, DuelZone.Monster);
                break;
            case FinishFlipBatchCommand jar:
                Target(jar.Target, jar.Side, DuelZone.Monster);
                break;
            case DamageCommand damage when damage.Amount > 0:
            case FinishJointDamageCommand:
            case FinishCardDestructionCommand:
            case FinishFlipDeckFlowCommand:
            case FinishMorphingJarCommand:
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
            case BanishCommand banish:
                Target(banish.Target, banish.Side, DuelZone.Deck, DuelZone.Hand, DuelZone.Graveyard, DuelZone.Monster);
                break;
            case PlaceEndPhaseMessageCommand message when message.ExpectedTurn > 0 && message.Slot is >= 0 and < 5:
                Target(message.Source, message.Side, DuelZone.Hand, DuelZone.Deck);
                break;
            case EndTurnDiscardCommand endDiscard when endDiscard.ExpectedTurn > 0:
                Target(endDiscard.Target, endDiscard.Side, DuelZone.Hand);
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
            case ChangePositionByFlipCommand forcedPosition:
                Target(forcedPosition.Target, forcedPosition.Side, DuelZone.Monster);
                break;
            default:
                throw new ArgumentException("Unknown command or invalid arguments.", nameof(command));
        }
    }

    private static void LabDestination(DuelZone zone, int slot, bool faceDown, bool defense)
    {
        bool valid = zone switch
        {
            DuelZone.Monster => slot is >= 0 and < 5 && (!faceDown || defense),
            DuelZone.SpellTrap => slot is >= 0 and < 5 && faceDown && !defense,
            DuelZone.FieldSpell => slot == 0 && faceDown && !defense,
            DuelZone.Hand or DuelZone.Deck or DuelZone.Graveyard or DuelZone.Banished => slot == -1 && !faceDown && !defense,
            _ => false
        };
        if (!valid) throw new ArgumentException("Invalid laboratory destination.");
    }

    private static bool IsValidBattleDamageDraw(BattleDamageEffectIntent intent, int side) =>
        intent.Controller == side &&
        BattleDamageEffectRules.AfterBattleDamage(intent.SourceResourceId, side, 1 - side, 1, false) is
            { Kind: BattleDamageEffectKind.Draw } draw && draw == intent;

    private static void Target(CardTarget target, int side, params DuelZone[] allowed)
    {
        if (target.Occurrence.Value <= 0 || target.Side != side || !allowed.Contains(target.Zone))
            throw new ArgumentException("Invalid occurrence, side or expected zone.", nameof(target));
    }
}
