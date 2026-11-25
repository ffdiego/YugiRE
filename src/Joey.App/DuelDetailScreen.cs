using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Numerics;

static partial class Game
{
    static Card? detailCard;
    static bool detailShowsBack = true;
    static Card? lastHoveredDetail;
    static bool lastHoveredWasBack;
    static bool hadHoveredTarget;
    static bool activatedDetail;
    static int duelDescriptionScroll;
    static readonly NativeLifePointPresentation[] lifeDisplays = [new(8000), new(8000)];
    static readonly PresentationTickClock lifeClock = new();

    static void AdvanceLifePresentation(TimeSpan elapsed)
    {
        // Core updates LP immediately, but its presentation must not start
        // while a spell is still activating or an attack/flip/destruction
        // animation is pending. In particular, never reveal damage while the
        // sword is travelling. The LP catch-up is itself a motion barrier to
        // keep CPU/turn actions from starting another move mid-number.
        if (CardMotionBusy && presentation.Style.PlayAnimations) return;
        for (int player = 0; player < lifeDisplays.Length; player++)
        {
            long previous=(long)lifeDisplays[player].Displayed+lifeDisplays[player].PendingDelta;
            int next=NativeLifePointPresentation.ClampToNativeDisplay(duel.Life[player]);
            if(previous!=next)PlayDuelSound(next<previous?DuelSoundCue.Damage:DuelSoundCue.Heal);
            lifeDisplays[player].SetLogicalLife(duel.Life[player], presentation.Style.PlayAnimations);
        }
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
        detailShowsBack = true; // The sidebar always starts with the DAT back.
        lastHoveredDetail = null;
        lastHoveredWasBack = false;
        hadHoveredTarget = false;
        duelDescriptionScroll = 0;
        activatedDetail = false;
    }

    static void ShowPlayedCardDetail(Card card, bool hiddenFromPlayer = false)
    {
        // Set cards belonging to the CPU are unknown to the human. Show
        // the back, not a Card object, even though Core has its identity.
        detailCard = hiddenFromPlayer ? null : card;
        detailShowsBack = hiddenFromPlayer;
        duelDescriptionScroll = 0;
        activatedDetail = true;
    }

    static void ShowActivatedDetail(Card card) => ShowPlayedCardDetail(card);

    static void UpdateDuelDetail()
    {
        // No object beneath the pointer is NOT a request to clear the panel.
        // Keep the last inspected face or back until another valid card
        // (including an unknown opponent card) is hovered.
        var target = FindHoveredDuelCard();
        bool enteredNewCard = target is { } hovering &&
            (!hadHoveredTarget ||
             !ReferenceEquals(hovering.Card, lastHoveredDetail) ||
             hovering.Back != lastHoveredWasBack);
        // A played card becomes the sidebar focus even if the pointer has
        // not moved from its previous card. Only a NEW hover takes over.
        hadHoveredTarget = target.HasValue;
        lastHoveredDetail = target?.Card;
        lastHoveredWasBack = target?.Back ?? false;
        if (activatedDetail || !enteredNewCard || target is not { } hovered) return;
        detailCard = hovered.Card;
        detailShowsBack = hovered.Back;
        duelDescriptionScroll = 0;
    }

