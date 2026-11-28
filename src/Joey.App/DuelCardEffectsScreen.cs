using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;

static partial class Game
{
    abstract record CardEffect;
    sealed record Destruction(DemoMonsterDestruction Monster) : CardEffect;
    sealed record EquipmentTravelVisual(string Artwork, CardPoint Source, CardPoint Target,
        int RotationIndex) : CardEffect
    {
        public bool TerminalDrawn { get; set; }
    }
    sealed record DisabledChainVisual(DemoSuppressedTrap Trap,NativeDisabledChainTarget Target,int Perspective) : CardEffect;
    sealed record CoinResultVisual(NativeCoinTimeline Timeline, int Highlight, NativeDiceTexture[] Textures) : CardEffect;
    sealed record DiceResultVisual(NativeDiceTimeline Timeline, NativeDiceTexture[] Textures) : CardEffect
    {
        public NativeDiceDraw? Frame { get; set; }
    }
    sealed record BreakDestruction(Card Card, NativeBreakFieldTarget Target,
        int Perspective, string CardPath, string ShinePath, NativeBreakTimeline Timeline) : CardEffect;
    sealed record TributePlacement(DemoMonsterTribute Tribute, CardPoint[] ParticleOrigins) : CardEffect;
    // Capture the departure before Joey.Core's committed hand is reflowed.
    // The moving card is drawn independently while its neighbours shift.
    sealed record HandTransfer(DemoHandTransfer Transfer, CardBounds? Departure = null) : CardEffect;
    sealed record SetPlacement(int Player, int Zone, bool Spell, CardPoint[] ParticleOrigins, bool Field = false) : CardEffect;
    sealed record NormalSummonPlacement(Card Card, int Player, int Zone) : CardEffect;
    sealed record SpecialSummonPlacement(DemoSpecialSummon Summon, bool RevealDetailOnStart) : CardEffect;
    sealed record GeneralFusionPlacement(DemoSpecialSummon Summon, NativeFusionPlayback Playback,
        string[] Miniatures, CardBounds[] Sizes) : CardEffect;
    sealed record DuelOpening : CardEffect;
    sealed record ShuffleDeck(int Player) : CardEffect;
    // Use the same FIFO as card animations. A cinematic with unknown Draw
    // has no duration, cannot enter the queue and cannot lock input.
    sealed record CinematicScene(NativeCinematicCue Cue, DemoExodiaCompletion? Completion) : CardEffect;

    static readonly PresentationSequence<CardEffect> legacyCardEffects = new();
    static readonly CommandPresentationSteps<CardEffect> commandCardEffects = new();
    // During a scheduler command, event handlers can emit only its own visual substeps.
    // No event waits in a second logical FIFO across commands.
    static PresentationSequence<CardEffect> cardEffects => queuedDuel?.Active is { } command
        ? commandCardEffects.For(command.Sequence) : legacyCardEffects;
    static readonly PresentationTickClock cardEffectClock = new();
    static bool CardEffectsBusy => cardEffects.Current is not null;
    static bool ExodiaOpeningQueued =>
        cardEffects.Pending.Any(step => step.Kind is CinematicScene
        { Cue.Kind: NativeCinematicKind.ExodiaOpening });
    // Diagnostic only: resource 81 or 744 would enter the native
    // bust-up pipeline, but the perspective gate and sprite timing
    // are not mapped yet. Never treat this as an active animation.
    static NativeCinematicCue? lastSignatureCandidate;
    // The native Vija Draw has not been ported. Retain the route for F12
    // only; no synthetic pause or invented five-letter overlay.
    static NativeCinematicCue? lastDestinyBoardCandidate;
    static bool CanPlayCardEffects => flight is null && attackCue is null && !ActivationBusy;
    // Card/attack animations take precedence over LP presentation. Do not
    // confuse this gate with MotionBusy, which also blocks actions while LP
    // numbers are still catching up with the already-committed Core state.
    static bool CardMotionBusy => flight is not null || attackCue is not null ||
                                  ActivationBusy || CardEffectsBusy;
    static bool LifePresentationBusy => presentation.Style.PlayAnimations &&
        Enumerable.Range(0, lifeDisplays.Length).Any(player =>
            lifeDisplays[player].Animating ||
            lifeDisplays[player].Displayed != NativeLifePointPresentation.ClampToNativeDisplay(duel.Life[player]));
    static bool MotionBusy => CardMotionBusy || LifePresentationBusy;

