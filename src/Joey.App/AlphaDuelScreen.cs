using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Numerics;

static partial class Game
{
    static CpuDuelController? alphaCpu;
    static bool alphaBlocked;
    static TimeSpan cpuDelay;
    static string alphaError = "";
    static int ViewPlayer => alphaCpu is null ? duel.ActivePlayer : 0;
    static IReadOnlyList<Card> ViewHand => alphaCpu is null ? duel.Hand : duel.HandFor(0);
    static bool HumanChoice => !duel.GameOver && (alphaCpu is null || CpuDuelController.PendingOwner(duel) == 0);
    static bool CanPlayTurn => !duel.GameOver && (alphaCpu is null || duel.ActivePlayer == 0);
    static bool CanPlayMainOrBattle => CanPlayTurn && (alphaCpu is null || !duel.CanDraw);

    sealed record CardFlight(Card Card, bool Hidden, bool Defense, int Player, int Zone, bool Spell,
        Rectangle Origin, Rectangle Destination, TimeSpan Duration);
    static CardFlight? flight;
    static TimeSpan travelElapsed;
    sealed record AttackCue(Vector2 Origin, Vector2 Target, TimeSpan Duration);
    static AttackCue? attackCue;
    static TimeSpan attackElapsed;

    static void StartAlpha()
    {
        try
        {
            var deck = AlphaDeck.Create(cards);
            duel = DemoDuel.FromDecks(deck, deck, Random.Shared.Next());
        }
        catch (InvalidDataException ex)
        {
            alphaError = ex.Message;
            return;
        }
        alphaError = "";
        alphaCpu = new(1);
        alphaBlocked = false;
        cpuDelay = TimeSpan.Zero;
        ResetDuelPresentation();
        message = "Your turn. Draw a card to start.";
    }

    static int RunAlphaHeadless()
    {
        const int seed = 1979;
        var deck = AlphaDeck.Create(cards);
        var match = DemoDuel.FromDecks(deck, deck, seed);
        CpuDuelController[] sides = [new(0), new(1)];
        for (int i = 0; i < 5000 && !match.GameOver; i++)
        {
            int owner = CpuDuelController.PendingOwner(match) ?? match.ActivePlayer;
            var step = sides[owner].Step(match);
            Console.WriteLine($"Turn {match.Turn} | P{owner + 1} | {step.Message}");
            if (step.Status != CpuStepStatus.Acted) return 1;
        }
        string result = match.IsDraw ? "Draw" : match.Winner is int winner ? $"P{winner + 1} wins" : "Action limit reached";
        Console.WriteLine($"{result} | Turn {match.Turn} | LP {match.Life[0]}/{match.Life[1]} | Deck {match.DeckCount(0)}/{match.DeckCount(1)} | Seed {seed}");
        return match.GameOver ? 0 : 1;
    }

    static void ResetDuelPresentation()
    {
        screen = "duel";
        handPage = 0;
        ClearDuelSelection();
        spellActivation.Stop();
        flight = null;
        attackCue = null;
        presentation.Clear();
        previousDuelMessage = "";
    }

    static Rectangle ZoneBounds(int player, int zone, bool spell)
    {
        bool near = player == ViewPlayer;
        var native = NativeDuelLayout.MonsterBounds(zone, near);
        return new(native.X, spell ? near ? 416 : 96 : native.Y, native.Width, native.Height);
    }

    static void BeginFlight(Card card, int player, int zone, Rectangle origin, bool spell, bool hidden)
    {
        if (alphaCpu is null || !presentation.Style.PlayAnimations) return;
        travelElapsed = TimeSpan.Zero;
        var duration = presentation.Style.Duration(DuelPresentationCue.CardMove);
        flight = new(card, hidden, !spell && hidden, player, zone, spell, origin,
            ZoneBounds(player, zone, spell), duration);
    }

    static bool InFlight(int player, int zone, bool spell) =>
        flight is { } f && (f.Player, f.Zone, f.Spell) == (player, zone, spell);

