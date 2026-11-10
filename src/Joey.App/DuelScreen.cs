using Joey.Core;
using Raylib_cs;
using System.Numerics;

static partial class Game
{
    // Native field centers and hand spacing; balloon rendering and update frequency are demo policy.
    static int hoverHand = -1;
    static bool setHandAction, choosingTributes, choosingEquip;
    static readonly int[] handLift = new int[6];
    static Rectangle HandBounds(int slot)
    {
        int count = Math.Min(6, duel.Hand.Count - handPage * 6);
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
        handPage = Math.Clamp(handPage, 0, Math.Max(0, (duel.Hand.Count - 1) / 6));
        Array.Clear(handLift);
    }
    static void UseHandAction(int index)
    {
        if (index < 0 || index >= duel.Hand.Count) return;
        hand = index; attacker = -1;
        var card = duel.Hand[index];
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
            if (duel.SummonLeftmost(index, setHandAction, out message)) ClearDuelSelection();
        }
        else if (setHandAction)
        {
            if (duel.SetFromHand(index, out message)) ClearDuelSelection();
        }
        else if (card.Id == 300 && duel.CanActivateFromHand(index))
        {
            choosingEquip = true;
            message = "Choose a face-up Warrior for Legendary Sword.";
        }
        else if (duel.Play(index, out message)) ClearDuelSelection();
    }
    static string HandActionCaption(int index) => duel.PendingEndDiscards > 0 ? "Discard" :
        setHandAction ? "Set" : duel.Hand[index].IsMonster ? "Summon" : "Activate";
    static bool HandActionEnabled(int index) => duel.PendingEndDiscards > 0 ||
        (setHandAction ? duel.CanSetFromHand(index) : duel.Hand[index].IsMonster
            ? duel.CanSummonFromHand(index) : duel.CanActivateFromHand(index));
    static bool UpdateHandInteraction()
    {
        handPage = Math.Clamp(handPage, 0, Math.Max(0, (duel.Hand.Count - 1) / 6));
        int count = Math.Min(6, duel.Hand.Count - handPage * 6);
        int next = -1;
        for (int slot = 0; slot < count; slot++)
        {
            var rect = HandBounds(slot);
            // Include both rest and lifted positions so the card never escapes its hover area.
            if (Hit(new Rectangle(rect.X, rect.Y, rect.Width, 598 - rect.Y))) next = handPage * 6 + slot;
        }
        if (next < 0 && hoverHand / 6 == handPage && hoverHand >= 0 && hoverHand < duel.Hand.Count)
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
            handLift[slot] = HandPresentation.Approach(handLift[slot], target);
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
        if (faceDown) DrawCardBack(x, y, (int)rect.Width - 6, (int)rect.Height - 4);
        else DrawArtwork(card, x, y, (int)rect.Width - 6, (int)rect.Height - 4);
        if (defense) Label("DEF", x + 2, y + (int)rect.Height - 16, 10, Gold);
    }
    static void DrawDuelField(bool blockInput)
    {
        for (int player = 0; player < 2; player++)
        {
            bool own = player == duel.ActivePlayer;
            for (int zone = 0; zone < 5; zone++)
            {
                int x = 336 + (own ? zone : 4 - zone) * 78;
                var monsterRect = new Rectangle(x, own ? 308 : 204, 66, 88);
                var spellRect = new Rectangle(x, own ? 416 : 96, 66, 88);
                var monster = duel.Monsters[player, zone];
                Raylib.DrawRectangleLinesEx(monsterRect, 1, own ? Gold : Color.Gray);
                Raylib.DrawRectangleLinesEx(spellRect, 1, own ? Gold : Color.Gray);
                if (monster != null)
                {
                    DrawFieldCard(monster, monsterRect, duel.MonsterFaceDown[player, zone], duel.DefensePosition[player, zone]);
                    if (!duel.MonsterFaceDown[player, zone])
                        Label($"{duel.AttackValue(player, zone)}/{duel.DefenseValue(player, zone)}", x, (int)monsterRect.Y + 89, 10, Gold);
                }
                if (own && (attacker == zone || tributeZones.Contains(zone)))
                    Raylib.DrawRectangleLinesEx(monsterRect, 3, tributeZones.Contains(zone) ? Color.Red : Color.White);
                if (duel.Spells[player, zone] is Card spell)
                    DrawFieldCard(spell, spellRect, duel.SpellFaceDown[player, zone]);
                if (blockInput || !Raylib.IsMouseButtonPressed(MouseButton.Left)) continue;
                if (Hit(monsterRect))
                {
                    if (choosingTributes && own && monster != null)
                    {
                        if (!tributeZones.Remove(zone)) tributeZones.Add(zone);
                        int needed = DemoDuel.NormalTributes(duel.Hand[hand]);
                        if (tributeZones.Count == needed && duel.SummonLeftmost(hand, setHandAction, out message, tributeZones))
                            ClearDuelSelection();
                        else message = $"Choose {needed - tributeZones.Count} more tribute(s).";
                    }
                    else if (choosingEquip && monster != null)
                    {
                        if (duel.Equip(hand, player, zone, out message)) ClearDuelSelection();
                    }
                    else if (duel.Phase == DemoPhase.Battle)
                    {
                        if (own && monster != null) { attacker = zone; hand = -1; message = "Choose an enemy target or Direct attack."; }
                        else if (!own) duel.Attack(attacker, zone, out message);
                    }
                    else if (own && monster != null) duel.ChangePosition(zone, out message);
                }
                else if (own && Hit(spellRect) && duel.Spells[player, zone] != null)
                    duel.ActivateSetSpell(zone, out message);
            }
        }
    }
    static void DrawDuelHand()
    {
        int count = Math.Min(6, duel.Hand.Count - handPage * 6);
        var drawOrder = Enumerable.Range(0, count).OrderBy(slot => handPage * 6 + slot == hoverHand ? 1 : 0);
        foreach (int slot in drawOrder)
        {
            int index = handPage * 6 + slot;
            var rect = HandBounds(slot);
            Raylib.DrawRectangleRec(rect, Panel);
            DrawArtwork(duel.Hand[index], (int)rect.X + 2, (int)rect.Y + 2, (int)rect.Width - 4, (int)rect.Height - 4);
            Raylib.DrawRectangleLinesEx(rect, index == hand ? 2 : 1, index == hand ? Color.White : Gold);
        }
        if (hoverHand < 0 || hoverHand >= duel.Hand.Count) return;
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
        Raylib.DrawTexture(graphics.Texture(@"j\duel\field\fie_normal.bmp"), 230, 0, Color.White);
        if (Button("< Menu", 8, 8, 85, 30)) screen = "menu";
        Label("LOCAL DEMO", 105, 16, 16, Gold);
        if (Button("Draw", 240, 8, 80, 30)) { duel.Draw(out message); ClearDuelSelection(); }
        if (Button("Battle", 326, 8, 80, 30)) { duel.BeginBattle(out message); ClearDuelSelection(); }
        if (Button("End turn", 412, 8, 105, 30)) { duel.EndTurn(out message); ClearDuelSelection(); handPage = 0; }
        if (Button("Reset", 523, 8, 75, 30)) StartDemo();
        Label(duel.Winner is int winner ? $"PLAYER {winner + 1} WINS" : $"Turn {duel.Turn} P{duel.ActivePlayer + 1}: {duel.Phase}", 8, 50, 16, Gold);
        Raylib.DrawRectangle(240, 46, 545, 24, Panel);
        Label($"Opponent LP {duel.Life[1 - duel.ActivePlayer]}       Your LP {duel.Life[duel.ActivePlayer]}", 245, 51, 16);
        if (Button("Direct attack", 8, 380, 200, 30)) duel.Attack(attacker, -1, out message);
        if (Button("Cancel", 8, 416, 200, 30)) { ClearDuelSelection(); message = "Selection cleared."; }
        if (Button("Hand <", 8, 452, 98, 30, handPage > 0)) { handPage--; ClearDuelSelection(); }
        if (Button("Hand >", 112, 452, 98, 30, (handPage + 1) * 6 < duel.Hand.Count)) { handPage++; ClearDuelSelection(); }
        bool handInput = UpdateHandInteraction();
        if (hand >= 0 && hand < duel.Hand.Count) DrawArtwork(duel.Hand[hand], 8, 78, 200, 290);
        else if (attacker >= 0 && duel.Monsters[duel.ActivePlayer, attacker] is Card selectedMonster) DrawArtwork(selectedMonster, 8, 78, 200, 290);
        else Wrap("Hover a hand card. Right-click switches Summon/Activate to Set.", 12, 95, 195, 18);
        DrawDuelField(handInput);
        DrawDuelHand();
        var lines = TextLines(message, 205, 14);
        for (int i = 0; i < Math.Min(4, lines.Count); i++) Label(lines[i], 8, 492 + i * 17, 14, Gold);
        Label($"Deck {duel.DeckCount(0)}/{duel.DeckCount(1)} Grave {duel.Graveyard(0).Count}/{duel.Graveyard(1).Count}", 8, 568, 13);
        Label("Local demo | Right-click: Set", 8, 587, 12, Gold);
    }
}