    // A known card is available for the detail panel; an opponent's hidden
    // card is represented exclusively as a back; null means no hit at all.
    static (Card? Card, bool Back)? FindHoveredDuelCard()
    {
        var near = VisibleHand(ViewPlayer);
        for (int slot = near.Count - 1; slot >= 0; slot--)
        {
            if (IncomingTransferInFlight(ViewPlayer, slot, near[slot])) continue;
            if (Hit(HandBounds(slot))) return (near[slot], false);
        }

        if (alphaCpu is not null)
        {
            int opponent = 1 - ViewPlayer;
            var far = VisibleHand(opponent);
            if (hoverCpuHand >= 0 && hoverCpuHand < far.Count &&
                !IncomingTransferInFlight(opponent, hoverCpuHand, far[hoverCpuHand]))
                return (null, true);
        }

        for (int player = 0; player < 2; player++)
        {
            bool own = player == ViewPlayer;
            if (duel.FieldSpells[player] is Card fieldCard && Hit(FieldZoneBounds(player)))
                return !own && duel.FieldSpellFaceDown[player]
                    ? (null, true) : (fieldCard, false);
            for (int zone = 0; zone < 5; zone++)
            {
                if (Hit(ZoneBounds(player, zone, false)) &&
                    IsHiddenAttackTargetBeforeImpact(player, zone))
                    return (null, true);
                if (duel.Monsters[player, zone] is Card monster && !FusionResultHidden(player,zone) &&
                    Hit(ZoneBounds(player, zone, false)))
                    return !own && duel.MonsterFaceDown[player, zone]
                        ? (null, true) : (monster, false);
                if (duel.Spells[player, zone] is Card spell &&
                    Hit(ZoneBounds(player, zone, true)))
                    return !own && duel.SpellFaceDown[player, zone]
                        ? (null, true) : (spell, false);
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

        var font = graphics.Texture(graphics.LifeNumberSprite);
        if (font.Width % columns != 0)
        {
            Label(displayed.ToString(), 74, y, 19, Gold);
            return;
        }

        // The DAT's fontl atlas has ten 8x12 decimal glyphs. Draw each
        // at a whole-number multiplier (3x), not fractional 21.33x32,
        // to preserve the native hard pixel edges.
        int sourceWidth = NativeLifeHud.GlyphWidth(font.Width, columns);
        int glyphWidth = NativeLifeHud.DisplayGlyphWidth(font.Width, font.Height, columns);
        int glyphHeight = NativeLifeHud.DisplayHeightForAtlas(font.Height);
        int[] digits = NativeLifeHud.DecimalGlyphs(displayed);
        int x = NativeLifeHud.DisplayRightX - digits.Length * glyphWidth;
        foreach (int digit in digits)
        {
            Raylib.DrawTexturePro(font,
                new Rectangle(digit * sourceWidth, 0, sourceWidth, font.Height),
                new Rectangle(x, y, glyphWidth, glyphHeight),
                Vector2.Zero, 0, Color.White);
            x += glyphWidth;
        }
    }

    static void DrawLifeFloatingDigits()
    {
        if (!presentation.Style.PlayAnimations) return;
        // Draw 0047C660 invokes 004806F0 while the signed delta remains.
        // Use the actual 10..12 glyph atlas descriptor from number.txt.
        for (int player = 0; player < 2; player++)
        {
            var counter = lifeDisplays[player];
            if (counter.PendingDelta == 0) continue;
            int variant = counter.PendingDelta > 0 ? 1 : 0;
            if (graphics.LifeFloatingGlyphColumns[variant] is not int columns) continue;
            var texture = graphics.Texture(DatGraphics.LifeFloatingFonts[variant]);
            if (texture.Width % columns != 0) continue;

            int sourceGlyphWidth = NativeLifeHud.GlyphWidth(texture.Width, columns);
            int sourceGlyphHeight = texture.Height;
            // Actual number.txt: damage00/01.bmp each have 12 glyphs in a
            // 192x40 atlas, so preserve the 16x40 source samples. The 32px
            // height cap was an invented resize, not a verified native rule.
            int glyphWidth = sourceGlyphWidth, glyphHeight = sourceGlyphHeight;
            int phase = Math.Min(0x100, counter.RampCounter * 2);
            var glyphs = NativeFloatingLifeDigits.Sample(counter.PendingDelta, phase,
                glyphWidth, glyphHeight, columns, effectCosine);
            bool near = player == ViewPlayer;
            // 0047C660 resolves the slot (or central spell slot for -1),
            // then centers the FULL signed number, not its right edge.
            // Legacy logical-life input supplies -1; the scheduler adapter
            // can supply a battle slot through RequestNativeChange later.
            var anchor = NativeLifeHud.FloatingNumberAnchor(near, counter.AnchorSlot);
            var origin = NativeLifeHud.FloatingNumberOrigin(anchor, counter.PendingDelta,
                glyphWidth, glyphHeight, columns);
            int right = origin.X, top = origin.Y;
            foreach (var glyph in glyphs)
                Raylib.DrawTexturePro(texture,
                    new Rectangle(glyph.GlyphIndex * sourceGlyphWidth, 0,
                                  sourceGlyphWidth, sourceGlyphHeight),
                    new Rectangle(right + glyph.XOffsetPixels, top + glyph.YOffsetPixels,
                                  glyphWidth, glyphHeight),
                    Vector2.Zero, 0, Color.White);
        }
    }

    static void DrawDuelDetail()
    {
        activatedDetail = false;

        // detail/detail.bmp has already been drawn as the background in
        // DuelScreen. Do NOT cover its native LP top/bottom frames or card
        // description area with solid raylib Panel rectangles.
        DrawLifeBar(1 - ViewPlayer, near: false);
        DrawLifeBar(ViewPlayer, near: true);
        DrawLifeDigits(1 - ViewPlayer, NativeLifeHud.DisplayTopY);
        DrawLifeDigits(ViewPlayer, NativeLifeHud.DisplayBottomY);
        if (detailShowsBack)
        {
            var bounds = NativeDetailPanel.PreviewBounds;
            DrawCardBack(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            return; // No identity, ATK/DEF, name or description for CPU cards.
        }
        if (detailCard is not { } card) return;

        var preview = NativeDetailPanel.PreviewBounds;
        DrawArtwork(card, preview.X, preview.Y, preview.Width, preview.Height);

        // 0041B3E0 builds native rich text: name, optional monster ATK/DEF,
        // description; 0041BE80 supplies the actual 184px text viewport.
        // English font GDI face comes from 0040F650 / 00544B80.
        const int size = NativeDetailPanel.EnglishTextSize;
        Font? normalFont = duelTheme.DetailTextFont(bold: false);
        Font? titleFont = duelTheme.DetailTextFont(bold: true);
        int Measure(string value) =>
            normalFont is Font font
                ? (int)MathF.Ceiling(Raylib.MeasureTextEx(font, value, size, 0).X)
                : Raylib.MeasureText(value, size);

        string? stats = card.IsMonster
            ? $"ATK {card.Attack} / DEF {card.Defense}" : null;
        var lines = NativeDetailPanel.Wrap(card.Name, stats, card.Description, Measure);
        int visibleLines = NativeDetailPanel.VisibleLineCount();
        if (mouse.X >= NativeDetailPanel.TextX &&
            mouse.X < NativeDetailPanel.TextX + NativeDetailPanel.TextWidth &&
            mouse.Y >= NativeDetailPanel.TextY &&
            mouse.Y < NativeDetailPanel.TextBottom)
            duelDescriptionScroll -= (int)Raylib.GetMouseWheelMove() * 3;
        duelDescriptionScroll = NativeDetailPanel.ClampScroll(
            duelDescriptionScroll, lines.Count, visibleLines);

        // The native panel is clipped; unbounded text should never cover
        // the LP bar, card image or adjacent duel field.
        Raylib.BeginScissorMode(NativeDetailPanel.TextX,
            NativeDetailPanel.TextY, NativeDetailPanel.TextWidth,
            NativeDetailPanel.TextHeight);
        for (int i = 0; i < visibleLines && i + duelDescriptionScroll < lines.Count; i++)
        {
            var line = lines[i + duelDescriptionScroll];
            uint sourceColor = line.Kind switch
            {
                NativeDetailPanel.LineKind.Name => NativeDetailPanel.NameColorRef,
                NativeDetailPanel.LineKind.Stats => NativeDetailPanel.StatsColorRef,
                _ => NativeDetailPanel.DescriptionColorRef
            };
            var (r, g, b) = NativeDetailPanel.DecodeColorRef(sourceColor);
            var color = new Color((int)r, (int)g, (int)b, 255);
            int y = NativeDetailPanel.TextY + i * NativeDetailPanel.EnglishLineAdvance;
            Font? font = line.Kind == NativeDetailPanel.LineKind.Name ? titleFont : normalFont;
            if (font is Font loaded)
                Raylib.DrawTextEx(loaded, line.Value,
                    new Vector2(NativeDetailPanel.TextX, y), size, 0, color);
            else
                Raylib.DrawText(line.Value, NativeDetailPanel.TextX, y, size, color);
        }
        Raylib.EndScissorMode();
    }
}
