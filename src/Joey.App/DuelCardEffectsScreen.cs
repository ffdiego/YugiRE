using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;

static partial class Game
{
    abstract record CardEffect;
    sealed record Destruction(DemoMonsterDestruction Monster) : CardEffect;
    sealed record HandTransfer(DemoHandTransfer Transfer) : CardEffect;
    sealed record SetPlacement(int Player, int Zone, bool Spell, CardPoint[] ParticleOrigins) : CardEffect;

    static readonly PresentationSequence<CardEffect> cardEffects = new();
    static readonly PresentationTickClock cardEffectClock = new();
    static bool CardEffectsBusy => cardEffects.Current is not null;
    static bool CanPlayCardEffects => flight is null && attackCue is null && !ActivationBusy;
    static bool MotionBusy => flight is not null || attackCue is not null || ActivationBusy || CardEffectsBusy;

    static void ResetCardEffects()
    {
        cardEffects.Clear();
        cardEffectClock.Reset();
        duel.MonsterDestroyed += monster => QueueCardEffect(new Destruction(monster), NativeExplosionTimeline.Updates);
        duel.HandTransferred += transfer => QueueCardEffect(new HandTransfer(transfer), CardTravel.Updates);
    }

    static void QueueCardEffect(CardEffect effect, int updates)
    {
        if (!presentation.Style.PlayAnimations) return;
        if (!CardEffectsBusy) cardEffectClock.Reset();
        cardEffects.Enqueue(effect, updates);
    }

    static void QueueSetEffect(int player, int zone, bool spell)
    {
        if (!presentation.Style.PlayAnimations) return;
        // The card is already committed to the Core; Set runs after flight.
        // Constructor 0044EFD0 takes eight 5-bit MT draws. Reproduce its
        // distribution, but not its shared native RNG stream/seed.
        Span<int> radii = stackalloc int[NativeSetFrame.ParticleCount];
        for (int i = 0; i < radii.Length; i++)
            radii[i] = Random.Shared.Next(NativeSetFrame.MinimumRadius,
                                           NativeSetFrame.MaximumRadius + 1);
        var center = DuelCardLayout.Center(spell ? DuelCardZone.Spell : DuelCardZone.Monster,
                                          player == ViewPlayer, zone);
        var origins = NativeSetFrame.ParticleOrigins(center, radii, effectCosine);
        QueueCardEffect(new SetPlacement(player, zone, spell, origins), NativeSetFrame.Updates);
    }

    static void QueueOpeningDeal()
    {
        var near = duel.HandFor(0);
        var far = duel.HandFor(1);
        for (int index = 0; index < Math.Max(near.Count, far.Count); index++)
        {
            if (index < near.Count) QueueCardEffect(new HandTransfer(new(0, index, near[index], true)), CardTravel.Updates);
            if (index < far.Count) QueueCardEffect(new HandTransfer(new(1, index, far[index], true)), CardTravel.Updates);
        }
    }

    static void AdvanceCardEffects(TimeSpan elapsed)
    {
        if (cardEffects.Current is not { } current) return;
        int updates = cardEffectClock.Advance(elapsed, presentation.Style);
        // After a stall, leave the next card on its first sample for this draw.
        int remaining = current.Updates - cardEffects.ElapsedUpdates;
        if (updates >= remaining && current.Kind is HandTransfer { Transfer: { FromDeck: false } move })
            handLayouts[move.Player].RemoveAt(move.Index);
        cardEffects.Advance(Math.Min(updates, remaining));
    }

    static IReadOnlyList<Card> VisibleHand(int player)
    {
        if (!CardEffectsBusy) return duel.HandFor(player);

        // Undo outstanding transfers on a copy of the committed hand. Each
        // completed step becomes visible as soon as it leaves the queue.
        List<Card> visible = [.. duel.HandFor(player)];
        foreach (var step in cardEffects.Pending.Reverse())
        {
            if (step.Kind is not HandTransfer { Transfer: var move } || move.Player != player) continue;
            if (move.FromDeck)
                visible.RemoveAt(move.Index);
            else
                visible.Insert(move.Index, move.Card);
        }
        return visible;
    }

    static int VisibleGraveCount(int player) => Math.Max(0, duel.Graveyard(player).Count -
        cardEffects.Pending.Count(step => step.Kind is Destruction { Monster.Player: var owner } && owner == player ||
            step.Kind is HandTransfer { Transfer: { Player: var side, FromDeck: false } } && side == player));

    static int VisibleDeckCount(int player) => duel.DeckCount(player) +
        cardEffects.Pending.Count(step => step.Kind is HandTransfer { Transfer: { Player: var side, FromDeck: true } } &&
            side == player);

    static Rectangle ToRectangle(CardBounds bounds) => new(bounds.X, bounds.Y, bounds.Width, bounds.Height);

    static void DrawSidePiles()
    {
        for (int player = 0; player < 2; player++)
        {
            bool near = player == ViewPlayer;
            DrawPile(NativeDuelLayout.GraveBounds(near), VisibleGraveCount(player), "GRAVE");
            DrawPile(NativeDuelLayout.DeckBounds(near), VisibleDeckCount(player), "DECK");
        }
    }

