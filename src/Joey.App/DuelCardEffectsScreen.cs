using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Numerics;

static partial class Game
{
    abstract record CardEffect;
    sealed record Destruction(DemoMonsterDestruction Monster) : CardEffect;
    sealed record HandTransfer(DemoHandTransfer Transfer) : CardEffect;

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

    static void AdvanceCardEffects(TimeSpan elapsed)
    {
        if (cardEffects.Current is not { } current) return;
        int updates = cardEffectClock.Advance(elapsed, presentation.Style);
        // After a stall, leave the next card on its first sample for this draw.
        cardEffects.Advance(Math.Min(updates, current.Updates - cardEffects.ElapsedUpdates));
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
        }
    }

    static void DrawHandTransfer(DemoHandTransfer move)
    {
        bool near = move.Player == ViewPlayer;
        int count = VisibleHand(move.Player).Count;
        Rectangle handRect = near
            ? HandBoundsForCount(move.Index, count + (move.FromDeck ? 1 : 0))
            : CpuHandBounds(move.Index, count + (move.FromDeck ? 1 : 0));
        var handSlot = ToCardBounds(handRect);
        var pile = move.FromDeck ? NativeDuelLayout.DeckBounds(near) : NativeDuelLayout.GraveBounds(near);
        var bounds = CardTravel.Sample(move.FromDeck ? pile : handSlot, move.FromDeck ? handSlot : pile,
            cardEffects.ElapsedUpdates);
        DrawFieldCard(move.Card, ToRectangle(bounds), faceDown: !near || move.FromDeck);
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

    static void DrawExplosionSprite(ExplosionSpritePass? sample, CardPoint center)
    {
        if (sample is not { Opacity: > 0 } pass) return;
        var texture = graphics.Texture(DatGraphics.ExplosionSprites[pass.ResourceIndex]);
        int width = pass.ScaledPixels(texture.Width), height = pass.ScaledPixels(texture.Height);
        if (width == 0 || height == 0) return;
        Raylib.DrawTexturePro(texture, new Rectangle(0, 0, texture.Width, texture.Height),
            new Rectangle(center.X, center.Y, width, height), new Vector2(width / 2, height / 2),
            pass.RotationIndex * (360f / 512), new Color(255, 255, 255, pass.Opacity));
    }
}
