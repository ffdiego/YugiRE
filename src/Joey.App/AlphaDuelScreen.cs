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
        Rectangle Origin, Rectangle Destination);
    static CardFlight? flight;
    static readonly PresentationTickClock travelClock = new();
    static int travelUpdate;
    sealed record AttackCue(Vector2 Origin, Vector2 Target, TimeSpan Duration);
    static AttackCue? attackCue;
    static TimeSpan attackElapsed;

    static void StartAlpha()
    {
        try
        {
            var deck = AlphaDeck.Create(playableCards);
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
        message = "Your turn.";
    }

    static int RunAlphaHeadless()
    {
        const int seed = 1979;
        var deck = AlphaDeck.Create(playableCards);
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
        StopSpellActivation();
        ResetCardEffects();
        ResetDuelPhases();
        ResetDuelDetail();
        flight = null;
        travelClock.Reset();
        hoverClock.Reset();
        attackCue = null;
        presentation.Clear();
        previousDuelMessage = "";
    }

    static Rectangle ZoneBounds(int player, int zone, bool spell) =>
        CardZoneBounds(spell ? DuelCardZone.Spell : DuelCardZone.Monster, player, zone);

    static Rectangle FieldZoneBounds(int player) => CardZoneBounds(DuelCardZone.Field, player);

    static Rectangle CardZoneBounds(DuelCardZone zone, int player, int slot = 0)
    {
        var center = DuelCardLayout.Center(zone, player == ViewPlayer, slot);
        return new(center.X - NativeDuelLayout.MonsterCardWidth / 2,
            center.Y - NativeDuelLayout.MonsterCardHeight / 2,
            NativeDuelLayout.MonsterCardWidth, NativeDuelLayout.MonsterCardHeight);
    }

    static void BeginFlight(Card card, int player, int zone, Rectangle origin, bool spell, bool hidden)
    {
        if (alphaCpu is null || !presentation.Style.PlayAnimations) return;
        travelUpdate = 0;
        travelClock.Reset();
        flight = new(card, hidden, !spell && hidden, player, zone, spell, origin,
            ToRectangle(DuelCardLayout.OnBoard(spell ? DuelCardZone.Spell : DuelCardZone.Monster,
                player == ViewPlayer, zone, false, hidden).Bounds));
    }

    static bool InFlight(int player, int zone, bool spell) =>
        flight is { } f && (f.Player, f.Zone, f.Spell) == (player, zone, spell);

    static void DrawFlight()
    {
        if (flight is not { } f) return;
        static CardBounds Bounds(Rectangle rect) => new((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
        var cell = CardTravel.Sample(Bounds(f.Origin), Bounds(f.Destination), travelUpdate);
        DrawFieldCard(f.Card, new(cell.X, cell.Y, cell.Width, cell.Height), f.Hidden, f.Defense, upsideDown: f.Player != ViewPlayer);
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
        DrawAttackSword(head, cue.Target - cue.Origin);
    }

    static void DrawAttackAim()
    {
        var origin = NativeDuelLayout.MonsterCenter(attacker, true);
        Vector2 target = mouse;
        int opponent = 1 - ViewPlayer;
        for (int zone = 0; zone < 5; zone++)
        {
            if (duel.Monsters[opponent, zone] is null || !Hit(ZoneBounds(opponent, zone, false))) continue;
            var center = NativeDuelLayout.MonsterCenter(zone, false);
            target = new(center.X, center.Y);
            break;
        }
        var anchor = new Vector2(origin.X, ZoneBounds(ViewPlayer, attacker, false).Y + 26);
        DrawAttackSword(anchor, target - anchor);
    }

    static void DrawAttackSword(Vector2 position, Vector2 direction)
    {
        var sword = graphics.Texture(DatGraphics.AttackSword);
        float angle = MathF.Atan2(direction.X, -direction.Y) * 180 / MathF.PI;
        Raylib.DrawTexturePro(sword, new Rectangle(0, 0, sword.Width, sword.Height),
            new Rectangle(position.X, position.Y, 25, 51), new Vector2(12.5f, 25.5f), angle, Color.White);
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
        if (flight is not null)
        {
            travelUpdate += Math.Min(CardTravel.Updates, travelClock.Advance(elapsed, presentation.Style));
            if (travelUpdate >= CardTravel.Updates) flight = null;
        }
        if (alphaCpu is not { } cpu || alphaBlocked || duel.GameOver || MotionBusy || PhaseBusy) return;
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
        else if (step.SourceZone is int spellZone &&
                 step.Action is CpuAction.Equip or CpuAction.Spell or CpuAction.Response)
        {
            Rectangle? origin = step.HandIndex is int index ? CpuHandBounds(index, handCount) : null;
            BeginCardActivation(card, ZoneBounds(step.Player, spellZone, true), origin, nearSide: step.Player == ViewPlayer);
        }
        else if (step.Action == CpuAction.Position && step.SourceZone is int zone && duel.MonsterWasFlipped[step.Player, zone])
        {
            var center = NativeDuelLayout.MonsterCenter(zone, step.Player == ViewPlayer);
            BeginSpellActivation(center.X, center.Y, category: 1);
            ShowActivatedDetail(card);
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
        int count = VisibleHand(1).Count;
        for (int i = 0; i < Math.Min(6, count); i++)
        {
            if (DiscardInFlight(1, i)) continue;
            var rect = CpuHandBounds(i, count);
            DrawCardBack((int)rect.X, (int)rect.Y, (int)rect.Width, 20, cropped: true);
        }
        if (duelDebug || count > 6) Label($"CPU hand {count}", 266, 76, 13, Gold);
    }
}