    static void DrawPile(CardBounds bounds, int count, string label)
    {
        if (duelDebug) Raylib.DrawRectangleLinesEx(ToRectangle(bounds), 1, Color.Gray);
        if (count > 0) DrawCardBack(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        Label($"{label} {count}", bounds.X + 6, bounds.Y + bounds.Height + 2, 10, Gold);
    }

    static bool DiscardInFlight(int player, int index) =>
        CanPlayCardEffects &&
        cardEffects.Current?.Kind is HandTransfer { Transfer: { FromDeck: false } move } &&
        move.Player == player && move.Index == index;

    static void DrawCardEffects()
    {
        bool playing = CanPlayCardEffects;
        // Deferred removals stay beneath the active effect, even in adjacent zones.
        foreach (var step in cardEffects.Pending.Skip(playing ? 1 : 0))
            if (step.Kind is Destruction { Monster: var queued }) DrawDestructionCard(queued);

        if (!playing) return;
        switch (cardEffects.Current?.Kind)
        {
            case Destruction { Monster: var monster }:
                DrawDestruction(monster, cardEffects.ElapsedUpdates);
                break;
            case HandTransfer { Transfer: var transfer }:
                DrawHandTransfer(transfer);
                break;
            case SetPlacement placement:
                DrawSetPlacement(placement, cardEffects.ElapsedUpdates);
                break;
        }
    }

    static void DrawSetPlacement(SetPlacement placement, int elapsedUpdates)
    {
        bool near = placement.Player == ViewPlayer;
        var zone = placement.Spell ? DuelCardZone.Spell : DuelCardZone.Monster;
        var center = DuelCardLayout.Center(zone, near, placement.Zone);
        var frame = NativeSetFrame.FromUpdate(elapsedUpdates, effectCosine);
        // Constructor 0044EFD0 uses 00402690: flag is monster-type 1..20,
        // NOT the player's side. Draw 0044F390 selects indices 2/3 for
        // monsters, 4/5 for spell/trap, alternating every 8 counter units.
        // Manifest order is still inferred from original DAT resource names.
        if (frame.MainOpacity <= 0) return;
        DrawEffectStamp(placement.Spell ? DatGraphics.SetAuraYellow : DatGraphics.SetAuraBlue,
            center, 0, frame.ScaleFixed16, frame.MainOpacity);
        foreach (var particle in frame.Particles)
        {
            var origin = placement.ParticleOrigins[particle.Sequence];
            var sprite = graphics.Texture(DatGraphics.SetSprites[
                2 + (placement.Spell ? 2 : 0) + particle.Variant]);
            Raylib.DrawTexture(sprite, origin.X - sprite.Width / 2,
                origin.Y - particle.RisePixels - sprite.Height / 2,
                new Color(255, 255, 255, frame.MainOpacity));
        }
        // Exact MT draw sequence, original 512-element cosine table,
        // BMP manifest order and the native scratch blend await comparison.
    }

    static void DrawHandTransfer(DemoHandTransfer move)
    {
        bool near = move.Player == ViewPlayer;
        int count = VisibleHand(move.Player).Count;
        var handSlot = move.FromDeck
            ? HandPresentation.Bounds(move.Index, count + 1, near)
            : handLayouts[move.Player].Bounds(move.Index, count, near ? HandLiftAt(move.Index) : 0);
        var pile = move.FromDeck ? NativeDuelLayout.DeckBounds(near) : NativeDuelLayout.GraveBounds(near);
        var bounds = CardTravel.Sample(move.FromDeck ? pile : handSlot, move.FromDeck ? handSlot : pile,
            cardEffects.ElapsedUpdates);
        DrawFieldCard(move.Card, ToRectangle(bounds), faceDown: !near || move.FromDeck, upsideDown: !near);
    }

    static void DrawDestruction(DemoMonsterDestruction monster, int elapsedUpdates)
    {
        bool near = monster.Player == ViewPlayer;
        var center = DuelCardLayout.Center(DuelCardZone.Monster, near, monster.Zone);
        int counter = elapsedUpdates * NativeExplosionTimeline.UpdateIncrement;
        if (counter < 0x80)
            DrawDestructionCard(monster);

        var frame = NativeExplosionFrame.FromCounter(counter, effectCosine);
        DrawExplosionSprite(frame.Flare, center);
        DrawExplosionSprite(frame.Fragments, center);
        DrawExplosionSprite(frame.Initial, center);
    }

    static void DrawDestructionCard(DemoMonsterDestruction monster) =>
        DrawDuelCard(monster.Card, DuelCardLayout.OnBoard(DuelCardZone.Monster, monster.Player == ViewPlayer,
            monster.Zone, monster.Defense, monster.FaceDown));

    static void DrawExplosionSprite(NativeEffectSpritePass? sample, CardPoint center)
    {
        if (sample is not { Opacity: > 0 } pass) return;
        DrawEffectStamp(DatGraphics.ExplosionSprites[pass.ResourceIndex], center,
            pass.RotationIndex, pass.ScaleFixed16, pass.Opacity);
    }
}
