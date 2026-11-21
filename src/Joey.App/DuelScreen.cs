using Joey.LookAndFeel;
using Joey.Core;
using Raylib_cs;
using System.Numerics;

static partial class Game
{
    // Native field centers and hand spacing; balloon rendering and update frequency are demo policy.
    static int hoverHand = -1;
    static bool setHandAction, choosingTributes, choosingEquip;
    static readonly int[] handLift = new int[6];
    static readonly PresentationTickClock hoverClock = new();
    static Rectangle HandBounds(int slot) => HandBoundsForCount(handPage * 6 + slot, ViewHand.Count, handLift[slot]);

    static Rectangle HandBoundsForCount(int index, int handCount, int lift = 0)
    {
        int count = Math.Min(6, handCount - index / 6 * 6);
        return new(HandPresentation.CenterX(index % 6, count) - HandPresentation.CardWidth / 2,
            600 - HandPresentation.CardHeight / 2 - lift, HandPresentation.CardWidth, HandPresentation.CardHeight);
    }
    static Rectangle ActionBounds(int slot)
    {
        var card = HandBounds(slot);
        return new(card.X, card.Y - 32, card.Width, 27);
    }
    static bool Hit(Rectangle rect) => Raylib.CheckCollisionPointRec(mouse, rect);
    static void ClearDuelSelection()
    {
        hand = attacker = hoverHand = -1;
        setHandAction = choosingTributes = choosingEquip = false;
        tributeZones.Clear();
        handPage = Math.Clamp(handPage, 0, Math.Max(0, (ViewHand.Count - 1) / 6));
        Array.Clear(handLift);
    }
    static void UseHandAction(int index)
    {
        if (index < 0 || index >= ViewHand.Count) return;
        hand = index; attacker = -1;
        var card = ViewHand[index];
        if (duel.PendingEndDiscards > 0)
        {
            if (duel.DiscardForEndTurn(index, out message)) ClearDuelSelection();
            return;
        }
        if (card.IsMonster)
        {
            if (!duel.CanSummonFromHand(index)) return;
            int needed = DemoDuel.NormalTributes(card);
            if (needed > 0)
            {
                tributeZones.Clear(); choosingTributes = true;
                message = $"Choose {needed} own monster(s) to tribute. Placement is automatic.";
                return;
            }
            CommitHumanSummon(index);
        }
        else if (setHandAction)
        {
            int zone = duel.FirstFreeSpellZone();
            var origin = HandBounds(index % 6);
            if (duel.SetFromHand(index, out message))
            {
                if (zone >= 0) BeginFlight(card, ViewPlayer, zone, origin, true, true);
                ClearDuelSelection();
            }
        }
        else if (EquipmentRules.Supports(card.Id) && duel.CanActivateFromHand(index))
        {
            choosingEquip = true;
            message = $"Choose a compatible face-up monster for {card.Name}.";
        }
        else
        {
            int zone = duel.FirstFreeSpellZone();
            var origin = HandBounds(index % 6);
            if (!duel.Play(index, out message)) return;
            if (FieldSpellRules.Supports(card.Id))
                BeginCardActivation(card, FieldZoneBounds(ViewPlayer), origin);
            else if (zone >= 0 && card.ClassCode == 22)
                BeginCardActivation(card, ZoneBounds(ViewPlayer, zone, true), origin);
            ClearDuelSelection();
        }
    }
    static bool CommitHumanSummon(int index)
    {
        var card = ViewHand[index];
        int zone = duel.FirstFreeMonsterZone(tributeZones);
        var origin = HandBounds(index % 6);
        if (!duel.SummonLeftmost(index, setHandAction, out message, tributeZones)) return false;
        BeginFlight(card, ViewPlayer, zone, origin, false, setHandAction);
        ClearDuelSelection();
        return true;
    }
    static string HandActionCaption(int index) => duel.PendingEndDiscards > 0 ? "Discard" :
        setHandAction ? "Set" : ViewHand[index].IsMonster ? "Summon" : "Activate";
    static bool HandActionEnabled(int index) => duel.PendingEndDiscards > 0 ||
        (setHandAction ? duel.CanSetFromHand(index) : ViewHand[index].IsMonster
            ? duel.CanSummonFromHand(index) : duel.CanActivateFromHand(index));
    static bool UpdateHandInteraction(int updates)
    {
        handPage = Math.Clamp(handPage, 0, Math.Max(0, (ViewHand.Count - 1) / 6));
        int count = Math.Min(6, ViewHand.Count - handPage * 6);
        int next = -1;
        for (int slot = 0; slot < count; slot++)
        {
            var rect = HandBounds(slot);
            // Include both rest and lifted positions so the card never escapes its hover area.
            if (Hit(new Rectangle(rect.X, rect.Y, rect.Width, 598 - rect.Y))) next = handPage * 6 + slot;
        }
        if (next < 0 && hoverHand / 6 == handPage && hoverHand >= 0 && hoverHand < ViewHand.Count)
        {
            var balloon = ActionBounds(hoverHand % 6);
            if (Hit(new Rectangle(balloon.X, balloon.Y, balloon.Width, balloon.Height + 6))) next = hoverHand;
        }
        bool overHand = next >= 0;
        if (choosingTributes || choosingEquip) next = -1;
        if (next != hoverHand)
        {
            hoverHand = next;
            if (!choosingTributes && !choosingEquip) setHandAction = false;
        }
        for (int slot = 0; slot < 6; slot++)
        {
            int target = hoverHand == handPage * 6 + slot ? HandPresentation.HoverOffset : 0;
            if (!presentation.Style.PlayAnimations) handLift[slot] = target;
            else
                for (int i = 0; i < updates && handLift[slot] != target; i++)
                    handLift[slot] = presentation.Style.MoveCardAxis(handLift[slot], target);
        }
        if (hoverHand < 0) return overHand;
        hand = hoverHand; attacker = -1;
        if (Raylib.IsMouseButtonPressed(MouseButton.Right) && duel.PendingEndDiscards == 0)
            setHandAction = !setHandAction;
        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && HandActionEnabled(hoverHand))
            UseHandAction(hoverHand);
        return true;
    }
    static void DrawCardBack(int x, int y, int width, int height, bool cropped = false, int degrees = 0)
    {
        bool sideways = degrees % 180 != 0;
        if (duelTheme.CardBack is Texture2D texture)
        {
            float sourceHeight = cropped ? Math.Min(texture.Height, height * (float)texture.Width / width) : texture.Height;
            Raylib.DrawTexturePro(texture,
                new Rectangle(0, texture.Height - sourceHeight, texture.Width, sourceHeight),
                new Rectangle(x + width / 2f, y + height / 2f, sideways ? height : width, sideways ? width : height),
                new Vector2(sideways ? height / 2f : width / 2f, sideways ? width / 2f : height / 2f),
                degrees, Color.White);
            return;
        }
        Raylib.DrawRectangle(x, y, width, height, new Color(57, 32, 20, 255));
        Raylib.DrawRectangleLines(x + 2, y + 2, width - 4, height - 4, Gold);
        Raylib.DrawEllipseLines(x + width / 2, y + height / 2, width / 3f, height / 3f, Gold);
    }
    static void DrawFieldCard(Card card, Rectangle frame, bool faceDown, bool defense = false, bool upsideDown = false) =>
        DrawDuelCard(card, DuelCardLayout.InTransit(ToCardBounds(frame), !upsideDown, defense, faceDown));

    static void DrawDuelCard(Card card, CardRenderGeometry geometry)
    {
        var (x, y, width, height) = geometry.Bounds;
        if (geometry.FaceDown) DrawCardBack(x, y, width, height, degrees: geometry.Degrees);
        else DrawArtwork(card, x, y, width, height, degrees: geometry.Degrees);
    }

    static void DrawDuelField(bool blockInput)
    {
        for (int player = 0; player < 2; player++)
        {
            bool own = player == ViewPlayer;
            // Dedicated field zone, independent of the five ordinary spell slots.
            var fieldRect = FieldZoneBounds(player);
            Raylib.DrawRectangleLinesEx(fieldRect, 1, own ? Gold : Color.Gray);
            Label("FIELD", (int)fieldRect.X + 7, (int)fieldRect.Y - 15, 11, Gold);
            if (duel.FieldSpells[player] is Card fieldCard)
            {
                if (!ActivationInFlight(fieldRect))
                    DrawDuelCard(fieldCard, DuelCardLayout.OnBoard(DuelCardZone.Field, own, 0, false, duel.FieldSpellFaceDown[player]));
                if (own && duel.FieldSpellFaceDown[player] && !blockInput &&
                    Raylib.IsMouseButtonPressed(MouseButton.Left) && Hit(fieldRect) &&
                    duel.ActivateSetFieldSpell(out message))
                    BeginCardActivation(fieldCard, fieldRect);
            }
            for (int zone = 0; zone < 5; zone++)
            {
                var monsterRect = ZoneBounds(player, zone, false);
                var spellRect = ZoneBounds(player, zone, true);
                var monster = duel.Monsters[player, zone];
                Raylib.DrawRectangleLinesEx(monsterRect, 1, own ? Gold : Color.Gray);
                Raylib.DrawRectangleLinesEx(spellRect, 1, own ? Gold : Color.Gray);
                if (monster != null && !InFlight(player, zone, false))
                {
                    var geometry = DuelCardLayout.OnBoard(DuelCardZone.Monster, own, zone,
                        duel.DefensePosition[player, zone], duel.MonsterFaceDown[player, zone]);
                    DrawDuelCard(monster, geometry);
                    if (geometry.AttributesCenter is { } center)
                    {
                        const int fontSize = 10;
                        string attributes = $"{duel.AttackValue(player, zone)} / {duel.DefenseValue(player, zone)}";
                        Label(attributes, center.X - MeasureLabel(attributes, fontSize) / 2, center.Y - fontSize / 2, fontSize);
                    }
                }
                if (own && (attacker == zone || tributeZones.Contains(zone)))
                    Raylib.DrawRectangleLinesEx(monsterRect, 3, tributeZones.Contains(zone) ? Color.Red : Color.White);
                if (duel.SelectedFlipTargets.Any(t => t.Player == player && t.Zone == zone))
                    Raylib.DrawRectangleLinesEx(monsterRect, 4, Color.SkyBlue);
                if (duel.PendingFlipTarget is
                    { Effect: DemoFlipEffect.ReturnUpToTwo or DemoFlipEffect.ReturnOne } &&
                    duel.IsMarkedForBattleDestruction(player, zone))
                    Raylib.DrawRectangleLinesEx(monsterRect, 3, Color.Red);
                if (duel.Spells[player, zone] is Card spell && !InFlight(player, zone, true) && !ActivationInFlight(spellRect))
                    DrawDuelCard(spell, DuelCardLayout.OnBoard(DuelCardZone.Spell, own, zone, false, duel.SpellFaceDown[player, zone]));
                if (!blockInput && own && duel.CanAttack(zone) && attacker != zone)
                    DrawAttackSword(new(monsterRect.X + monsterRect.Width / 2, monsterRect.Y + 26), new(0, -1));
                if (blockInput || !Raylib.IsMouseButtonPressed(MouseButton.Left)) continue;
                if (Hit(monsterRect))
                {
                    if (duel.PendingFlipTarget is not null)
                    {
                        if (monster is not null && duel.ResolveFlipTarget(player, zone, out message))
                            ClearDuelSelection();
                    }
                    else if (choosingTributes && own && monster != null)
                    {
                        if (!tributeZones.Remove(zone)) tributeZones.Add(zone);
                        int needed = DemoDuel.NormalTributes(ViewHand[hand]);
                        if (tributeZones.Count != needed || !CommitHumanSummon(hand))
                            message = $"Choose {needed - tributeZones.Count} more tribute(s).";
                    }
                    else if (choosingEquip && monster != null)
                    {
                        var equip = ViewHand[hand];
                        int sourceZone = duel.FirstFreeSpellZone();
                        var origin = HandBounds(hand % 6);
                        if (duel.Equip(hand, player, zone, out message))
                        {
                            BeginCardActivation(equip, ZoneBounds(ViewPlayer, sourceZone, true), origin);
                            ClearDuelSelection();
                        }
                    }
                    else if (duel.Phase == DemoPhase.Battle)
                    {
                        if (own && duel.CanAttack(zone))
                        {
                            attacker = zone;
                            hand = -1;
                            message = "Choose an enemy target.";
                            if (!Enumerable.Range(0, 5).Any(z => duel.Monsters[1 - player, z] is not null))
                                CommitHumanAttack(-1);
                        }
                        else if (!own && monster is not null) CommitHumanAttack(zone);
                    }
                    else if (own && monster != null)
                    {
                        bool reveal = duel.MonsterFaceDown[player, zone];
                        if (duel.ChangePosition(zone, out message) && reveal)
                        {
                            ShowActivatedDetail(monster);
                            if (alphaCpu is not null)
                            {
                                var center = NativeDuelLayout.MonsterCenter(zone, own);
                                BeginSpellActivation(center.X, center.Y, category: 1);
                            }
                        }
                    }
                }
                else if (own && Hit(spellRect) && duel.Spells[player, zone] is Card setSpell &&
                         duel.ActivateSetSpell(zone, out message))
                    BeginCardActivation(setSpell, spellRect);
            }
        }
    }
    static void DrawDuelHand()
    {
        var visible = VisibleHand(ViewPlayer);
        int page = Math.Clamp(handPage, 0, Math.Max(0, (visible.Count - 1) / 6));
        int count = Math.Min(6, visible.Count - page * 6);
        var drawOrder = Enumerable.Range(0, count).OrderBy(slot => handPage * 6 + slot == hoverHand ? 1 : 0);
        foreach (int slot in drawOrder)
        {
            int index = page * 6 + slot;
            if (DiscardInFlight(ViewPlayer, index)) continue;
            var rect = HandBoundsForCount(index, visible.Count, handLift[slot]);
            Raylib.DrawRectangleRec(rect, Panel);
            DrawArtwork(visible[index], (int)rect.X + 2, (int)rect.Y + 2, (int)rect.Width - 4, (int)rect.Height - 4);
            Raylib.DrawRectangleLinesEx(rect, index == hand ? 2 : 1, index == hand ? Color.White : Gold);
        }
        if (MotionBusy || PhaseBusy || hoverHand < 0 || hoverHand >= ViewHand.Count) return;
        if (!duel.CanSetFromHand(hoverHand) && !duel.CanActivateFromHand(hoverHand) && duel.PendingEndDiscards == 0) return;
        var bubble = ActionBounds(hoverHand % 6);
        bool enabled = HandActionEnabled(hoverHand);
        Raylib.DrawRectangleRounded(bubble, .3f, 6, enabled ? new Color(248, 229, 177, 255) : Color.Gray);
        Vector2 tip = new(bubble.X + bubble.Width / 2, bubble.Y + bubble.Height + 5);
        Raylib.DrawTriangle(new Vector2(tip.X - 6, tip.Y - 6), tip, new Vector2(tip.X + 6, tip.Y - 6), enabled ? Gold : Color.Gray);
        string caption = HandActionCaption(hoverHand);
        Label(caption, (int)(bubble.X + (bubble.Width - MeasureLabel(caption, 16)) / 2), (int)bubble.Y + 5, 16, Color.Black);
    }
    static void DrawDuel()
    {
        TimeSpan frameElapsed = TimeSpan.FromSeconds(Math.Clamp(Raylib.GetFrameTime(), 0, 1));
        presentation.Advance(frameElapsed);
        int hoverUpdates = hoverClock.Advance(frameElapsed, presentation.Style);
        bool effectsCouldAdvance = CanPlayCardEffects;
        AdvanceSpellActivation(frameElapsed);
        if (effectsCouldAdvance) AdvanceCardEffects(frameElapsed);
        AdvanceDuelPhases(frameElapsed);
        if (!MotionBusy && !PhaseBusy && (alphaCpu is null || HumanChoice)) ApplyOptionalEffectPromptPolicy();
        AdvanceAlpha(frameElapsed);
        var pendingOwner = CpuDuelController.PendingOwner(duel);
        bool selectingCard = duel.PendingEffectDecision is not null || duel.PendingCardSelection is not null ||
            duel.PendingSummonResponse is not null || duel.PendingAttackResponse is not null;
        bool showingMotion = MotionBusy || PhaseBusy;
        bool turnInput = !showingMotion && !alphaBlocked && CanPlayTurn && pendingOwner is null;
        bool actionInput = turnInput && CanPlayMainOrBattle;
        bool choiceInput = !showingMotion && !alphaBlocked && HumanChoice;
        if (duelTheme.Board is Texture2D board)
            Raylib.DrawTexturePro(board, new Rectangle(0, 0, board.Width, board.Height),
                new Rectangle(230, 0, 570, 600), Vector2.Zero, 0, Color.White);
        else if (alphaCpu is null)
            Raylib.DrawTexture(graphics.Texture(@"j\duel\field\fie_normal.bmp"), 230, 0, Color.White);
        else
        {
            Raylib.DrawRectangle(230, 72, 556, 229, new Color(35, 51, 63, 255));
            Raylib.DrawRectangle(230, 301, 556, 299, new Color(21, 66, 66, 255));
            Raylib.DrawLine(230, 301, 786, 301, Gold);
        }
        if (alphaCpu is not null) DrawCpuHand();
        if (Button("< Menu", 252, 8, 85, 30)) { screen = "menu"; return; }
        Label(alphaCpu is null ? $"LOCAL DEMO · {presentation.Style.Name}" : "ALPHA - VS CPU", 345, 17, 13, Gold);
        DrawPhasePanel(turnInput);
        if (Button("Reset", 454, 8, 75, 30))
        {
            if (alphaCpu is null) StartDemo(); else StartAlpha();
            return;
        }
        if (alphaCpu is null && Button(optionalEffectPrompts == EffectPromptMode.Ask
                ? "Prompts: ON" : "Prompts: OFF", 610, 74, 168, 30, !selectingCard))
            optionalEffectPrompts = optionalEffectPrompts == EffectPromptMode.Ask
                ? EffectPromptMode.Off : EffectPromptMode.Ask;
        if (duel.PendingFlipTarget is { Effect: DemoFlipEffect.ReturnUpToTwo } &&
            Button("Done FLIP", 605, 74, 125, 30, choiceInput))
        {
            duel.FinishFlipTargets(out message);
            ClearDuelSelection();
        }
        int turnLabel = displayedTurn == 0 ? duel.Turn : displayedTurn;
        int displayedPlayer = (turnLabel - 1) % 2;
        string playerLabel = alphaCpu is null ? $"P{displayedPlayer + 1}" : displayedPlayer == 0 ? "You" : "CPU";
        string heading = duel.IsDraw ? "DRAW" : duel.Winner is int winner
            ? alphaCpu is null ? $"PLAYER {winner + 1} WINS" : winner == 0 ? "YOU WIN" : "CPU WINS"
            : $"Turn {turnLabel} | {playerLabel}";
        Raylib.DrawRectangle(250, 44, 540, 24, Panel);
        Label(heading, 256, 49, 14, Gold);
        if (Button("Cancel", 530, 8, 63, 30, actionInput)) { ClearDuelSelection(); message = "Selection cleared."; }
        if (Button("< Hand", 600, 8, 78, 30, actionInput && handPage > 0)) { handPage--; ClearDuelSelection(); }
        if (Button("Hand >", 684, 8, 78, 30, actionInput && (handPage + 1) * 6 < ViewHand.Count)) { handPage++; ClearDuelSelection(); }
        // Refresh eligibility after any turn-changing input in this frame.
        actionInput &= !PhaseBusy && CanPlayMainOrBattle && CpuDuelController.PendingOwner(duel) is null;
        bool handInput = actionInput && UpdateHandInteraction(hoverUpdates);
        bool flipInput = choiceInput && duel.PendingFlipTarget is not null;
        DrawDuelField(handInput || (!actionInput && !flipInput) || selectingCard);
        if (!showingMotion && !selectingCard) UpdateDuelDetail();
        DrawDuelDetail();
        DrawSidePiles();
        DrawDuelHand();
        DrawCardEffects();
        DrawFlight();
        if (actionInput && duel.CanAttack(attacker) && attackCue is null)
            DrawAttackAim();
        DrawAttackCue();
        DrawSpellActivation();
        DrawPhaseNotice();
        if (duel.PendingFlipTarget is DemoFlipTarget flip)
        {
            string help = flip.Effect switch
            {
                DemoFlipEffect.ReturnUpToTwo =>
                    $"P{flip.Owner + 1} Penguin: {duel.SelectedFlipTargets.Count}/2, select or Done",
                DemoFlipEffect.ReturnOne =>
                    $"P{flip.Owner + 1} Hane-Hane: choose ONE eligible monster",
                _ => $"P{flip.Owner + 1} FLIP: choose any monster on the field"
            };
            if (HumanChoice) Label(help, 248, alphaCpu is null ? 78 : 195, 13, Gold);
        }
        if (message != previousDuelMessage)
        {
            DuelPresentationCue cue = message.Contains("FLIP", StringComparison.OrdinalIgnoreCase)
                ? DuelPresentationCue.FlipEffect
                : message.Contains("battle", StringComparison.OrdinalIgnoreCase)
                    ? DuelPresentationCue.Battle
                    : DuelPresentationCue.CardMove;
            presentation.Show(cue);
            previousDuelMessage = message;
        }
        var lines = TextLines(message, 365, 12);
        if (lines.Count > 0)
            Label(lines[0], 416, 50, 12, presentation.Busy ? Color.White : Gold);
        if (choiceInput && HumanChoice && pendingOwner is not null)
        {
            DrawCardSelectionOverlay();
            DrawSummonResponseOverlay();
            DrawAttackResponseOverlay();
            DrawEffectDecisionOverlay();
        }
    }
}