    static void DrawFlight()
    {
        if (flight is not { } f) return;
        static CardBounds Bounds(Rectangle rect) => new((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
        int update = Math.Clamp((int)(travelElapsed.TotalSeconds / f.Duration.TotalSeconds * CardTravel.Updates), 0, CardTravel.Updates);
        var cell = CardTravel.Sample(Bounds(f.Origin), Bounds(f.Destination), update);
        DrawFieldCard(f.Card, new(cell.X, cell.Y, cell.Width, cell.Height), f.Hidden, f.Defense);
    }

    static void BeginAttackCue(int player, int source, int target)
    {
        if (alphaCpu is null || !presentation.Style.PlayAnimations) return;
        bool near = player == ViewPlayer;
        var from = NativeDuelLayout.MonsterCenter(source, near);
        var to = target >= 0 ? NativeDuelLayout.MonsterCenter(target, !near) : new CardPoint(525, near ? 144 : 459);
        attackElapsed = TimeSpan.Zero;
        attackCue = new(new(from.X, from.Y), new(to.X, to.Y), presentation.Style.Duration(DuelPresentationCue.Battle) / 2);
    }

    static void DrawAttackCue()
    {
        if (attackCue is not { } cue) return;
        float fraction = (float)(attackElapsed / cue.Duration);
        Vector2 head = Vector2.Lerp(cue.Origin, cue.Target, fraction);
        Vector2 tail = Vector2.Lerp(cue.Origin, cue.Target, Math.Max(0, fraction - .3f));
        Raylib.DrawLineEx(tail, head, 6, Gold);
        Raylib.DrawCircleV(head, 6, Color.White);
    }

    static void CommitHumanAttack(int target)
    {
        int player = duel.ActivePlayer;
        if (duel.Attack(attacker, target, out message)) BeginAttackCue(player, attacker, target);
    }

    static void AdvanceAlpha(TimeSpan elapsed)
    {
        attackElapsed += elapsed;
        if (attackCue is { } attack && attackElapsed >= attack.Duration) attackCue = null;
        travelElapsed += elapsed;
        if (flight is { } move && travelElapsed >= move.Duration) flight = null;
        if (alphaCpu is not { } cpu || alphaBlocked || duel.GameOver || flight is not null || spellActivation.IsPlaying) return;
        cpuDelay -= elapsed;
        if (cpuDelay > TimeSpan.Zero) return;
        int? owner = CpuDuelController.PendingOwner(duel);
        if ((owner ?? duel.ActivePlayer) != cpu.Player) return;
        int handCount = duel.HandFor(cpu.Player).Count;
        var step = cpu.Step(duel);
        if (step.Status == CpuStepStatus.Blocked)
        {
            message = step.Message;
            alphaBlocked = true;
            return;
        }
        if (step.Status != CpuStepStatus.Acted) return;
        cpuDelay = presentation.Style.Duration(step.Action == CpuAction.Attack ? DuelPresentationCue.Battle : DuelPresentationCue.CardMove);
        message = "CPU: " + (step.Action == CpuAction.BeginBattle ? "Battle phase." : step.Message);
        ClearDuelSelection();
        if (step.Action == CpuAction.Attack && step.SourceZone is int attackerZone && step.TargetZone is int targetZone)
            BeginAttackCue(step.Player, attackerZone, targetZone);
        if (step.Card is not Card card) return;
        if (step.Action is CpuAction.Set or CpuAction.Summon)
        {
            if (step.TargetZone is not int zone || zone < 0) return;
            int index = step.HandIndex.GetValueOrDefault();
            var origin = CpuHandBounds(index, handCount);
            BeginFlight(card, step.Player, zone, origin, !card.IsMonster, step.Action == CpuAction.Set);
        }
        else if (step.Action is CpuAction.Equip or CpuAction.Spell or CpuAction.Response)
        {
            var center = step.SourceZone is int zone
                ? NativeDuelLayout.MonsterCenter(zone, step.Player == ViewPlayer) : new CardPoint(525, 140);
            BeginSpellActivation(center.X, center.Y);
        }
        else if (step.Action == CpuAction.Position && step.SourceZone is int zone && duel.MonsterWasFlipped[step.Player, zone])
        {
            var center = NativeDuelLayout.MonsterCenter(zone, step.Player == ViewPlayer);
            BeginSpellActivation(center.X, center.Y, category: 1);
        }
    }

    static Rectangle CpuHandBounds(int index, int count)
    {
        // The far hand uses mirrored native spacing in a compact client row.
        int page = index / 6, cardsOnPage = Math.Clamp(count - page * 6, 1, 6);
        int center = 525 + (525 - HandPresentation.CenterX(index % 6, cardsOnPage)) / 2;
        return new(center - 15, 73, 30, 40);
    }

    static void DrawCpuHand()
    {
        int count = duel.HandFor(1).Count;
        for (int i = 0; i < Math.Min(6, count); i++)
        {
            var rect = CpuHandBounds(i, count);
            DrawCardBack((int)rect.X, (int)rect.Y, (int)rect.Width, 20);
        }
        Label($"CPU hand {count}", 254, 76, 13, Gold);
    }
}