    static void ResetCardEffects()
    {
        legacyCardEffects.Clear();
        commandCardEffects.Reset();
        cardEffectClock.Reset();
        breakTails.Clear();
        breakTailClock.Reset();
        liveAffect=new();
        liveExclusions.Clear();
        exclusionClock.Reset();
        lastSignatureCandidate = null;
        lastDestinyBoardCandidate = null;
        duel.DestinyBoardCompleted += completed =>
            lastDestinyBoardCandidate =
                NativeCinematicRouting.DestinyBoardVictory(completed.Player);
        duel.MonsterNormalSummoned += normal =>
        {
            lastSignatureCandidate = NativeCinematicRouting.SignatureCandidate(
                normal.Player, normal.Card.Id, normal.FaceDown);
            // Presentation events also cover forced/automatic normal Summons.
            // A hidden CPU Set must never disclose its actual card identity.
            ShowPlayedCardDetail(normal.Card,
                hiddenFromPlayer: normal.Player != ViewPlayer && normal.FaceDown);
        };
        duel.EquipmentActivated += QueueEquipmentTravel;
        duel.CardBanished += QueueLiveExclusion;
        duel.LabCoinResultReported += QueueLiveCoin;
        duel.LabDiceResultReported += result => QueueLiveDice(result.Side, result.Region, result.Index);
        duel.DiceRolled += result => QueueLiveDice(result.Side, result.Region, result.Face);
        duel.MonsterDestroyed += monster =>
        {
            if (!presentation.Style.PlayAnimations) return;
            if (TryCreateLiveBreak(monster) is { } effect)
                QueueCardEffect(effect,NativeLiveBreak.BlockingUpdates);
            else
                QueueCardEffect(new Destruction(monster), NativeExplosionTimeline.Updates);
        };
        duel.SpellTrapDestroyed += removal =>
        {
            if (!presentation.Style.PlayAnimations) return;
            if (TryCreateLiveBreak(removal) is { } effect)
                QueueCardEffect(effect,NativeLiveBreak.BlockingUpdates);
        };
        duel.MonsterTributed += tribute => QueueTributeEffect(tribute);
        duel.HandTransferred += transfer =>
        {
            // Joey.Core already applied this transfer when the event fires.
            // For removal, the old card is still in the layout until the
            // next presentation sync, so preserve its actual departure.
            CardBounds? departure = transfer.FromDeck ? null :
                handLayouts[transfer.Player].Bounds(
                    transfer.Index, duel.HandFor(transfer.Player).Count + 1);
            QueueCardEffect(new HandTransfer(transfer, departure), CardTravel.Updates);
        };
        duel.MonsterSpecialSummoned += summon =>
        {
            bool generalFusionQueued = TryQueueGeneralFusion(summon);
            if (!generalFusionQueued)
                ShowPlayedCardDetail(summon.Card, hiddenFromPlayer:
                    summon.Player != ViewPlayer && summon.FaceDownDefense);
            QueueCardEffect(new SpecialSummonPlacement(summon, generalFusionQueued), NativeSpecialSummonFrame.Updates);
            if(!summon.FaceDownDefense)QueueJinzoSuppression(summon.Card,summon.Player,summon.Zone);
        };
        duel.DeckShuffled += player =>
            QueueCardEffect(new ShuffleDeck(player), NativeShuffleFrame.Updates);
        duel.ExodiaCompleted += completion => QueueExodiaOpening(completion);
    }

