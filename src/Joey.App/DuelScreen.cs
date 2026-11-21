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
    static Rectangle HandBounds(int slot)
    {
        int count = Math.Min(6, ViewHand.Count - handPage * 6);
        return new(HandPresentation.CenterX(slot, count) - HandPresentation.CardWidth / 2,
            600 - HandPresentation.CardHeight / 2 - handLift[slot], HandPresentation.CardWidth, HandPresentation.CardHeight);
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
            if (!duel.Play(index, out message)) return;
            if (FieldSpellRules.Supports(card.Id))
                BeginCardActivation(card, FieldZoneBounds(ViewPlayer));
            else if (zone >= 0 && card.ClassCode == 22)
                BeginCardActivation(card, ZoneBounds(ViewPlayer, zone, true));
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
    static void DrawCardBack(int x, int y, int width, int height)
    {
        Raylib.DrawRectangle(x, y, width, height, new Color(57, 32, 20, 255));
        Raylib.DrawRectangleLines(x + 2, y + 2, width - 4, height - 4, Gold);
        Raylib.DrawEllipseLines(x + width / 2, y + height / 2, width / 3f, height / 3f, Gold);
    }
    static void DrawFieldCard(Card card, Rectangle rect, bool faceDown, bool defense = false)
    {
        int x = (int)rect.X + 3, y = (int)rect.Y + 2;
        int width = (int)rect.Width - 6, height = (int)rect.Height - 4;
        if (faceDown)
        {
            int backHeight = defense ? Math.Min(height, width * 200 / 290) : height;
            DrawCardBack(x, y + (height - backHeight) / 2, width, backHeight);
        }
        else DrawArtwork(card, x, y, width, height, sideways: defense);
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
                DrawFieldCard(fieldCard, fieldRect, duel.FieldSpellFaceDown[player]);
                if (own && duel.FieldSpellFaceDown[player] && !blockInput &&
                    Raylib.IsMouseButtonPressed(MouseButton.Left) && Hit(fieldRect) &&
                    duel.ActivateSetFieldSpell(out message))
                    BeginCardActivation(fieldCard, fieldRect);
            }
            for (int zone = 0; zone < 5; zone++)
            {
                var monsterRect = ZoneBounds(player, zone, false);
                var spellRect = ZoneBounds(player, zone, true);
                int x = (int)monsterRect.X;
                var monster = duel.Monsters[player, zone];
                Raylib.DrawRectangleLinesEx(monsterRect, 1, own ? Gold : Color.Gray);
                Raylib.DrawRectangleLinesEx(spellRect, 1, own ? Gold : Color.Gray);
                if (monster != null && !InFlight(player, zone, false))
                {
                    DrawFieldCard(monster, monsterRect, duel.MonsterFaceDown[player, zone], duel.DefensePosition[player, zone]);
                    if (!duel.MonsterFaceDown[player, zone])
                        Label($"{duel.AttackValue(player, zone)}/{duel.DefenseValue(player, zone)}", x, (int)monsterRect.Y + 89, 10, Gold);
                }
                if (own && (attacker == zone || tributeZones.Contains(zone)))
                    Raylib.DrawRectangleLinesEx(monsterRect, 3, tributeZones.Contains(zone) ? Color.Red : Color.White);
                if (duel.SelectedFlipTargets.Any(t => t.Player == player && t.Zone == zone))
                    Raylib.DrawRectangleLinesEx(monsterRect, 4, Color.SkyBlue);
                if (duel.PendingFlipTarget is
                    { Effect: DemoFlipEffect.ReturnUpToTwo or DemoFlipEffect.ReturnOne } &&
                    duel.IsMarkedForBattleDestruction(player, zone))
                    Raylib.DrawRectangleLinesEx(monsterRect, 3, Color.Red);
                if (duel.Spells[player, zone] is Card spell && !InFlight(player, zone, true))
                    DrawFieldCard(spell, spellRect, duel.SpellFaceDown[player, zone]);
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
                        if (duel.Equip(hand, player, zone, out message))
                        {
                            BeginCardActivation(equip, ZoneBounds(ViewPlayer, sourceZone, true));
                            ClearDuelSelection();
                        }
                    }
                    else if (duel.Phase == DemoPhase.Battle)
                    {
                        if (own && monster != null) { attacker = zone; hand = -1; message = "Choose an enemy target or Direct attack."; }
                        else if (!own) CommitHumanAttack(zone);
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
        int count = Math.Min(6, ViewHand.Count - handPage * 6);
        var drawOrder = Enumerable.Range(0, count).OrderBy(slot => handPage * 6 + slot == hoverHand ? 1 : 0);
        foreach (int slot in drawOrder)
        {
            int index = handPage * 6 + slot;
            var rect = HandBounds(slot);
            Raylib.DrawRectangleRec(rect, Panel);
            DrawArtwork(ViewHand[index], (int)rect.X + 2, (int)rect.Y + 2, (int)rect.Width - 4, (int)rect.Height - 4);
            Raylib.DrawRectangleLinesEx(rect, index == hand ? 2 : 1, index == hand ? Color.White : Gold);
        }
        if (hoverHand < 0 || hoverHand >= ViewHand.Count) return;
        if (!duel.CanSetFromHand(hoverHand) && !duel.CanActivateFromHand(hoverHand) && duel.PendingEndDiscards == 0) return;
        var bubble = ActionBounds(hoverHand % 6);
        bool enabled = HandActionEnabled(hoverHand);
        Raylib.DrawRectangleRounded(bubble, .3f, 6, enabled ? new Color(248, 229, 177, 255) : Color.Gray);
        Vector2 tip = new(bubble.X + bubble.Width / 2, bubble.Y + bubble.Height + 5);
        Raylib.DrawTriangle(new Vector2(tip.X - 6, tip.Y - 6), tip, new Vector2(tip.X + 6, tip.Y - 6), enabled ? Gold : Color.Gray);
        string caption = HandActionCaption(hoverHand);
        Label(caption, (int)(bubble.X + (bubble.Width - Raylib.MeasureText(caption, 16)) / 2), (int)bubble.Y + 5, 16, Color.Black);
    }
    static void DrawDuel()
    {
        TimeSpan frameElapsed = TimeSpan.FromSeconds(Math.Clamp(Raylib.GetFrameTime(), 0, 1));
        presentation.Advance(frameElapsed);
        int hoverUpdates = hoverClock.Advance(frameElapsed, presentation.Style);
        bool activationWasPlaying = spellActivation.IsPlaying;
        spellActivation.Advance(frameElapsed, presentation.Style);
        if (!activationWasPlaying && attackCue is null)
            AdvanceDestructions(frameElapsed);
        if (alphaCpu is null || HumanChoice) ApplyOptionalEffectPromptPolicy();
        AdvanceAlpha(frameElapsed);
        var pendingOwner = CpuDuelController.PendingOwner(duel);
        bool selectingCard = duel.PendingEffectDecision is not null || duel.PendingCardSelection is not null ||
            duel.PendingSummonResponse is not null || duel.PendingAttackResponse is not null;
        bool showingMotion = flight is not null || attackCue is not null || spellActivation.IsPlaying || DestructionBusy;
        bool turnInput = !showingMotion && !alphaBlocked && CanPlayTurn && pendingOwner is null;
        bool actionInput = turnInput && CanPlayMainOrBattle;
        bool choiceInput = !showingMotion && !alphaBlocked && HumanChoice;
        if (alphaCpu is null)
            Raylib.DrawTexture(graphics.Texture(@"j\duel\field\fie_normal.bmp"), 230, 0, Color.White);
        else
        {
            Raylib.DrawRectangle(230, 72, 556, 229, new Color(35, 51, 63, 255));
            Raylib.DrawRectangle(230, 301, 556, 299, new Color(21, 66, 66, 255));
            Raylib.DrawLine(230, 301, 786, 301, Gold);
            DrawCpuHand();
        }
        if (Button("< Menu", 8, 8, 85, 30)) { screen = "menu"; return; }
        Label(alphaCpu is null ? $"LOCAL DEMO · {presentation.Style.Name}" : "ALPHA - VS CPU", 105, 16, 13, Gold);
        if (Button("Draw", 240, 8, 80, 30, turnInput && duel.CanDraw)) { duel.Draw(out message); ClearDuelSelection(); }
        if (Button("Battle", 326, 8, 80, 30, actionInput && duel.CanBeginBattle)) { duel.BeginBattle(out message); ClearDuelSelection(); }
        if (Button("End turn", 412, 8, 105, 30, actionInput && duel.Phase != DemoPhase.End))
        {
            duel.EndTurn(out message); ClearDuelSelection(); handPage = 0;
            if (alphaCpu is not null && duel.ActivePlayer == 1)
                cpuDelay = presentation.Style.Duration(DuelPresentationCue.CardMove);
        }
        if (Button("Reset", 523, 8, 75, 30))
        {
            if (alphaCpu is null) StartDemo(); else StartAlpha();
            return;
        }
        if (alphaCpu is null && Button(optionalEffectPrompts == EffectPromptMode.Ask
                ? "Prompts: ON" : "Prompts: OFF", 610, 8, 168, 30, !selectingCard))
            optionalEffectPrompts = optionalEffectPrompts == EffectPromptMode.Ask
                ? EffectPromptMode.Off : EffectPromptMode.Ask;
        if (duel.PendingFlipTarget is { Effect: DemoFlipEffect.ReturnUpToTwo } &&
            Button("Done FLIP", 605, 8, 125, 30, choiceInput))
        {
            duel.FinishFlipTargets(out message);
            ClearDuelSelection();
        }
        string playerLabel = alphaCpu is null ? $"P{duel.ActivePlayer + 1}" : duel.ActivePlayer == 0 ? "You" : "CPU";
        Label(duel.IsDraw ? "DRAW" : duel.Winner is int winner
            ? alphaCpu is null ? $"PLAYER {winner + 1} WINS" : winner == 0 ? "YOU WIN" : "CPU WINS"
            : $"Turn {duel.Turn} | {playerLabel}: {duel.Phase}", 8, 50, 16, Gold);
        Raylib.DrawRectangle(240, 46, 545, 24, Panel);
        Label($"{(alphaCpu is null ? "Opponent" : "CPU")} LP {duel.Life[1 - ViewPlayer]}       Your LP {duel.Life[ViewPlayer]}       {presentation.Style.Name}", 245, 51, 16);
        if (Button("Direct attack", 8, 380, 200, 30, actionInput && duel.CanAttack(attacker))) CommitHumanAttack(-1);
        if (Button("Cancel", 8, 416, 200, 30, actionInput)) { ClearDuelSelection(); message = "Selection cleared."; }
        if (Button("Hand <", 8, 452, 98, 30, actionInput && handPage > 0)) { handPage--; ClearDuelSelection(); }
        if (Button("Hand >", 112, 452, 98, 30, actionInput && (handPage + 1) * 6 < ViewHand.Count)) { handPage++; ClearDuelSelection(); }
        // Refresh eligibility after any turn-changing input in this frame.
        actionInput &= CanPlayMainOrBattle && CpuDuelController.PendingOwner(duel) is null;
        bool handInput = actionInput && UpdateHandInteraction(hoverUpdates);
        bool flipInput = choiceInput && duel.PendingFlipTarget is not null;
        DrawDuelField(handInput || (!actionInput && !flipInput) || selectingCard);
        if (!showingMotion && !selectingCard) UpdateDuelDetail();
        DrawDuelDetail();
        DrawGraveyards();
        DrawDestructions();
        DrawDuelHand();
        DrawFlight();
        DrawAttackCue();
        DrawSpellActivation();
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
        var lines = TextLines(message, 205, 14);
        for (int i = 0; i < Math.Min(4, lines.Count); i++) Label(lines[i], 8, 492 + i * 17, 14, presentation.Busy ? Color.White : Gold);
        Label($"Deck {duel.DeckCount(0)}/{duel.DeckCount(1)} Grave {VisibleGraveCount(0)}/{VisibleGraveCount(1)}", 8, 568, 13);
        Label("Right-click: Set", 8, 587, 12, Gold);
        if (choiceInput && HumanChoice && pendingOwner is not null)
        {
            DrawCardSelectionOverlay();
            DrawSummonResponseOverlay();
            DrawAttackResponseOverlay();
            DrawEffectDecisionOverlay();
        }
    }
}
