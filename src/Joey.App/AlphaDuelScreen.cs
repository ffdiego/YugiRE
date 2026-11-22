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
    // Tribute monsters must disappear before the incoming card's travel.
    // Core already committed the summon; this only delays its visual flight.
    static CardFlight? deferredTributeFlight;
    static readonly PresentationTickClock travelClock = new();
    static int travelUpdate;
    // A hidden defender remains visually face-down until sword contact,
    // although Core has already resolved battle and may have destroyed it.
    sealed record HiddenAttackTarget(Card Card, int Player, int Zone, bool Defense);
    sealed record AttackCue(CardPoint Origin, CardPoint Target, bool NearSide,
        bool AlreadyAimed, HiddenAttackTarget? HiddenTarget);
    static AttackCue? attackCue;
    static readonly PresentationTickClock attackClock = new();
    static int attackUpdates;

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
        QueueDuelOpening();
        QueueOpeningDeal();
        QueueInitialExodiaOpening();
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
        ClearDuelSelection();
        StopSpellActivation();
        ResetCardEffects();
        foreach (var layout in handLayouts) layout.Clear();
        ResetDuelPhases();
        ResetDuelDetail();
        ResetDuelResultNotice();
        flight = null;
        deferredTributeFlight = null;
        visualReviewPaused = false;
        visualReviewStep = false;
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
        var planned = new CardFlight(card, hidden, !spell && hidden, player, zone, spell, origin,
            ToRectangle(DuelCardLayout.OnBoard(spell ? DuelCardZone.Spell : DuelCardZone.Monster,
                player == ViewPlayer, zone, false, hidden).Bounds));
        if (!spell && HasQueuedTributesFor(player))
        {
            deferredTributeFlight = planned;
            return;
        }
        travelUpdate = 0;
        travelClock.Reset();
        flight = planned;
    }

    static void ResumeTributeSummonFlight()
    {
        if (deferredTributeFlight is not { } planned) return;
        deferredTributeFlight = null;
        travelUpdate = 0;
        travelClock.Reset();
        flight = planned;
    }

    static bool InFlight(int player, int zone, bool spell) =>
        flight is { } f && (f.Player, f.Zone, f.Spell) == (player, zone, spell)
        || deferredTributeFlight is { } pending &&
           (pending.Player, pending.Zone, pending.Spell) == (player, zone, spell);

    static void DrawFlight()
    {
        if (flight is not { } f) return;
        static CardBounds Bounds(Rectangle rect) => new((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
        var cell = CardTravel.Sample(Bounds(f.Origin), Bounds(f.Destination), travelUpdate);
        DrawFieldCard(f.Card, new(cell.X, cell.Y, cell.Width, cell.Height), f.Hidden, f.Defense, upsideDown: f.Player != ViewPlayer);
    }

    static HiddenAttackTarget? CaptureHiddenAttackTarget(int defender, int zone)
    {
        if (zone is < 0 or >= NativeDuelLayout.MonsterSlots ||
            !duel.MonsterFaceDown[defender, zone] ||
            duel.Monsters[defender, zone] is not Card card) return null;
        return new(card, defender, zone, duel.DefensePosition[defender, zone]);
    }

    static void BeginAttackCue(int player, int source, int target,
        HiddenAttackTarget? hiddenTarget = null)
    {
        if (!presentation.Style.PlayAnimations) return;
        bool near = player == ViewPlayer;
        var from = NativeAttackFrame.SwordAnchor(source, near);
        var to = target >= 0 ? NativeDuelLayout.MonsterCenter(target, !near) : NativeAttackFrame.DirectTarget(near);
        attackClock.Reset();
        attackUpdates = 0;
        attackCue = new(from, to, near, target >= 0 && (alphaCpu is null || player == 0), hiddenTarget);
    }

    static void DrawHiddenAttackTargetBeforeImpact()
    {
        // The sword reaches the defender at t=0x100 (update 16). The
        // underlying Core monster may already be face-up or destroyed, so
        // cover it with the captured back until contact. CardEffects draws
        // before this overlay; do not leak a queued destruction reveal.
        if (attackCue?.HiddenTarget is not { } target ||
            attackUpdates >= NativeAttackFrame.ImpactStartUpdate) return;
        var geometry = DuelCardLayout.OnBoard(DuelCardZone.Monster,
            target.Player == ViewPlayer, target.Zone, target.Defense, faceDown: true);
        DrawDuelCard(target.Card, geometry);
    }

    static void DrawAttackCue()
    {
        if (attackCue is not { } cue) return;
        var frame = NativeAttackFrame.Sample(attackUpdates, cue.Origin, cue.Target, cue.NearSide, effectCosine,
            cue.AlreadyAimed);
        if (frame.ImpactScaleFixed16 > 0)
            DrawEffectStamp(DatGraphics.AttackImpact, frame.Position, 0, frame.ImpactScaleFixed16, frame.Opacity);
        DrawEffectStamp(frame.PartialSword ? DatGraphics.AttackSwordPartial : DatGraphics.AttackSword,
            frame.Position, frame.RotationIndex, 0x10000, frame.Opacity);
    }

    static void DrawEffectStamp(string name, CardPoint center, int rotation, int scale, int opacity)
    {
        var sprite = graphics.Texture(name);
        var transform = new NativeEffectSpritePass(0, rotation, scale, opacity);
        int width = transform.ScaledPixels(sprite.Width), height = transform.ScaledPixels(sprite.Height);
        if (width == 0 || height == 0) return;
        Raylib.DrawTexturePro(sprite, new Rectangle(0, 0, sprite.Width, sprite.Height),
            new Rectangle(center.X, center.Y, width, height), new Vector2(width / 2, height / 2),
            rotation * (360f / 512), new Color(255, 255, 255, opacity));
    }

    static void DrawAttackAim()
    {
        var origin = NativeAttackFrame.SwordAnchor(attacker, true);
        Vector2 target = mouse;
        int opponent = 1 - ViewPlayer;
        for (int zone = 0; zone < 5; zone++)
        {
            if (duel.Monsters[opponent, zone] is null || !Hit(ZoneBounds(opponent, zone, false))) continue;
            var center = NativeDuelLayout.MonsterCenter(zone, false);
            target = new(center.X, center.Y);
            break;
        }
        if (duel.CanAttackDirectly(attacker))
        {
            var direct = NativeAttackFrame.DirectTarget(nearSide: true);
            var sword = graphics.Texture(DatGraphics.AttackSword);
            var hitbox = new Rectangle(direct.X - sword.Width / 2, direct.Y - sword.Height / 2,
                sword.Width, sword.Height);
            bool hovered = Hit(hitbox);
            DrawEffectStamp(DatGraphics.AttackSword, direct, 0, 0x10000, hovered ? 255 : 0xa0);
            if (hovered)
            {
                target = new(direct.X, direct.Y);
                if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    CommitHumanAttack(-1);
                    return;
                }
            }
        }
        int rotation = NativeAttackFrame.AimRotation(target.X - origin.X, target.Y - origin.Y);
        DrawEffectStamp(DatGraphics.AttackSword, origin, rotation, 0x10000, 255);
    }

    static void CommitHumanAttack(int target)
    {
        int player = duel.ActivePlayer;
        var hidden = CaptureHiddenAttackTarget(1 - player, target);
        if (duel.Attack(attacker, target, out message) && duel.PendingAttackResponse is null)
            BeginAttackCue(player, attacker, target, hidden);
    }

    static void AdvanceAlpha(TimeSpan elapsed)
    {
        if (attackCue is not null)
        {
            attackUpdates += Math.Min(NativeAttackFrame.Updates, attackClock.Advance(elapsed, presentation.Style));
            if (attackUpdates >= NativeAttackFrame.Updates) attackCue = null;
        }
        if (flight is not null)
        {
            travelUpdate += Math.Min(CardTravel.Updates, travelClock.Advance(elapsed, presentation.Style));
            if (travelUpdate >= CardTravel.Updates) flight = null;
        }
        // F7 steps presentation, never authorizes a new CPU decision.
        // Without this gate a paused idle duel could still mutate Joey.Core.
        if (visualReviewPaused) return;
        if (alphaCpu is not { } cpu || alphaBlocked || duel.GameOver || MotionBusy || PhaseBusy) return;
        cpuDelay -= elapsed;
        if (cpuDelay > TimeSpan.Zero) return;
        int? owner = CpuDuelController.PendingOwner(duel);
        if ((owner ?? duel.ActivePlayer) != cpu.Player) return;
        int handCount = duel.HandFor(cpu.Player).Count;
        var pendingAttack = duel.PendingAttackResponse?.Attack;
        // CPU Step resolves its Core action immediately. Capture hidden
        // targets before it can flip/destroy them; only the matching attack
        // uses its snapshot, all other CPU actions discard these references.
        var hiddenBeforeStep = new HiddenAttackTarget?[2, NativeDuelLayout.MonsterSlots];
        for (int player = 0; player < 2; player++)
        for (int zone = 0; zone < NativeDuelLayout.MonsterSlots; zone++)
            hiddenBeforeStep[player, zone] = CaptureHiddenAttackTarget(player, zone);
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
        if (step.Action == CpuAction.Attack && duel.PendingAttackResponse is null &&
            step.SourceZone is int attackerZone && step.TargetZone is int targetZone)
            BeginAttackCue(step.Player, attackerZone, targetZone,
                targetZone is >= 0 and < NativeDuelLayout.MonsterSlots
                    ? hiddenBeforeStep[1 - step.Player, targetZone] : null);
        else if (step.Action == CpuAction.Response && step.Card is null && pendingAttack is { } continued)
            BeginAttackCue(continued.AttackingPlayer, continued.AttackerZone, continued.TargetZone,
                continued.TargetZone is >= 0 and < NativeDuelLayout.MonsterSlots
                    ? hiddenBeforeStep[1 - continued.AttackingPlayer, continued.TargetZone] : null);
        if (step.Card is not Card card) return;
        if (step.Action is CpuAction.Set or CpuAction.Summon or
            CpuAction.Spell or CpuAction.Equip or CpuAction.Response)
            ShowPlayedCardDetail(card, hiddenFromPlayer:
                step.Player != ViewPlayer && step.Action == CpuAction.Set);
        if (step.Action is CpuAction.Set or CpuAction.Summon)
        {
            if (step.TargetZone is not int zone || zone < 0) return;
            int index = step.HandIndex.GetValueOrDefault();
            var origin = CpuHandBounds(index, handCount);
            handLayouts[step.Player].RemoveAt(index);
            BeginFlight(card, step.Player, zone, origin, !card.IsMonster, step.Action == CpuAction.Set);
            if (step.Action == CpuAction.Set)
                QueueSetEffect(step.Player, zone, spell: !card.IsMonster);
            else
                QueueNormalSummonEffect(card, step.Player, zone);
        }
        else if (step.SourceZone is int spellZone &&
                 step.Action is CpuAction.Equip or CpuAction.Spell or CpuAction.Response)
        {
            Rectangle? origin = step.HandIndex is int index ? CpuHandBounds(index, handCount) : null;
            if (step.HandIndex is int spent) handLayouts[step.Player].RemoveAt(spent);
            BeginCardActivation(card, ZoneBounds(step.Player, spellZone, true), origin, nearSide: step.Player == ViewPlayer);
        }
        else if (step.Action == CpuAction.Position && step.SourceZone is int zone && duel.MonsterWasFlipped[step.Player, zone])
        {
            var center = NativeDuelLayout.MonsterCenter(zone, step.Player == ViewPlayer);
            BeginSpellActivation(center.X, center.Y, category: 1);
            ShowActivatedDetail(card);
        }
    }

    static Rectangle CpuHandBounds(int index, int count) =>
        ToRectangle(handLayouts[1].Bounds(index, count, CpuHandDropAt(index)));

    static void DrawCpuHand()
    {
        var visible = VisibleHand(1);
        SynchronizeHand(1, visible);
        int count = visible.Count;
        for (int i = 0; i < count; i++)
        {
            if (i == hoverCpuHand || IncomingTransferInFlight(1, i, visible[i]))
                continue;
            var rect = CpuHandBounds(i, count);
            DrawCardBack((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height, degrees: 180);
        }
        // The selected Joey card drops below its neighbours and moves to
        // the topmost draw layer. The back remains hidden even on hover.
        if (hoverCpuHand >= 0 && hoverCpuHand < count &&
            !IncomingTransferInFlight(1, hoverCpuHand, visible[hoverCpuHand]))
        {
            var rect = CpuHandBounds(hoverCpuHand, count);
            DrawCardBack((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height, degrees: 180);
            DrawHandSelectionFrame(rect, nearSide: false);
        }
        if (duelDebug) Label($"CPU hand {count}", 266, 76, 13, Gold);
    }
}
