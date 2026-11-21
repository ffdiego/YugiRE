using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;

static partial class Game
{
    static Card? detailCard;
    static Card? lastHoveredDetail;
    static bool activatedDetail;
    static int duelDescriptionScroll;
    static readonly NativeLifePointPresentation[] lifeDisplays = [new(8000), new(8000)];
    static readonly PresentationTickClock lifeClock = new();

    static void AdvanceLifePresentation(TimeSpan elapsed)
    {
        // Read logical LP after all Core actions for this frame. The original
        // LP object is independent of the logical duel; presentation only.
        for (int player = 0; player < lifeDisplays.Length; player++)
            lifeDisplays[player].SetLogicalLife(duel.Life[player], presentation.Style.PlayAnimations);
        int updates = lifeClock.Advance(elapsed, presentation.Style);
        for (int player = 0; player < lifeDisplays.Length; player++)
            for (int update = 0; update < updates && lifeDisplays[player].Animating; update++)
                lifeDisplays[player].AdvanceNativeUpdate();
    }

    static void ResetDuelDetail()
    {
        lifeClock.Reset();
        for (int player = 0; player < lifeDisplays.Length; player++)
            lifeDisplays[player].Reset(duel.Life[player]);
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
        int count = ViewHand.Count;
        if (hoverHand >= 0 && hoverHand < ViewHand.Count &&
            Hit(HandBounds(hoverHand)))
            return ViewHand[hoverHand];
        for (int slot = count - 1; slot >= 0; slot--)
            if (Hit(HandBounds(slot)))
                return ViewHand[slot];

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
        Raylib.DrawRectangle(62, 4, 137, 28, Panel);
        Raylib.DrawRectangle(62, 552, 137, 28, Panel);
        Label(lifeDisplays[1 - ViewPlayer].Displayed.ToString(), 74, 9, 19, Gold);
        Label(lifeDisplays[ViewPlayer].Displayed.ToString(), 74, 557, 19, Gold);
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