    static void QueueCardEffect(CardEffect effect, int updates)
    {
        if (!presentation.Style.PlayAnimations) return;
        if (!CardEffectsBusy) cardEffectClock.Reset();
        cardEffects.Enqueue(effect, updates);
    }

    static void QueueTributeEffect(DemoMonsterTribute tribute)
    {
        if (!presentation.Style.PlayAnimations) return;
        Span<int> radii = stackalloc int[NativeSetFrame.ParticleCount];
        for (int i = 0; i < radii.Length; i++)
            radii[i] = Random.Shared.Next(NativeSetFrame.MinimumRadius,
                NativeSetFrame.MaximumRadius + 1);
        var center = DuelCardLayout.Center(DuelCardZone.Monster,
            tribute.Player == ViewPlayer, tribute.Zone);
        QueueCardEffect(new TributePlacement(tribute,
            NativeTributeFrame.ParticleOrigins(center, radii, effectCosine)),
            NativeTributeFrame.ReleasedUpdates);
    }

    static bool HasQueuedTributesFor(int player) =>
        cardEffects.Pending.Any(step => step.Kind is TributePlacement { Tribute.Player: var owner }
            && owner == player);

    static void QueueSetEffect(int player, int zone, bool spell, bool field = false)
    {
        if (!presentation.Style.PlayAnimations) return;
        // The card is already committed to the Core; Set runs after flight.
        // Constructor 0044EFD0 takes eight 5-bit MT draws. Reproduce its
        // distribution, but not its shared native RNG stream/seed.
        Span<int> radii = stackalloc int[NativeSetFrame.ParticleCount];
        for (int i = 0; i < radii.Length; i++)
            radii[i] = Random.Shared.Next(NativeSetFrame.MinimumRadius,
                                           NativeSetFrame.MaximumRadius + 1);
        var center = DuelCardLayout.Center(field ? DuelCardZone.Field : spell ? DuelCardZone.Spell : DuelCardZone.Monster,
                                          player == ViewPlayer, zone);
        var origins = NativeSetFrame.ParticleOrigins(center, radii, effectCosine);
        QueueCardEffect(new SetPlacement(player, zone, spell, origins, field), NativeSetFrame.Updates);
    }

    static void QueueNormalSummonEffect(Card card, int player, int zone)
    {
        // Native 0044F9F0: 33 updates. Core has already applied the summon.
        // The same queue keeps the visual presentation after the hand flight.
        QueueCardEffect(new NormalSummonPlacement(card, player, zone), NativeNormalSummonFrame.Updates);
        QueueJinzoSuppression(card,player,zone);
    }

    static void QueueExodiaOpening(DemoExodiaCompletion completion)
    {
        if (completion.Pieces.Count != NativeExodiaOpeningFrame.CardCount)
            throw new ArgumentException("Expected five Exodia pieces.", nameof(completion));
        // Core commits the win immediately; the existing FIFO handles
        // the visual wait, after any already-queued fifth-card transfer.
        var cue = NativeCinematicRouting.ExodiaOpening(completion.Player);
        QueueCinematic(cue, completion);
    }

    static bool QueueCinematic(NativeCinematicCue cue, DemoExodiaCompletion? completion)
    {
        int? updates = NativeCinematicRouting.ImplementedUpdates(cue);
        if (updates is not int count || !presentation.Style.PlayAnimations)
            return false; // Crucial: unknown bust-up scenes do NOT stall.
        if (cue.Kind == NativeCinematicKind.ExodiaOpening && completion is null)
            throw new ArgumentException("Exodia scene requires the five cards.", nameof(completion));
        QueueCardEffect(new CinematicScene(cue, completion), count);
        return true;
    }

