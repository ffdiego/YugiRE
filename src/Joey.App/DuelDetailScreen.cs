using Joey.Core;
using Raylib_cs;

static partial class Game
{
    static Card? detailCard;
    static Card? lastHoveredDetail;
    static bool activatedDetail;
    static int duelDescriptionScroll;

    static void ResetDuelDetail()
    {
        detailCard = null;
        duelDescriptionScroll = 0;
        lastHoveredDetail = null;
        activatedDetail = false;
    }

    static void ShowActivatedDetail(Card card)
    {
        detailCard = card;
        duelDescriptionScroll = 0;
        activatedDetail = true;
    }

    static void UpdateDuelDetail()
    {
        var hovered = FindHoveredDuelCard();
        if (!activatedDetail && hovered is not null && !ReferenceEquals(hovered, lastHoveredDetail))
        {
            detailCard = hovered;
            duelDescriptionScroll = 0;
        }
        lastHoveredDetail = hovered;
    }

    static Card? FindHoveredDuelCard()
    {
        int count = Math.Min(6, ViewHand.Count - handPage * 6);
        if (hoverHand >= 0 && hoverHand / 6 == handPage && hoverHand < ViewHand.Count &&
            Hit(HandBounds(hoverHand % 6)))
            return ViewHand[hoverHand];
        for (int slot = count - 1; slot >= 0; slot--)
            if (Hit(HandBounds(slot)))
                return ViewHand[handPage * 6 + slot];

        for (int player = 0; player < 2; player++)
        {
            bool own = player == ViewPlayer;
            if (Hit(FieldZoneBounds(player)) && (own || !duel.FieldSpellFaceDown[player]))
                return duel.FieldSpells[player];
            for (int zone = 0; zone < 5; zone++)
            {
                if (Hit(ZoneBounds(player, zone, false)) && (own || !duel.MonsterFaceDown[player, zone]))
                    return duel.Monsters[player, zone];
                if (Hit(ZoneBounds(player, zone, true)) && (own || !duel.SpellFaceDown[player, zone]))
                    return duel.Spells[player, zone];
            }
        }
        return null;
    }

    static void DrawDuelDetail()
    {
        activatedDetail = false;
        var background = graphics.Texture(DatGraphics.DuelDetailBackground);
        Raylib.DrawTexturePro(background, new Rectangle(0, 0, background.Width, background.Height),
            new Rectangle(0, 0, 214, 600), System.Numerics.Vector2.Zero, 0, Color.White);
        Raylib.DrawRectangle(8, 10, 198, 32, Panel);
        Raylib.DrawRectangle(8, 553, 198, 32, Panel);
        Label($"Opponent LP  {duel.Life[1 - ViewPlayer]}", 12, 16, 19, Gold);
        Label($"Your LP  {duel.Life[ViewPlayer]}", 12, 561, 19, Gold);
        if (detailCard is not { } card) return;

        DrawArtwork(card, 14, 53, 186, 265);
        string stats = card.IsMonster ? $"\nATK {card.Attack} / DEF {card.Defense}" : "";
        var lines = TextLines(card.Name + stats + "\n" + card.Description, 188, 13);
        Raylib.DrawRectangle(8, 321, 198, 229, Panel);
        const int visibleLines = 14;
        if (mouse.X < 214) duelDescriptionScroll -= (int)Raylib.GetMouseWheelMove() * 3;
        duelDescriptionScroll = Math.Clamp(duelDescriptionScroll, 0, Math.Max(0, lines.Count - visibleLines));
        for (int i = 0; i < visibleLines && i + duelDescriptionScroll < lines.Count; i++)
            Label(lines[i + duelDescriptionScroll], 14, 325 + i * 16, 13, i + duelDescriptionScroll == 0 ? Gold : Color.White);
    }
}
