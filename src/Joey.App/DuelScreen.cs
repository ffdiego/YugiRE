using Joey.LookAndFeel;
using Joey.Core;
using Raylib_cs;
using System.Numerics;

static partial class Game
{
    // Native field centers and hand spacing; balloon rendering and update frequency are demo policy.
    static int hoverHand = -1;
    static int hoverCpuHand = -1;
    static int[] cpuHandDrop = [];
    static bool handActionsWereEnabled;
    static bool setHandAction, choosingTributes, choosingEquip;
    static int[] handLift = [];
    // Presentation-only numeric smoothing for the two monster rows.
    // Lazily allocated; no Core state or attribute values are mutated.
    static readonly NativeFieldAttributeTimeline?[,] fieldAttributeTimelines =
        new NativeFieldAttributeTimeline?[2, NativeDuelLayout.MonsterSlots];
    static readonly HandReflow<Card>[] handLayouts = [new(), new()];
    static readonly PresentationTickClock hoverClock = new();
    static int HandLiftAt(int index) => index < handLift.Length ? handLift[index] : 0;
    static int CpuHandDropAt(int index) => index < cpuHandDrop.Length ? cpuHandDrop[index] : 0;
    static Rectangle HandBounds(int slot) =>
        ToRectangle(handLayouts[ViewPlayer].Bounds(slot,
            VisibleHand(ViewPlayer).Count, HandLiftAt(slot)));

    static void SynchronizeHand(int player, IReadOnlyList<Card> visible, int updates = 0) =>
        handLayouts[player].Synchronize(visible, player == ViewPlayer, updates, presentation.Style.PlayAnimations);

    // The action cloud is part of the cursor image and moves with the
    // pointer; the card itself only has a native-free discard fallback.
    static Rectangle DiscardActionBounds(int slot)
    {
        var card = HandBounds(slot);
        return new(card.X, card.Y - 32, card.Width, 27);
    }

    static NativeCursorAction? CurrentHandAction(int index) =>
        index < 0 || index >= ViewHand.Count ? null :
        NativeDuelCursor.HandAction(
            ViewHand[index].IsMonster,
            duel.CanSummonFromHand(index),
            duel.CanSetFromHand(index),
            duel.CanActivateFromHand(index),
            setHandAction,
            duel.PendingEndDiscards > 0);

    static bool CanToggleHandAction(int index) =>
        index >= 0 && index < ViewHand.Count &&
        NativeDuelCursor.CanToggleHandAction(
            ViewHand[index].IsMonster,
            duel.CanSummonFromHand(index),
            duel.CanSetFromHand(index),
            duel.CanActivateFromHand(index));
    static bool Hit(Rectangle rect) => Raylib.CheckCollisionPointRec(mouse, rect);

    // card.txt holds both large hand and small board selection frames.
    // Centre the frame over the *drawn card*, not the larger input zone.
    static void DrawCardSelectionFrame(Rectangle card, bool nearSide, bool large, int degrees)
    {
        string name = large
            ? NativeCardSelectionFrame.LargeSprite(nearSide)
            : NativeCardSelectionFrame.SmallSprite(nearSide);
        var texture = graphics.Texture(name);
        // Use the texture's NATURAL dimensions for DrawTexturePro.
        // A 90-degree rotation turns the 58x80 raster into an 80x58
        // footprint. Passing 80x58 as the destination would transpose it
        // twice and leave the highlight vertical on defense cards.
        var footprint = NativeCardSelectionFrame.ProjectedBounds(
            ToCardBounds(card), texture.Width, texture.Height, degrees);
        Raylib.DrawTexturePro(texture,
            new Rectangle(0, 0, texture.Width, texture.Height),
            new Rectangle(footprint.X + footprint.Width / 2f,
                          footprint.Y + footprint.Height / 2f,
                          texture.Width, texture.Height),
            new Vector2(texture.Width / 2f, texture.Height / 2f),
            degrees, Color.White);
    }

    static void DrawHandSelectionFrame(Rectangle bounds, bool nearSide) =>
        DrawCardSelectionFrame(bounds, nearSide, large: true, degrees: nearSide ? 0 : 180);