    static void QueueInitialExodiaOpening()
    {
        // FromDecks can recognize a complete starting hand before the UI
        // subscribes to Core events. Keep this order after Duel Start/deal.
        if (duel.ExodiaWinner is not int winner) return;
        var pieces = ExodiaRules.CompleteHand(duel.HandFor(winner));
        if (pieces is not null)
            QueueExodiaOpening(new DemoExodiaCompletion(winner, pieces));
    }

    static void QueueDuelOpening()
    {
        QueueCardEffect(new DuelOpening(), NativeDuelStartFrame.Updates);
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
        // The logical hand count changes at transfer START, not when the
        // animation ends. SynchronizeHand performs the reflow; removing
        // the old index here would delete a different remaining card.
        // After a stall, leave the next card on its first sample.
        if(updates>0 && cardEffects.ElapsedUpdates==0)
        {
            DuelSoundCue? cue=current.Kind switch
            {
                Destruction=>DuelSoundCue.Destruction,
                HandTransfer=>DuelSoundCue.CardMove,
                TributePlacement=>DuelSoundCue.Tribute,
                SpecialSummonPlacement=>DuelSoundCue.SpecialSummon,
                DuelOpening=>DuelSoundCue.Start,
                _=>null
            };
            if(cue is { } sound)PlayDuelSound(sound);
        }
        int remaining = current.Updates - cardEffects.ElapsedUpdates;
        int consumed = Math.Min(updates, remaining);
        // One virtual native Update/Draw per rendered step for these banks.
        // Drop excess catch-up rather than skip their Draw-dependent motion.
        if (current.Kind is CoinResultVisual or DiceResultVisual or EquipmentTravelVisual)
            consumed = Math.Min(consumed, 1);
        // The native terminal counter is cleared in Draw, not Update.
        // Keep the sample at 192 visible until it has actually been drawn.
        if (current.Kind is EquipmentTravelVisual equip &&
            cardEffects.ElapsedUpdates == NativeEquipmentTravel.Updates && !equip.TerminalDrawn)
            consumed = 0;
        if (consumed != 0 && current.Kind is CoinResultVisual coin)
            coin.Timeline.AdvanceNativeUpdate();
        if (consumed != 0 && current.Kind is DiceResultVisual dice)
            dice.Timeline.AdvanceNativeUpdate();
        if (current.Kind is GeneralFusionPlacement fusion)
            fusion.Playback.AdvanceTo(
                Math.Min(NativeFusionPlayback.LastSample, cardEffects.ElapsedUpdates + consumed),
                effectCosine, key => fusion.Sizes[key],
                cue => PlayDuelSound(cue == 0x10000020u ? DuelSoundCue.FusionStart : DuelSoundCue.FusionResult));
        if (consumed > 0 && cardEffects.ElapsedUpdates == 0 &&
            current.Kind is SpecialSummonPlacement { RevealDetailOnStart: true } special)
            ShowPlayedCardDetail(special.Summon.Card);
        if (current.Kind is BreakDestruction broken)
        {
            for (int i=0;i<consumed;i++) broken.Timeline.AdvanceNativeUpdate();
            if (consumed == remaining)
            {
                // Release the command scope at t=64. No logical action is
                // stored here: only the already committed visual snapshot.
                if (breakTails.Count == 0) breakTailClock.Reset();
                breakTails.Add(broken);
            }
        }
        cardEffects.Advance(consumed);
        // Pay tribute(s) visually before revealing the new summon flight.
        // The flight has already been committed logically by Joey.Core.
        if (current.Kind is TributePlacement && !HasQueuedTributesFor(((TributePlacement)current.Kind).Tribute.Player))
            ResumeTributeSummonFlight();
    }

