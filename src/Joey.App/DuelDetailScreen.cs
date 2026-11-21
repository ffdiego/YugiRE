using Joey.Core;

static partial class Game
{
    static Card? detailCard;
    static Card? lastHoveredDetail;
    static bool activatedDetail;

    static void ResetDuelDetail()
    {
        detailCard = null;
        lastHoveredDetail = null;
        activatedDetail = false;
    }

    static void ShowActivatedDetail(Card card)
    {
        detailCard = card;
        activatedDetail = true;
    }

    static void UpdateDuelDetail()
    {
        var hovered = FindHoveredDuelCard();
        if (!activatedDetail && hovered is not null && !ReferenceEquals(hovered, lastHoveredDetail))
            detailCard = hovered;
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
        if (detailCard is not { } card)
        {
            Wrap("Hover a card to inspect it. Right-click switches Summon/Activate to Set.", 12, 95, 195, 18);
            return;
        }

        DrawArtwork(card, 8, 78, 200, 178);
        string stats = card.IsMonster ? $"\nATK {card.Attack} / DEF {card.Defense}" : "";
        var lines = TextLines(card.Name + stats + "\n" + card.Description, 205, 12);
        for (int i = 0; i < Math.Min(8, lines.Count); i++)
            Label(lines[i], 8, 260 + i * 14, 12, i == 0 ? Gold : Raylib_cs.Color.White);
    }
}
