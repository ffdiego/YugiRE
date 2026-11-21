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
        Raylib.DrawRectangleLinesEx(ToRectangle(bounds), 1, Color.Gray);
        if (count > 0) DrawCardBack(bounds.X + 8, bounds.Y + 3, 50, 68);
        Label($"{label} {count}", bounds.X + 6, bounds.Y + 74, 10, Gold);
    }

    static bool DiscardInFlight(int player, int index) =>
        CanPlayCardEffects &&
        cardEffects.Current?.Kind is HandTransfer { Transfer: { FromDeck: false } move } &&
        move.Player == player && move.Index == index;

    static void DrawCardEffects()
    {
        bool first = true;
        bool playing = CanPlayCardEffects;
        foreach (var step in cardEffects.Pending)
        {
            if (step.Kind is Destruction { Monster: var monster })
                DrawDestruction(monster, first && playing ? cardEffects.ElapsedUpdates : 0);
            else if (first && playing && step.Kind is HandTransfer { Transfer: var transfer })
                DrawHandTransfer(transfer);
            first = false;
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
        var rect = ZoneBounds(monster.Player, monster.Zone, false);
        var center = new Vector2(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        int counter = elapsedUpdates * NativeExplosionTimeline.UpdateIncrement;
        var passes = NativeExplosionTimeline.PassesAt(counter);
        if ((passes & NativeExplosionPasses.Initial) != 0)
            DrawDuelCard(monster.Card, DuelCardLayout.OnBoard(DuelCardZone.Monster, monster.Player == ViewPlayer,
                monster.Zone, monster.Defense, monster.FaceDown));
        if ((passes & NativeExplosionPasses.Flare) != 0)
        {
            int alpha = Math.Clamp(235 - 3 * Math.Abs(counter - 88), 0, 235);
            Raylib.DrawCircleV(center, 8 + counter / 6f, new Color(255, 220, 151, alpha));
            Raylib.DrawCircleLines((int)center.X, (int)center.Y, counter / 3f, new Color(255, 246, 218, alpha));
        }
        if ((passes & NativeExplosionPasses.Fragments) == 0) return;
        // Procedural fragments use the native draw window, with client-selected shapes and trajectories.
        float progress = (counter - 128) / 80f;
        int opacity = (int)(255 * (1 - progress));
        for (int i = 0; i < 24; i++)
        {
            float angle = i * 2.399963f;
            float distance = (9 + progress * 48) * (0.5f + (i % 7) / 10f);
            Vector2 point = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
            Raylib.DrawRectangle((int)point.X, (int)point.Y, 2 + i % 4, 2 + i % 3,
                new Color(255, 185 + i % 65, 125 + i % 100, opacity));
        }
    }
}