    static void DrawFieldSelectionFrame(CardRenderGeometry geometry, bool nearSide) =>
        DrawCardSelectionFrame(ToRectangle(geometry.Bounds), nearSide,
            large: false, degrees: geometry.Degrees);

    static void UpdateCpuHandHover(int updates)
    {
        hoverCpuHand = -1;
        if (alphaCpu is null) return;
        int opponent = 1 - ViewPlayer;
        var cards = VisibleHand(opponent);
        if (cpuHandDrop.Length < cards.Count) Array.Resize(ref cpuHandDrop, cards.Count);
        int next = -1;
        for (int index = 0; index < cards.Count; index++)
        {
            if (IncomingTransferInFlight(opponent, index, cards[index])) continue;
            var current = ToRectangle(handLayouts[opponent].Bounds(
                index, cards.Count, CpuHandDropAt(index)));
            var rest = ToRectangle(handLayouts[opponent].Bounds(index, cards.Count));
            float top = MathF.Min(current.Y, rest.Y);
            float bottom = MathF.Max(current.Y + current.Height, rest.Y + rest.Height);
            if (Hit(new Rectangle(current.X, top, current.Width, bottom - top)))
                next = index;
        }
        hoverCpuHand = next;
        for (int index = 0; index < cpuHandDrop.Length; index++)
        {
            int target = index == hoverCpuHand ? HandPresentation.HoverOffset : 0;
            if (!presentation.Style.PlayAnimations) cpuHandDrop[index] = target;
            else
                for (int i = 0; i < updates && cpuHandDrop[index] != target; i++)
                    cpuHandDrop[index] = presentation.Style.MoveCardAxis(cpuHandDrop[index], target);
        }
    }