    static IReadOnlyList<Card> VisibleHand(int player)
    {
        if (!CardEffectsBusy) return duel.HandFor(player);

        // Undo only transfers that have not started rendering. The active
        // transfer already changes the visible hand *layout*: existing cards
        // reflow while the moving card is separately animated. The arriving
        // card is masked by IncomingTransferInFlight until it has landed.
        List<Card> visible = [.. duel.HandFor(player)];
        CardEffect? active = CanPlayCardEffects ? cardEffects.Current?.Kind : null;
        foreach (var step in cardEffects.Pending.Reverse())
        {
            if (step.Kind is not HandTransfer { Transfer: var move } || move.Player != player) continue;
            if (ReferenceEquals(step.Kind, active)) continue;
            if (move.FromDeck)
                visible.RemoveAt(move.Index);
            else
                visible.Insert(move.Index, move.Card);
        }
        return visible;
    }

    static int VisibleGraveCount(int player) => Math.Max(0, duel.Graveyard(player).Count -
        cardEffects.Pending.Count(step => step.Kind is Destruction { Monster.Player: var owner } && owner == player ||
            step.Kind is BreakDestruction { Target.Side: var breakOwner } && breakOwner == player ||
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

    static bool IncomingTransferInFlight(int player, int index, Card card) =>
        CanPlayCardEffects &&
        cardEffects.Current?.Kind is HandTransfer { Transfer: { FromDeck: true } move } &&
        move.Player == player && move.Index == index &&
        ReferenceEquals(move.Card, card);

    static void DrawCardEffects()
    {
        bool playing = CanPlayCardEffects;
        // Deferred removals stay beneath the active effect, even in adjacent zones.
        foreach (var step in cardEffects.Pending.Skip(playing ? 1 : 0))
            if (step.Kind is Destruction { Monster: var queued }) DrawDestructionCard(queued);
            else if (step.Kind is BreakDestruction queuedBreak) DrawPendingBreakCard(queuedBreak);

        // Detached tails continue during later commands and never enter
        // CardEffectsBusy / MotionBusy or keep a command scope alive.
        foreach (var tail in breakTails) DrawLiveBreak(tail);

        if (!playing) return;
        switch (cardEffects.Current?.Kind)
        {
            case DisabledChainVisual disabled:
                DrawLiveDisabledChain(disabled,cardEffects.ElapsedUpdates);
                break;
            case EquipmentTravelVisual equip:
                DrawEquipmentTravel(equip,cardEffects.ElapsedUpdates);
                break;
            case CoinResultVisual coin:
                DrawLiveCoin(coin);
                break;
            case DiceResultVisual die:
                DrawLiveDice(die);
                break;
            case BreakDestruction broken:
                DrawLiveBreak(broken);
                break;
            case Destruction { Monster: var monster }:
                DrawDestruction(monster, cardEffects.ElapsedUpdates);
                break;
            case TributePlacement tribute:
                DrawTributePlacement(tribute, cardEffects.ElapsedUpdates);
                break;
            case HandTransfer transfer:
                DrawHandTransfer(transfer);
                break;
            case SetPlacement placement:
                DrawSetPlacement(placement, cardEffects.ElapsedUpdates);
                break;
            case NormalSummonPlacement summon:
                DrawNormalSummon(summon, cardEffects.ElapsedUpdates);
                break;
            case GeneralFusionPlacement fusion:
                DrawLiveGeneralFusion(fusion);
                break;
            case SpecialSummonPlacement special:
                DrawSpecialSummon(special.Summon, cardEffects.ElapsedUpdates);
                break;
            case DuelOpening:
                DrawDuelOpening(cardEffects.ElapsedUpdates);
                break;
            case ShuffleDeck shuffled:
                DrawDeckShuffle(shuffled.Player, cardEffects.ElapsedUpdates);
                break;
            case CinematicScene { Cue.Kind: NativeCinematicKind.ExodiaOpening,
                                  Completion: { } exodia }:
                DrawExodiaOpening(exodia, cardEffects.ElapsedUpdates);
                break;
        }
    }

    static void DrawExodiaOpening(DemoExodiaCompletion completion, int elapsedUpdates)
    {
        var frame = NativeExodiaOpeningFrame.FromUpdate(elapsedUpdates, effectCosine);
        foreach (var pose in frame.Cards)
        {
            if (pose.Opacity == 0) continue;
            string? full = graphics.Artwork.For(completion.Pieces[pose.Sequence]);
            if (full is null || !full.StartsWith(@"card\", StringComparison.OrdinalIgnoreCase))
                continue;
            var texture = graphics.Texture(@"mini\" + full[5..]);
            Raylib.DrawTexture(texture,
                pose.Center.X - texture.Width / 2,
                pose.Center.Y - texture.Height / 2,
                new Color(255, 255, 255, pose.Opacity));
        }

        // This is ONLY t=0..312 of 00454010. Its remaining stages (assembled
        // Exodia figure, background, glow/ball and audio) are not mapped.
        // The generic native win banner is the deliberate client fallback.
    }

    static void DrawDeckShuffle(int player, int elapsedUpdates)
    {
        var frame = NativeShuffleFrame.FromUpdate(elapsedUpdates);
        var deckCenter = DuelCardLayout.FrameCenter(
            NativeDuelLayout.DeckBounds(player == ViewPlayer));
        // DAT_005DB570 is the shared native shuffle X origin; its value is
        // unresolved here, so (525,300) is an explicit Alpha approximation.
        var stageCenter = new CardPoint(525, 300);
        var center = frame.Position(deckCenter, stageCenter);
        int scale = frame.Moving ? frame.DeckWeightFixed8 << 8 : 0x10000;
        if (scale > 0)
            DrawEffectStamp(DatGraphics.ShuffleSprites[frame.SpriteFrame],
                center, 0, scale, 255);
        if (!frame.Moving)
            DrawEffectStamp(DatGraphics.ShuffleFrameEnglish,
                stageCenter with { Y = 326 }, 0, 0x10000, 255);
    }

    static void DrawDuelOpening(int elapsedUpdates)
    {
        var frame = NativeDuelStartFrame.FromUpdate(elapsedUpdates, effectCosine);
        if (frame.Opacity == 0) return;
        DrawEffectStamp(DatGraphics.DuelStartEnglish, new CardPoint(400, 300),
            0, frame.ScaleFixed8 << 8, frame.Opacity);
    }

    static void DrawTributePlacement(TributePlacement placement, int elapsedUpdates)
    {
        var tribute = placement.Tribute;
        var frame = NativeTributeFrame.FromUpdate(elapsedUpdates, released: true, effectCosine);
        var center = DuelCardLayout.Center(DuelCardZone.Monster,
            tribute.Player == ViewPlayer, tribute.Zone);

        // The native first pass transforms the sacrificed card. Reuse the
        // source monster snapshot after Joey.Core has already moved it to GY.
        // Native rotation/scratch blend and sacrifice.txt resource order
        // remain unverified in this partial C# renderer.
        string? art = graphics.Artwork.For(tribute.Card);
        string name = tribute.FaceDown ? DatGraphics.DuelBack
            : art is not null && art.StartsWith(@"card\", StringComparison.OrdinalIgnoreCase)
                ? @"mini\" + art[5..] : DatGraphics.DuelBack;
        var geometry = DuelCardLayout.OnBoard(DuelCardZone.Monster,
            tribute.Player == ViewPlayer, tribute.Zone, tribute.Defense, tribute.FaceDown);
        var tex = graphics.Texture(name);
        if (frame.CardOpacity > 0)
            Raylib.DrawTexturePro(tex,
                new Rectangle(0, 0, tex.Width, tex.Height),
                new Rectangle(center.X, center.Y, geometry.Bounds.Width, geometry.Bounds.Height),
                new System.Numerics.Vector2(geometry.Bounds.Width / 2f, geometry.Bounds.Height / 2f),
                geometry.Degrees, new Color(255, 255, 255, frame.CardOpacity));

        // 004870B0 launches eight stars after counter 128, 16 units apart.
        // sacrifice.txt key 2/3 are the yellow stars (v3 manifest).
        foreach (var particle in frame.Particles)
        {
            var origin = placement.ParticleOrigins[particle.Sequence];
            var sprite = graphics.Texture(NativePlacementAssets.TributeStar(particle.Variant));
            Raylib.DrawTexture(sprite,
                origin.X - sprite.Width / 2,
                origin.Y - particle.RisePixels - sprite.Height / 2,
                new Color(255, 255, 255, frame.CardOpacity));
        }
    }

    static void DrawSetPlacement(SetPlacement placement, int elapsedUpdates)
    {
        bool near = placement.Player == ViewPlayer;
        var zone = placement.Field ? DuelCardZone.Field : placement.Spell ? DuelCardZone.Spell : DuelCardZone.Monster;
        var center = DuelCardLayout.Center(zone, near, placement.Zone);
        var frame = NativeSetFrame.FromUpdate(elapsedUpdates, effectCosine);
        // Constructor 0044EFD0 uses 00402690: flag is monster-type 1..20,
        // NOT the player's side. Draw 0044F390 selects indices 2/3 for
        // monsters, 4/5 for spell/trap, alternating every 8 counter units.
        // Original manifest order: monster yellow, spell/trap blue.
        if (frame.MainOpacity <= 0) return;
        DrawEffectStamp(NativePlacementAssets.Aura(placement.Spell),
            center, 0, frame.ScaleFixed16, frame.MainOpacity);
        foreach (var particle in frame.Particles)
        {
            var origin = placement.ParticleOrigins[particle.Sequence];
            var sprite = graphics.Texture(NativePlacementAssets.Star(placement.Spell,particle.Variant));
            Raylib.DrawTexture(sprite, origin.X - sprite.Width / 2,
                origin.Y - particle.RisePixels - sprite.Height / 2,
                new Color(255, 255, 255, frame.MainOpacity));
        }
        // Exact shared MT sequence and native scratch +AC blend await comparison.
    }

    static void DrawNormalSummon(NormalSummonPlacement summon, int elapsedUpdates)
    {
        // The constructor 0044F630 loads data/mini/list_card.txt; the DAT
        // contains a miniature bank; summon.txt is a separate effect bank.
        // 0044FA10 selects mini in the first branch and key 0 of summon.txt
        // in the second branch, NOT a vertically scrolling monster face.
        string? mini = graphics.Artwork.MiniFor(summon.Card);
        if (mini is null) return;
        var sprite = graphics.Texture(mini);
        var center = DuelCardLayout.Center(DuelCardZone.Monster, summon.Player == ViewPlayer, summon.Zone);
        var frame = NativeNormalSummonFrame.FromUpdate(elapsedUpdates, effectCosine);
        if (frame.Opacity == 0) return;
        var tint = new Color(255, 255, 255, frame.Opacity);
        if (frame.Stage == NativeNormalSummonStage.ExpandMiniature)
        {
            float scale = frame.ScaleFixed16 / 65536f;
            float width = sprite.Width * scale, height = sprite.Height * scale;
            Raylib.DrawTexturePro(sprite, new Rectangle(0, 0, sprite.Width, sprite.Height),
                new Rectangle(center.X, center.Y, width, height),
                new System.Numerics.Vector2(width / 2, height / 2), summon.Player == ViewPlayer ? 0 : 180, tint);
            return;
        }

        // Candidate crop adapter: 0044FA10 wraps a mini-sized window
        // inside summon.txt key 0 (100x94), not the card itself.
        // Exact horizontal offset and native +18 blend require assembly.
        var rainbow=graphics.Texture(NativePlacementAssets.Rainbow);
        foreach(var strip in NativeSummonRainbow.Strips(frame.ScrollPixels,
            rainbow.Width,rainbow.Height,DuelCardLayout.MiniWidth,DuelCardLayout.MiniHeight,center))
            Raylib.DrawTexturePro(rainbow,ToRectangle(strip.Source),ToRectangle(strip.Destination),
                System.Numerics.Vector2.Zero,0,tint);
    }

    static void DrawSpecialSummon(DemoSpecialSummon summon, int elapsedUpdates)
    {
        var frame = NativeSpecialSummonFrame.FromUpdate(elapsedUpdates, effectCosine);
        var center = DuelCardLayout.Center(DuelCardZone.Monster, summon.Player == ViewPlayer, summon.Zone);
        if (frame.SpiralFrame is int sprite && frame.SpiralOpacity > 0)
        {
            // Native 00450100 selects the a/d set by position flag 0x4000.
            string name = summon.InDefense
                ? DatGraphics.SpecialSummonDefenseSprites[sprite]
                : DatGraphics.SpecialSummonAttackSprites[sprite];
            DrawEffectStamp(name, center, 0, 0x10000, frame.SpiralOpacity);
        }

        if (!frame.DrawCardOverlay) return;
        // Native 0054E890 transforms the card at obj+0x108. The resource
        // and size/mask are not fully reconstructed; this is the client's
        // provisional card overlay using original mini/back DAT textures.
        string? full = graphics.Artwork.For(summon.Card);
        string nameOfCard = summon.FaceDownDefense
            ? DatGraphics.DuelBack
            : full is not null && full.StartsWith(@"card\", StringComparison.OrdinalIgnoreCase)
                ? @"mini\" + full[5..] : DatGraphics.DuelBack;
        var texture = graphics.Texture(nameOfCard);
        var geometry = DuelCardLayout.OnBoard(DuelCardZone.Monster,
            summon.Player == ViewPlayer, summon.Zone, summon.InDefense, summon.FaceDownDefense);
        float scale = frame.OverlayScaleFixed8 / 256f;
        float width = geometry.Bounds.Width * scale, height = geometry.Bounds.Height * scale;
        Raylib.DrawTexturePro(texture,
            new Rectangle(0, 0, texture.Width, texture.Height),
            new Rectangle(center.X, center.Y, width, height),
            new System.Numerics.Vector2(width / 2, height / 2),
            geometry.Degrees, Color.White);
    }

    static void DrawHandTransfer(HandTransfer effect)
    {
        var move = effect.Transfer;
        bool near = move.Player == ViewPlayer;
        int count = VisibleHand(move.Player).Count;
        // The arriving card's target moves with the fan as its neighbours
        // reflow. Departing cards retain the pre-removal origin snapshot.
        var handSlot = move.FromDeck
            ? handLayouts[move.Player].Bounds(move.Index, count)
            : effect.Departure ?? HandPresentation.Bounds(move.Index, count + 1, near);
        var pile = move.FromDeck ? NativeDuelLayout.DeckBounds(near) : NativeDuelLayout.GraveBounds(near);
        var bounds = CardTravel.Sample(move.FromDeck ? pile : handSlot, move.FromDeck ? handSlot : pile,
            cardEffects.ElapsedUpdates);
        if (move.FromDeck && near)
        {
            // 00418B60 interpolates the face and orientation over eight
            // native update steps. Cosine scaling previews 00418EF0;
            // the source table and blending are not verified yet.
            var reveal = NativeHandDrawReveal.FromUpdate(cardEffects.ElapsedUpdates, nearSide: true);
            float width = Math.Max(1f, (float)(bounds.Width * reveal.HorizontalScale));
            float left = bounds.X + (bounds.Width - width) / 2f;
            DrawFieldCard(move.Card,
                new Rectangle(left, bounds.Y, width, bounds.Height),
                faceDown: reveal.FaceDown);
        }
        else
            DrawFieldCard(move.Card, ToRectangle(bounds),
                faceDown: !near, upsideDown: !near);
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
