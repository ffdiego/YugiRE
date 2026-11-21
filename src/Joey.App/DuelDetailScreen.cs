using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Numerics;

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

    static void DrawLifeBar(int player, bool near)
    {
        // 0047C660: bar length = floor(min(displayed, 8000) * width / 8000).
        // The actual native image placement and pass order need frame comparison.
        var baseSprite = graphics.Texture(near ? DatGraphics.LifeBackgroundNear : DatGraphics.LifeBackgroundFar);
        var fillSprite = graphics.Texture(near ? DatGraphics.LifeFillNear : DatGraphics.LifeFillFar);
        const int x = 62, width = 137, height = 14;
        int y = near ? 582 : 36;
        Raylib.DrawTexturePro(baseSprite, new Rectangle(0, 0, baseSprite.Width, baseSprite.Height),
            new Rectangle(x, y, width, height), Vector2.Zero, 0, Color.White);
        int filled = NativeLifeHud.FilledBarPixels(lifeDisplays[player].Displayed, width);
        if (filled <= 0) return;

        // Crop both source and destination: do not stretch a full native
        // bitmap into a shorter filled bar.
        float sourceWidth = fillSprite.Width * filled / (float)width;
        Raylib.DrawTexturePro(fillSprite, new Rectangle(0, 0, sourceWidth, fillSprite.Height),
            new Rectangle(x, y, filled, height), Vector2.Zero, 0, Color.White);
    }

    static void DrawLifeDigits(int player, int y)
    {
        int displayed = lifeDisplays[player].Displayed;
        if (graphics.LifeNumberGlyphColumns is not int columns)
        {
            // Unknown number.txt descriptor: keep the working legacy HUD.
            Label(displayed.ToString(), 74, y, 19, Gold);
            return;
        }

        var font = graphics.Texture(DatGraphics.LifeNumberFont);
        if (font.Width % columns != 0)
        {
            Label(displayed.ToString(), 74, y, 19, Gold);
            return;
        }

        // 004803C0: a horizontal atlas, decimal digits chosen from low to
        // high while drawing right-to-left. Here positions and color are
        // provisional; source glyph slicing follows number.txt.
        int glyphWidth = NativeLifeHud.GlyphWidth(font.Width, columns);
        int[] digits = NativeLifeHud.DecimalGlyphs(displayed);
        float height = Math.Min(19, font.Height);
        float width = glyphWidth * height / font.Height;
        float x = 188 - digits.Length * width;
        float top = y + (19 - height) / 2;
        foreach (int digit in digits)
        {
            Raylib.DrawTexturePro(font,
                new Rectangle(digit * glyphWidth, 0, glyphWidth, font.Height),
                new Rectangle(x, top, width, height), Vector2.Zero, 0, Color.White);
            x += width;
        }
    }

    static void DrawDuelDetail()
    {
        activatedDetail = false;
        Raylib.DrawRectangle(62, 4, 137, 28, Panel);
        Raylib.DrawRectangle(62, 552, 137, 28, Panel);
        DrawLifeBar(1 - ViewPlayer, near: false);
        DrawLifeBar(ViewPlayer, near: true);
        DrawLifeDigits(1 - ViewPlayer, 9);
        DrawLifeDigits(ViewPlayer, 557);
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