    static void ClearDuelSelection()
    {
        hand = attacker = hoverHand = hoverCpuHand = -1;
        handActionsWereEnabled = false;
        setHandAction = choosingTributes = choosingEquip = false;
        tributeZones.Clear();
        Array.Clear(handLift);
        Array.Clear(cpuHandDrop);
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
        var action = CurrentHandAction(index);
        if (action is null) return;
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
        else if (action == NativeCursorAction.Set)
        {
            int zone = duel.FirstFreeSpellZone();
            var origin = HandBounds(index);
            if (duel.SetFromHand(index, out message))
            {
                ShowPlayedCardDetail(card);
                handLayouts[ViewPlayer].RemoveAt(index);
                if (zone >= 0)
                {
                    BeginFlight(card, ViewPlayer, zone, origin, true, true);
                    QueueSetEffect(ViewPlayer, zone, spell: true);
                }
                ClearDuelSelection();
            }
        }
        else if (action == NativeCursorAction.Activate &&
                 EquipmentRules.Supports(card.Id) && duel.CanActivateFromHand(index))
        {
            choosingEquip = true;
            message = $"Choose a compatible face-up monster for {card.Name}.";
        }
        else if (action == NativeCursorAction.Activate)
        {
            int zone = duel.FirstFreeSpellZone();
            var origin = HandBounds(index);
            if (!duel.Play(index, out message)) return;
            if (duel.PendingSpellTrapTarget is not null || duel.PendingFusionSummon is not null)
            {
                ClearDuelSelection();
                return;
            }
            ShowPlayedCardDetail(card);
            handLayouts[ViewPlayer].RemoveAt(index);
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
        var origin = HandBounds(index);
        if (!duel.SummonLeftmost(index, setHandAction, out message, tributeZones)) return false;
        ShowPlayedCardDetail(card);
        handLayouts[ViewPlayer].RemoveAt(index);
        bool set = setHandAction;
        BeginFlight(card, ViewPlayer, zone, origin, false, set);
        if (set) QueueSetEffect(ViewPlayer, zone, spell: false);
        else QueueNormalSummonEffect(card, ViewPlayer, zone);
        ClearDuelSelection();
        return true;
    }
    static string HandActionCaption(int index) => "Discard"; // sandbox fallback only
    static bool HandActionEnabled(int index) =>
        duel.PendingEndDiscards > 0 || CurrentHandAction(index) is not null;
    // Inspection and lifting are independent of Core action availability.
    // The player may hover both hands during draws, effects and CPU turns.
    static bool UpdateHandInteraction(int updates, bool allowActions)
    {
        var visible = VisibleHand(ViewPlayer);
        int count = visible.Count;
        if (handLift.Length < count) Array.Resize(ref handLift, count);
        int next = -1;
        for (int slot = 0; slot < count; slot++)
        {
            if (IncomingTransferInFlight(ViewPlayer, slot, visible[slot])) continue;
            var rect = HandBounds(slot);
            // Hover is on the card, not on a static bubble (the original
            // action bubble follows the cursor). Union lifted/rest bounds
            // prevents flicker as the card animates upward.
            var resting = ToRectangle(handLayouts[ViewPlayer].Bounds(
                slot, count, 0));
            float minY = MathF.Min(rect.Y, resting.Y);
            float maxY = MathF.Max(rect.Y + rect.Height, resting.Y + resting.Height);
            if (Hit(new Rectangle(rect.X, minY, rect.Width, maxY - minY)))
                next = slot;
        }
        bool overHand = next >= 0;
        if (choosingTributes || choosingEquip) next = -1;
        if (next != hoverHand || (allowActions && !handActionsWereEnabled))
        {
            hoverHand = next;
            if (allowActions && !choosingTributes && !choosingEquip)
            {
                // Monsters default to Summon, spells to Activate when legal;
                // a Trap can only Set, even before right-click.
                setHandAction = next >= 0 &&
                    NativeDuelCursor.HandAction(
                        ViewHand[next].IsMonster,
                        duel.CanSummonFromHand(next),
                        duel.CanSetFromHand(next),
                        duel.CanActivateFromHand(next),
                        requestSet: false,
                        discarding: duel.PendingEndDiscards > 0)
                    == NativeCursorAction.Set;
            }
        }
        handActionsWereEnabled = allowActions;
        for (int slot = 0; slot < handLift.Length; slot++)
        {
            int target = hoverHand == slot ? HandPresentation.HoverOffset : 0;
            if (!presentation.Style.PlayAnimations) handLift[slot] = target;
            else
                for (int i = 0; i < updates && handLift[slot] != target; i++)
                    handLift[slot] = presentation.Style.MoveCardAxis(handLift[slot], target);
        }
        if (hoverHand < 0 || !allowActions) return overHand;
        hand = hoverHand; attacker = -1;
        if (Raylib.IsMouseButtonPressed(MouseButton.Right) &&
            duel.PendingEndDiscards == 0 && CanToggleHandAction(hoverHand))
            setHandAction = !setHandAction;
        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && HandActionEnabled(hoverHand))
            UseHandAction(hoverHand);
        return true;
    }
    static void DrawCardBack(int x, int y, int width, int height, int degrees = 0)
    {
        bool sideways = degrees % 180 != 0;
        // The original ships a separate mini/card_ura.bmp. Preserve any
        // explicit custom duel theme, but use the native 50x72 back on
        // field/deck-size cards instead of reducing the 200x290 back.
        Texture2D? cardBack = NativeCardRaster.IsNativeMiniDestination(width, height) &&
            duelTheme.UsesNativeCardBack
                ? graphics.Texture(DatGraphics.MiniCardBack)
                : duelTheme.CardBack;
        if (cardBack is Texture2D texture)
        {
            Raylib.DrawTexturePro(texture,
                new Rectangle(0, 0, texture.Width, texture.Height),
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

    // 004194D0 draws DEF, its slash, then ATK using original DAT bitmaps.
    // Position alpha is 0xA0/0x100; modified attributes use blue/red atlas.
    // The anchor is the existing 00418A80 field-card geometry, whose final
    // native pixel parity still requires paired frame comparison.
    static void DrawFieldAttributes(int player, int zone, Card monster, CardPoint center, int presentationUpdates)
    {
        var normal = graphics.Texture(NativeFieldAttributeFonts.Normal);
        int width = NativeLifeHud.GlyphWidth(normal.Width, NativeFieldAttributeFonts.NormalColumns);
        var timeline = fieldAttributeTimelines[player, zone] ??= new NativeFieldAttributeTimeline();
        var displayed = timeline.Sample(monster,
            monster.Attack, monster.Defense,
            duel.AttackValue(player, zone), duel.DefenseValue(player, zone),
            presentationUpdates, presentation.Style.PlayAnimations);
        var frame = NativeFieldAttributeFrame.Compose(center, width, normal.Height,
            displayed.Attack, displayed.Defense,
            monster.Attack, monster.Defense, duel.DefensePosition[player, zone]);

        DrawFieldNumber(frame.Defense);
        Raylib.DrawTexturePro(normal,
            new Rectangle(NativeFieldAttributeFonts.SeparatorIndex * width, 0, width, normal.Height),
            new Rectangle(frame.SeparatorX, frame.SeparatorY, width, normal.Height),
            Vector2.Zero, 0,
            new Color(255, 255, 255, Math.Min(255, frame.SeparatorOpacity)));
        DrawFieldNumber(frame.Attack);
    }

    static void DrawFieldNumber(NativeFieldNumberPass pass)
    {
        var font = graphics.Texture(pass.Sprite);
        int glyphWidth = NativeLifeHud.GlyphWidth(font.Width, pass.Columns);
        int[] digits = NativeLifeHud.DecimalGlyphs(pass.Value);
        int x = pass.LeftX(glyphWidth);
        var tint = new Color(255, 255, 255, Math.Min(255, pass.Opacity));
        foreach (int digit in digits)
        {
            Raylib.DrawTexturePro(font,
                new Rectangle(digit * glyphWidth, 0, glyphWidth, font.Height),
                new Rectangle(x, pass.TopY, glyphWidth, font.Height),
                Vector2.Zero, 0, tint);
            x += glyphWidth;
        }
    }

    static void DrawDuelField(bool blockInput, int attributeUpdates)
    {
        var pendingTarget = duel.PendingSpellTrapTarget;
        bool CanHighlightSpell(int player, int zone) => pendingTarget is null ||
            HumanChoice && pendingTarget.EligibleTargets.Any(target => target.Player == player && target.Zone == zone);
        for (int player = 0; player < 2; player++)
        {
            bool own = player == ViewPlayer;
            // Dedicated field zone, independent of the five ordinary spell slots.
            var fieldRect = FieldZoneBounds(player);
            if (duelDebug) Raylib.DrawRectangleLinesEx(fieldRect, 1, own ? Gold : Color.Gray);
            if (duel.FieldSpells[player] is Card fieldCard)
            {
                if (!ActivationInFlight(fieldRect))
                {
                    var geometry = DuelCardLayout.OnBoard(
                        DuelCardZone.Field, own, 0, false, duel.FieldSpellFaceDown[player]);
                    DrawDuelCard(fieldCard, geometry);
                    if (Hit(fieldRect) && CanHighlightSpell(player, SpellTrapTargetRules.FieldZone) && !IsJoeySpeaking())
                        DrawFieldSelectionFrame(geometry, own);
                }
                if (own && duel.FieldSpellFaceDown[player] && !blockInput && duel.PendingSpellTrapTarget is null &&
                    Raylib.IsMouseButtonPressed(MouseButton.Left) && Hit(fieldRect) &&
                    duel.ActivateSetFieldSpell(out message))
                    BeginCardActivation(fieldCard, fieldRect);
                else if (!blockInput && Hit(fieldRect) && duel.CanUseFusionGate(player))
                {
                    Label("Click Fusion Gate to Fusion Summon", 10, 587, 11, Gold);
                    if (Raylib.IsMouseButtonPressed(MouseButton.Left) && duel.BeginFusionGate(player, out message))
                        ClearDuelSelection();
                }
            }
            for (int zone = 0; zone < 5; zone++)
            {
                var monsterRect = ZoneBounds(player, zone, false);
                var spellRect = ZoneBounds(player, zone, true);
                var monster = duel.Monsters[player, zone];
                if (duelDebug)
                {
                    Raylib.DrawRectangleLinesEx(monsterRect, 1, own ? Gold : Color.Gray);
                    Raylib.DrawRectangleLinesEx(spellRect, 1, own ? Gold : Color.Gray);
                }
                if (monster != null && !InFlight(player, zone, false))
                {
                    var geometry = DuelCardLayout.OnBoard(DuelCardZone.Monster, own, zone,
                        duel.DefensePosition[player, zone], duel.MonsterFaceDown[player, zone]);
                    DrawDuelCard(monster, geometry);
                    if (Hit(monsterRect) && pendingTarget is null && !IsJoeySpeaking())
                        DrawFieldSelectionFrame(geometry, own);
                    // The Core may have flipped an enemy defender logically
                    // before the sword reaches it. Reuse the same presentation
                    // gate as F12: do not expose that enemy's new attributes
                    // beneath the captured face-down card or in diagnostics.
                    bool showAttributes = NativeFieldAttributeVisibility.ShouldDrawClient(
                        own, duel.MonsterFaceDown[player, zone],
                        settled: true, hiddenDefenderBeforeImpact:
                            IsHiddenAttackTargetBeforeImpact(player, zone));
                    if (showAttributes && geometry.AttributesCenter is { } center)
                        DrawFieldAttributes(player, zone, monster, center, attributeUpdates);
                    else
                        fieldAttributeTimelines[player, zone]?.Reset();
                }
                if (monster is null || InFlight(player, zone, false))
                    fieldAttributeTimelines[player, zone]?.Reset();

                if (own && (attacker == zone || tributeZones.Contains(zone)))
                    Raylib.DrawRectangleLinesEx(monsterRect, 3, tributeZones.Contains(zone) ? Color.Red : Color.White);
                if (duel.SelectedFlipTargets.Any(t => t.Player == player && t.Zone == zone))
                    Raylib.DrawRectangleLinesEx(monsterRect, 4, Color.SkyBlue);
                if (duel.PendingFlipTarget is
                    { Effect: DemoFlipEffect.ReturnUpToTwo or DemoFlipEffect.ReturnOne } &&
                    duel.IsMarkedForBattleDestruction(player, zone))
                    Raylib.DrawRectangleLinesEx(monsterRect, 3, Color.Red);
                if (duel.Spells[player, zone] is Card spell && !InFlight(player, zone, true) && !ActivationInFlight(spellRect))
                {
                    var geometry = DuelCardLayout.OnBoard(
                        DuelCardZone.Spell, own, zone, false, duel.SpellFaceDown[player, zone]);
                    DrawDuelCard(spell, geometry);
                    if (Hit(spellRect) && CanHighlightSpell(player, zone) && !IsJoeySpeaking())
                        DrawFieldSelectionFrame(geometry, own);
                }
                if (!blockInput && own && duel.CanAttack(zone) && attacker != zone)
                    DrawEffectStamp(DatGraphics.AttackSword, NativeAttackFrame.SwordAnchor(zone, own),
                        0, 0x10000, 0xa0);
                if (blockInput || duel.PendingSpellTrapTarget is not null ||
                    !Raylib.IsMouseButtonPressed(MouseButton.Left)) continue;
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
                        var origin = HandBounds(hand);
                        if (duel.Equip(hand, player, zone, out message))
                        {
                            handLayouts[ViewPlayer].RemoveAt(hand);
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
                         duel.ActivateSetSpell(zone, out message) && duel.PendingSpellTrapTarget is null &&
                         duel.PendingFusionSummon is null)
                    BeginCardActivation(setSpell, spellRect);
            }
        }
    }

    static void ReadSpellTrapTarget(PendingSpellTrapTarget pending)
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Backspace))
        {
            duel.CancelSpellTrapTarget(out message);
            return;
        }
        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return;
        foreach (var target in pending.EligibleTargets)
        {
            var area = target.IsFieldZone ? FieldZoneBounds(target.Player) : ZoneBounds(target.Player, target.Zone, true);
            if (!Hit(area)) continue;
            int sourceZone = pending.FromSet ? pending.SourceIndex : duel.FirstFreeSpellZone();
            Rectangle? origin = pending.FromSet ? null : HandBounds(pending.SourceIndex);
            if (duel.ResolveSpellTrapTarget(target.Player, target.Zone, out message))
            {
                if (!pending.FromSet) handLayouts[pending.Owner].RemoveAt(pending.SourceIndex);
                ShowPlayedCardDetail(pending.Spell);
                BeginCardActivation(pending.Spell, ZoneBounds(pending.Owner, sourceZone, true), origin);
                ClearDuelSelection();
            }
            return;
        }
    }
    static void DrawDuelHand()
    {
        var visible = VisibleHand(ViewPlayer);
        SynchronizeHand(ViewPlayer, visible);
        int focused = (choosingTributes || choosingEquip) && hand >= 0 ? hand : hoverHand;
        var drawOrder = Enumerable.Range(0, visible.Count).OrderBy(index => index == focused ? 1 : 0);
        foreach (int index in drawOrder)
        {
            if (IncomingTransferInFlight(ViewPlayer, index, visible[index])) continue;
            var rect = ToRectangle(handLayouts[ViewPlayer].Bounds(index, visible.Count, HandLiftAt(index)));
            DrawArtwork(visible[index], (int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
            if (index == focused) DrawHandSelectionFrame(rect, nearSide: true);
        }
        // The cursor image itself switches to Summon/Set/Activate when
        // a legal hand action is hovered. Do not draw a second action
        // cloud tied to the card. Keep the sandbox discard caption only.
        if (MotionBusy || PhaseBusy || hoverHand < 0 ||
            hoverHand >= ViewHand.Count || duel.PendingEndDiscards == 0) return;
        var bubble = DiscardActionBounds(hoverHand);
        Raylib.DrawRectangleRounded(bubble, .3f, 6,
            new Color(248, 229, 177, 255));
        Vector2 tip = new(bubble.X + bubble.Width / 2, bubble.Y + bubble.Height + 5);
        Raylib.DrawTriangle(new Vector2(tip.X - 6, tip.Y - 6), tip, new Vector2(tip.X + 6, tip.Y - 6), Gold);
        string caption = HandActionCaption(hoverHand);
        Label(caption, (int)(bubble.X + (bubble.Width - MeasureLabel(caption, 16)) / 2), (int)bubble.Y + 5, 16, Color.Black);
    }
    static bool IsJoeySpeaking() =>
        presentation.Busy && presentation.ActiveCue == DuelPresentationCue.JoeySpeech;

    static void DrawDuel()
    {
        TimeSpan frameElapsed = VisualReviewTiming.Elapsed(
            TimeSpan.FromSeconds(Raylib.GetFrameTime()), presentation.Style,
            visualReviewPaused, visualReviewStep);
        presentation.Advance(frameElapsed);
        int hoverUpdates = hoverClock.Advance(frameElapsed, presentation.Style);
        bool effectsCouldAdvance = CanPlayCardEffects;
        AdvanceSpellActivation(frameElapsed);
        if (effectsCouldAdvance) AdvanceCardEffects(frameElapsed);
        AdvanceDuelPhases(frameElapsed);
        if (!visualReviewPaused && !MotionBusy && !PhaseBusy &&
            (alphaCpu is null || HumanChoice)) ApplyOptionalEffectPromptPolicy();
        for (int player = 0; player < 2; player++)
            SynchronizeHand(player, VisibleHand(player), hoverUpdates);
        AdvanceAlpha(frameElapsed);
        var pendingOwner = CpuDuelController.PendingOwner(duel);
        var fusion = duel.PendingFusionSummon;
        bool selectingCard = duel.PendingEffectDecision is not null || duel.PendingCardSelection is not null || fusion is not null ||
            duel.PendingSummonResponse is not null || duel.PendingAttackResponse is not null;
        bool showingMotion = MotionBusy || PhaseBusy;
        bool turnInput = !visualReviewPaused && !showingMotion && !alphaBlocked && CanPlayTurn && pendingOwner is null;
        bool actionInput = turnInput && CanPlayMainOrBattle;
        bool choiceInput = !visualReviewPaused && !showingMotion && !alphaBlocked && HumanChoice;
        var spellTarget = duel.PendingSpellTrapTarget;
        Raylib.DrawTexture(graphics.Texture(DatGraphics.DuelDetailBackground), 0, 0, Color.White);
        if (duelTheme.Board is Texture2D board)
            Raylib.DrawTexturePro(board, new Rectangle(0, 0, board.Width, board.Height),
                ToRectangle(DuelSurfaceLayout.Board), Vector2.Zero, 0, Color.White);
        bool joeySpeaking = IsJoeySpeaking();
        if (!joeySpeaking) UpdateCpuHandHover(hoverUpdates);
        else hoverCpuHand = -1;
        if (alphaCpu is not null) DrawCpuHand();
        DrawPhasePanel(turnInput);

        if (actionInput && Raylib.IsKeyPressed(KeyboardKey.Backspace)) ClearDuelSelection();
        // Refresh eligibility after any turn-changing input in this frame.
        actionInput &= !PhaseBusy && CanPlayMainOrBattle && CpuDuelController.PendingOwner(duel) is null;
        bool handHovered = !joeySpeaking && UpdateHandInteraction(hoverUpdates, actionInput);
        bool handInput = actionInput && handHovered;
        bool flipInput = choiceInput && duel.PendingFlipTarget is not null;
        if (choiceInput && spellTarget is not null) ReadSpellTrapTarget(spellTarget);
        DrawDuelField(handInput || (!actionInput && !flipInput) || selectingCard,
            hoverUpdates);
        // A travelling card or the CPU's turn must not freeze the sidebar.
        // Only an actual Joey speech cue may suppress inspection.
        if (!joeySpeaking) UpdateDuelDetail();
        AdvanceLifePresentation(frameElapsed);
        DrawDuelDetail();
        if (duel.PendingFlipTarget is { Effect: DemoFlipEffect.ReturnUpToTwo } &&
            Button("Done", 132, 584, 76, 16, choiceInput))
        {
            duel.FinishFlipTargets(out message);
            ClearDuelSelection();
        }
        DrawSidePiles();
        DrawDuelHand();
        DrawCardEffects();
        DrawFlight();
        // Draw this AFTER queued destruction cards so no face-up card
        // appears beneath the sword before the impact sample.
        DrawHiddenAttackTargetBeforeImpact();
        if (actionInput && duel.CanAttack(attacker) && attackCue is null)
            DrawAttackAim();
        DrawAttackCue();
        DrawSpellActivation();
        DrawLifeFloatingDigits();
        DrawPhaseNotice();
        if (duel.PendingFlipTarget is DemoFlipTarget flip)
        {
            string help = flip.Effect switch
            {
                DemoFlipEffect.ReturnUpToTwo =>
                    $"Select up to 2 ({duel.SelectedFlipTargets.Count}/2)",
                DemoFlipEffect.ReturnOne =>
                    "Select one monster",
                _ => "Select a FLIP target"
            };
            if (HumanChoice) Label(help, 10, 587, 11, Gold);
        }
        else if (choiceInput && duel.PendingSpellTrapTarget is not null)
            Label("Choose a Spell or Trap (Backspace cancels)", 10, 587, 11, Gold);
        else if (actionInput && (choosingTributes || choosingEquip || duel.PendingEndDiscards > 0))
            Label(choosingTributes ? "Choose tributes (Backspace cancels)" : choosingEquip
                ? "Choose equip target (Backspace cancels)" : $"Discard {duel.PendingEndDiscards} to end turn", 10, 587, 11, Gold);
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
        DrawDuelResultNotice(frameElapsed);
        if (duelDebug) DrawDuelDebug(selectingCard);
        if (choiceInput && fusion is not null)
            DrawFusionSelection(fusion);
        else if (choiceInput && HumanChoice && pendingOwner is not null)
        {
            DrawCardSelectionOverlay();
            DrawSummonResponseOverlay();
            DrawAttackResponseOverlay();
            DrawEffectDecisionOverlay();
        }
    }
    static void DrawDuelDebug(bool selectingCard)
    {
        if (Button("Ocultar debug", 262, 8, 142, 30)) duelDebug = false;
        if (Button("< Menu", 411, 8, 85, 30)) screen = "menu";
        if (Button("Reset", 501, 8, 75, 30))
        {
            if (alphaCpu is null) StartDemo(); else StartAlpha();
        }
        if (alphaCpu is null && Button(optionalEffectPrompts == EffectPromptMode.Ask
                ? "Prompts: ON" : "Prompts: OFF", 583, 8, 172, 30, !selectingCard))
            optionalEffectPrompts = optionalEffectPrompts == EffectPromptMode.Ask
                ? EffectPromptMode.Off : EffectPromptMode.Ask;
        Raylib.DrawRectangle(262, 42, 502, 26, Panel);
        Label(TextLines(message, 490, 12)[0], 268, 49, 12);
    }

}
