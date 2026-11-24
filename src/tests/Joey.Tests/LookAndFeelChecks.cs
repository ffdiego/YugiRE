using Joey.LookAndFeel;

internal static class LookAndFeelChecks
{
    private sealed class CustomLook : DuelLookAndFeel
    {
        public override string Name => "My Rules";
        public override double Speed => 4;
        protected override TimeSpan BaseDuration(DuelPresentationCue cue) =>
            cue == DuelPresentationCue.FlipEffect ? TimeSpan.FromMilliseconds(800) : base.BaseDuration(cue);
    }

    public static void Run(Action<bool, string> check)
    {
        // Original DAT separates duel particle/HUD scenes from Joey's bust-up
        // reaction, signature summon and duel-result scenes.
        check(NativeVisualArchiveScope.Includes(@"j\duel\effect\vija.txt") &&
              NativeVisualArchiveScope.Includes(@"j\bust_up\reaction02.txt") &&
              NativeVisualArchiveScope.Includes(@"j\bust_up\summon2.txt") &&
              NativeVisualArchiveScope.Includes(@"j\bust_up\win\fukidashi\e_kati01.bmp") &&
              NativeVisualArchiveScope.Includes(@"J\BUST_UP\REACTION\REACTION01_EYES1.BMP"),
              "Visual DAT discovery: duel + bust_up manifests and character sprite layers");
        check(!NativeVisualArchiveScope.Includes(@"j\list\list_bg.bmp") &&
              !NativeVisualArchiveScope.Includes(@"j\bustup\reaction01.txt") &&
              !NativeVisualArchiveScope.Includes(@"j\dueling\effect\vija.txt") &&
              !NativeVisualArchiveScope.Includes(null),
              "Visual DAT discovery: reject adjacent/non-cinematic archive branches");
        // 00403310 routes two resource IDs to bust-up summon constructors;
        // the perspective gate and the Draw are not yet mapped. No
        // invented hold duration is allowed for report-only candidates.
        var redEyesCutin = NativeCinematicRouting.SignatureCandidate(
            1, NativeCinematicRouting.RedEyesResourceId, faceDown: false);
        var alternateCutin = NativeCinematicRouting.SignatureCandidate(
            0, NativeCinematicRouting.AlternateSignatureResourceId, faceDown: false);
        var exodiaCutin = NativeCinematicRouting.ExodiaOpening(0);
        check(redEyesCutin is { Kind: NativeCinematicKind.SignatureSummon,
                               Player: 1, CardResourceId: 81,
                               Manifest: @"j\bust_up\summon.txt" } &&
              alternateCutin is { Kind: NativeCinematicKind.SignatureSummon,
                                  Player: 0, CardResourceId: 744,
                                  Manifest: @"j\bust_up\summon2.txt" },
              "Native cinematic routing: signature candidates preserve IDs and source manifests");
        check(NativeCinematicRouting.SignatureCandidate(0, 81, faceDown: true) is null &&
              NativeCinematicRouting.SignatureCandidate(0, 80, faceDown: false) is null &&
              NativeCinematicRouting.SignatureCandidate(0, 745, faceDown: false) is null,
              "Native cinematic routing: Sets and unrelated monster IDs never cue a bust-up");
        check(exodiaCutin.Kind == NativeCinematicKind.ExodiaOpening &&
              exodiaCutin.Manifest == @"j\duel\exodia\exodia.txt" &&
              exodiaCutin.HasPlayableDraw &&
              NativeCinematicRouting.ImplementedUpdates(exodiaCutin) ==
                    NativeExodiaOpeningFrame.OpeningUpdates &&
              redEyesCutin.HasValue && !redEyesCutin.Value.HasPlayableDraw &&
              !alternateCutin!.Value.HasPlayableDraw &&
              NativeCinematicRouting.ImplementedUpdates(new(
                  NativeCinematicKind.DestinyBoard, 0, 1528,
                  @"j\duel\effect\vija.txt")) is null,
              "Cinematic gate: only the implemented Exodia stage can block the visual FIFO");
        bool invalidCinematicPlayerRejected = false;
        try { NativeCinematicRouting.SignatureCandidate(2, 81, faceDown: false); }
        catch (ArgumentOutOfRangeException) { invalidCinematicPlayerRejected = true; }
        check(invalidCinematicPlayerRejected,
              "Cinematic routing: reject invalid controller instead of misaddressing scene");
        var cinematicOrder = new PresentationSequence<string>();
        cinematicOrder.Enqueue("fifth transfer", CardTravel.Updates);
        if (exodiaCutin.HasPlayableDraw)
            cinematicOrder.Enqueue(exodiaCutin.Kind.ToString(),
                NativeCinematicRouting.ImplementedUpdates(exodiaCutin)!.Value);
        if (redEyesCutin?.HasPlayableDraw == true)
            cinematicOrder.Enqueue(redEyesCutin.Value.Kind.ToString(),
                NativeCinematicRouting.ImplementedUpdates(redEyesCutin.Value)!.Value);
        check(cinematicOrder.Pending.Count() == 2 &&
              cinematicOrder.Current?.Kind == "fifth transfer",
              "Cinematic scheduler: unimplemented signature cues cannot produce invisible input locks");
        cinematicOrder.Advance(CardTravel.Updates);
        check(cinematicOrder.Current?.Kind == "ExodiaOpening" &&
              cinematicOrder.ElapsedUpdates == 0,
              "Cinematic scheduler: Exodia begins on the first sample after the last card flight");

        // Destiny Board is a distinct winner/cinematic route, but Vija
        // 004584C0 is still Pending and must not block the input FIFO.
        var destinyBoardCue = NativeCinematicRouting.DestinyBoardVictory(1);
        check(destinyBoardCue.Kind == NativeCinematicKind.DestinyBoard &&
              destinyBoardCue.Player == 1 && destinyBoardCue.CardResourceId == 1528 &&
              destinyBoardCue.Manifest == @"j\duel\effect\vija.txt" &&
              !destinyBoardCue.HasPlayableDraw &&
              NativeCinematicRouting.ImplementedUpdates(destinyBoardCue) is null,
              "Destiny Board cinematic: separate Vija route without an invented renderer or input wait");

        // DAT contains a separate root cursor/ resource group: localized
        // action-cloud YGAs and two pointer images. These are unrelated
        // to summon.txt/set.txt effect objects drawn on the field.
        check(NativeVisualArchiveScope.Includes(@"cursor\cursor_e.txt") &&
              NativeVisualArchiveScope.Includes(@"CURSOR\CURSOR_E_SUMMON.YGA") &&
              NativeVisualArchiveScope.Includes(@"cursor\cursor00.yga") &&
              !NativeVisualArchiveScope.Includes(@"cursor_fake\cursor00.yga"),
              "Visual DAT inspector includes root cursor assets without adjacent directories");
        check(NativeDuelCursor.UsesSourceAlpha(@"cursor\cursor_e_summon.yga") &&
              NativeDuelCursor.UsesSourceAlpha(@"CURSOR\CURSOR_E_SET.YGA") &&
              NativeDuelCursor.UsesSourceAlpha(NativeDuelCursor.DefaultPointer) &&
              !NativeDuelCursor.UsesSourceAlpha(@"j\duel\effect\atk_eff.yga") &&
              !NativeDuelCursor.UsesSourceAlpha(@"cursor\cursor_e.txt"),
              "Cursor texture upload uses original source alpha, other YGA effects retain black-key policy");
        check(NativeDuelCursor.ManifestEnglish == @"cursor\cursor_e.txt" &&
              NativeDuelCursor.DefaultPointer == @"cursor\cursor00.yga" &&
              NativeDuelCursor.AlternatePointer == @"cursor\cursor01.yga" &&
              NativeDuelCursor.ActionSprite(NativeCursorAction.Summon) ==
                @"cursor\cursor_e_summon.yga" &&
              NativeDuelCursor.ActionSprite(NativeCursorAction.Set) ==
                @"cursor\cursor_e_set.yga" &&
              NativeDuelCursor.ActionSprite(NativeCursorAction.Activate) ==
                @"cursor\cursor_e_act.yga" &&
              NativeDuelCursor.ActionSprite(NativeCursorAction.Attack) ==
                @"cursor\cursor_e_atk.yga" &&
              NativeDuelCursor.ActionSprite(NativeCursorAction.Flip) ==
                @"cursor\cursor_e_flip.yga",
              "Original cursor cloud maps action names to actual English YGA assets");
        // The original screenshot shows a single hand-shaped pointer
        // WITH its Summon cloud; it follows the pointer, not the card center.
        check(NativeDuelCursor.CursorSprite(null) ==
                  NativeDuelCursor.DefaultPointer &&
              NativeDuelCursor.CursorSprite(NativeCursorAction.Summon) ==
                  @"cursor\cursor_e_summon.yga" &&
              NativeDuelCursor.CursorSprite(NativeCursorAction.Set) ==
                  @"cursor\cursor_e_set.yga" &&
              NativeDuelCursor.PointerOrigin(new CardPoint(375, 450)) ==
                  new CardPoint(375, 450),
              "Global cursor: one DAT sprite per frame, with legal hover changing its variant");
        check(NativeDuelCursor.HandAction(true, true, true, false, false, false)
                  == NativeCursorAction.Summon &&
              NativeDuelCursor.HandAction(true, true, true, false, true, false)
                  == NativeCursorAction.Set &&
              NativeDuelCursor.HandAction(false, false, true, false, false, false)
                  == NativeCursorAction.Set &&
              NativeDuelCursor.HandAction(false, false, true, false, true, false)
                  == NativeCursorAction.Set &&
              NativeDuelCursor.HandAction(false, false, false, true, false, false)
                  == NativeCursorAction.Activate &&
              NativeDuelCursor.HandAction(false, false, true, true, false, false)
                  == NativeCursorAction.Activate &&
              NativeDuelCursor.HandAction(false, false, true, true, true, false)
                  == NativeCursorAction.Set &&
              NativeDuelCursor.HandAction(true, false, false, false, false, false) is null &&
              NativeDuelCursor.HandAction(false, false, false, false, false, false) is null &&
              NativeDuelCursor.HandAction(false, false, true, false, false, true) is null,
              "Action cloud: only legal moves, Trap Set-only, both-mode spell and discard fallback");
        check(NativeDuelCursor.ActionForSprite(NativeDuelCursor.DefaultPointer) is null &&
              NativeDuelCursor.ActionForSprite(@"CURSOR\CURSOR_E_ATK.YGA") ==
                  NativeCursorAction.Attack &&
              NativeDuelCursor.ActionForSprite(NativeDuelCursor.ActionSprite(
                  NativeCursorAction.Summon)) == NativeCursorAction.Summon &&
              NativeDuelCursor.ActionForSprite(@"cursor\unknown.yga") is null &&
              NativeDuelCursor.ActionForSprite(null) is null,
              "F12 cursor metadata: action cloud derived from actual sprite, including field Attack");
        check(NativeDuelCursor.FieldMonsterAttackAction(
                  ownMonster: true, canAttack: true, choosingTarget: false) ==
                  NativeCursorAction.Attack &&
              NativeDuelCursor.CursorSprite(NativeCursorAction.Attack) ==
                  @"cursor\cursor_e_atk.yga" &&
              NativeDuelCursor.FieldMonsterAttackAction(
                  ownMonster: true, canAttack: false, choosingTarget: false) is null &&
              NativeDuelCursor.FieldMonsterAttackAction(
                  ownMonster: false, canAttack: true, choosingTarget: false) is null &&
              NativeDuelCursor.FieldMonsterAttackAction(
                  ownMonster: true, canAttack: true, choosingTarget: true) is null,
              "Native cursor: Attack cloud only for Core-legal own monster without targeting");
        check(NativeDuelCursor.CanToggleHandAction(true, true, true, false) &&
              NativeDuelCursor.CanToggleHandAction(false, false, true, true) &&
              !NativeDuelCursor.CanToggleHandAction(false, false, true, false) &&
              !NativeDuelCursor.CanToggleHandAction(false, false, false, true) &&
              !NativeDuelCursor.CanToggleHandAction(true, false, false, false),
              "Right-click only cycles when two legal actions exist");
        // Native detail 0041BE80 / 0041B3E0: the client must not
        // replace the original detail.bmp with raylib-colored rectangles.
        // The English GDI builder uses 14px Courier New-family text,
        // COLORREF (BGR numeric) colors and a 184px scrolling viewport.
        check(NativeDetailPanel.TextX == 8 &&
              NativeDetailPanel.TextY == 365 &&
              NativeDetailPanel.TextRight == 184 &&
              NativeDetailPanel.TextWidth == 176 &&
              NativeDetailPanel.TextBottom == 526 &&
              NativeDetailPanel.ScrollWidgetX == 186 &&
              NativeDetailPanel.ScrollWidgetY == 364 &&
              NativeDetailPanel.TextHeight == 161 &&
              NativeDetailPanel.EnglishTextSize == 14 &&
              NativeDetailPanel.EnglishLineAdvance == 14 &&
              NativeDetailPanel.EnglishFace == "Courier New" &&
              NativeDetailPanel.VisibleLineCount() == 11,
              "Native detail: recovered text viewport, scroll origin and English GDI font contract");
        check(NativeDetailPanel.DecodeColorRef(NativeDetailPanel.NameColorRef) ==
                  ((byte)16, (byte)16, (byte)16) &&
              NativeDetailPanel.DecodeColorRef(NativeDetailPanel.StatsColorRef) ==
                  ((byte)180, (byte)73, (byte)30) &&
              NativeDetailPanel.DecodeColorRef(NativeDetailPanel.DescriptionColorRef) ==
                  ((byte)110, (byte)88, (byte)11),
              "Native detail: decode source COLORREF values as RGB without swapping red/blue");
        var richLines = NativeDetailPanel.Wrap(
            "Dragon Knight", "ATK 2400 / DEF 2000",
            "A long description about damage." + Environment.NewLine + "Another paragraph.",
            value => value.Length * 7, width: 63);
        check(richLines.Take(2).SequenceEqual([
                  new NativeDetailPanel.TextLine("Dragon", NativeDetailPanel.LineKind.Name),
                  new NativeDetailPanel.TextLine("Knight", NativeDetailPanel.LineKind.Name)]) &&
              richLines.Any(line => line.Kind == NativeDetailPanel.LineKind.Stats) &&
              richLines.Any(line => line.Kind == NativeDetailPanel.LineKind.Description) &&
              richLines.All(line => line.Value.Length * 7 <= 63) &&
              NativeDetailPanel.ClampScroll(500, 18, 11) == 7 &&
              NativeDetailPanel.ClampScroll(-500, 18, 11) == 0 &&
              NativeDetailPanel.ClampScroll(5, 4, 11) == 0,
              "Native detail: measured wrapping, explicit paragraphs, viewport and scroll bounds");

        var first = new object();
        var second = new object();
        var arriving = new object();
        var fan = new HandReflow<object>();
        fan.Synchronize([first, second], near: true, updates: 0, animate: false);
        int oldFirstX = fan.Bounds(0, 2).X;
        fan.Synchronize([first, second, arriving], near: true, updates: 1, animate: true);
        int inFlightX = fan.Bounds(0, 3).X;
        int destinationX = HandPresentation.Bounds(0, 3, nearSide: true).X;
        check(inFlightX != oldFirstX && inFlightX != destinationX &&
              Math.Abs(inFlightX - destinationX) < Math.Abs(oldFirstX - destinationX),
              "Hand reflow: existing cards move towards the new fan while a draw is travelling");
        fan.Synchronize([first, arriving], near: true, updates: 1, animate: true);
        check(fan.Bounds(0, 2).X != inFlightX,
              "Hand reflow: removing a card changes the neighbours' layout immediately");

        // Native 004194D0 and number.txt: one 11-column default atlas,
        // two 10-column modified atlases; the slash is glyph index 10.
        string fieldManifest = "duel_fontm.bmp 11\n" +
            "duel_fontm_b.bmp 10\n" + "duel_fontm_r.bmp 10\n";
        check(NativeFieldAttributeFonts.Normal == @"j\duel\font\duel_fontm.bmp" &&
              NativeFieldAttributeFonts.Increased == @"j\duel\font\duel_fontm_b.bmp" &&
              NativeFieldAttributeFonts.Decreased == @"j\duel\font\duel_fontm_r.bmp" &&
              NativeFieldAttributeFonts.SeparatorIndex == 10 &&
              NativeLifeHud.TryFontColumns(fieldManifest, "duel_fontm.bmp") == 11 &&
              NativeLifeHud.TryFontColumns(fieldManifest, "duel_fontm_b.bmp") == 10 &&
              NativeLifeHud.TryFontColumns(fieldManifest, "duel_fontm_r.bmp") == 10 &&
              NativeFieldAttributeFonts.ForValue(1200, 1200) == NativeFieldAttributeFonts.Normal &&
              NativeFieldAttributeFonts.ForValue(1300, 1200) == NativeFieldAttributeFonts.Increased &&
              NativeFieldAttributeFonts.ForValue(1100, 1200) == NativeFieldAttributeFonts.Decreased,
              "Native field font: original normal/boost/debuff atlases and slash index");
        var attackFrame = NativeFieldAttributeFrame.Compose(new CardPoint(525, 398),
            normalGlyphWidth: 8, normalGlyphHeight: 12,
            attack: 1200, defense: 1000, originalAttack: 1200, originalDefense: 1000,
            defensePosition: false);
        var defenseFrame = NativeFieldAttributeFrame.Compose(new CardPoint(525, 398),
            normalGlyphWidth: 8, normalGlyphHeight: 12,
            attack: 1300, defense: 900, originalAttack: 1200, originalDefense: 1000,
            defensePosition: true);
        check(attackFrame.Attack.CenterX == 501 &&
              attackFrame.Defense.CenterX == 549 &&
              attackFrame.SeparatorX == 521 &&
              attackFrame.SeparatorY == 392 &&
              attackFrame.Attack.Opacity == 256 &&
              attackFrame.Defense.Opacity == 160 &&
              attackFrame.SeparatorOpacity == 160 &&
              defenseFrame.Attack.Opacity == 160 &&
              defenseFrame.Defense.Opacity == 256 &&
              defenseFrame.Attack.Sprite == NativeFieldAttributeFonts.Increased &&
              defenseFrame.Defense.Sprite == NativeFieldAttributeFonts.Decreased,
              "Native field ATK/DEF: positions, active/inactive alpha and modified palettes");
        check(new NativeFieldNumberPass(NativeFieldAttributeFonts.Normal, 11,
                  123, 501, 0, 160).LeftX(7) == 490 &&
              new NativeFieldNumberPass(NativeFieldAttributeFonts.Normal, 11,
                  0, 501, 0, 160).LeftX(7) == 497 &&
              NativeFieldAttributeFrame.SmoothDisplayed(1000, 2000) == 1125 &&
              NativeFieldAttributeFrame.SmoothDisplayed(2000, 1000) == 1875 &&
              NativeFieldAttributeFrame.SmoothDisplayed(1000, 1001) == 1001,
              "Native field attributes: odd-width glyph anchoring and 7/8 signed smoothing");

        // The native object's 00418B10 initializes to the card's base
        // stats. 004194D0 eases toward effective stats once per visual tick.
        // The client is 60 FPS but Classic is 30 updates/s, so zero updates
        // must not move the digits (including while F6 is paused).
        var fieldTimeline = new NativeFieldAttributeTimeline();
        object fieldCardA = new(), fieldCardB = new();
        var fieldAtSpawn = fieldTimeline.Sample(fieldCardA, 1000, 1000,
            targetAttack: 1800, targetDefense: 600,
            presentationUpdates: 0, animate: true);
        check(fieldAtSpawn == new NativeFieldDisplayedAttributes(1000, 1000) &&
              fieldTimeline.HasCard &&
              fieldTimeline.Sample(fieldCardA, 1000, 1000, 1800, 600, 0, true) ==
                  fieldAtSpawn &&
              fieldTimeline.Sample(fieldCardA, 1000, 1000, 1800, 600, 1, true) ==
                  new NativeFieldDisplayedAttributes(1100, 950) &&
              fieldTimeline.Sample(fieldCardA, 1000, 1000, 1800, 600, 0, true) ==
                  new NativeFieldDisplayedAttributes(1100, 950) &&
              fieldTimeline.Sample(fieldCardA, 1000, 1000, 1800, 600, 1, true) ==
                  new NativeFieldDisplayedAttributes(1188, 906),
              "Native attribute easing: seed from CARD_Prop and hold on zero updates");
        // Multiple accumulated ticks catch up after a long frame. Replacing
        // a card with identical base attributes resets its smoothing.
        var batchTimeline = new NativeFieldAttributeTimeline();
        var multi = batchTimeline.Sample(fieldCardA, 1000, 1000, 1800, 600, 2, true);
        check(multi == new NativeFieldDisplayedAttributes(1188, 906) &&
              batchTimeline.Sample(fieldCardB, 1000, 1000, 1800, 600, 0, true) ==
                  new NativeFieldDisplayedAttributes(1000, 1000) &&
              batchTimeline.Sample(fieldCardB, 1000, 1000, 1800, 600, 0, false) ==
                  new NativeFieldDisplayedAttributes(1800, 600),
              "Native attribute easing: accumulated ticks, identity reset and Instant");
        batchTimeline.Reset();
        check(!batchTimeline.HasCard &&
              batchTimeline.Sample(fieldCardA, 1300, 1400, 1300, 1400, 3, true) ==
                  new NativeFieldDisplayedAttributes(1300, 1400),
              "Native attribute easing: emptied slot never retains prior occupant numbers");
        bool invalidAttributeTickRejected = false;
        try { fieldTimeline.Sample(fieldCardA, 1000, 1000, 1100, 1100, -1, true); }
        catch (ArgumentOutOfRangeException) { invalidAttributeTickRejected = true; }
        check(invalidAttributeTickRejected, "Native attribute easing: negative updates rejected");

        // Native card Draw gate 00418EF0 -> 004194D0. The object must
        // be fully settled, the descriptor must address a monster row,
        // and 0056DD30 must agree on which card occupies that slot.
        // Bit 0x8000 is the native face-up/revealed flag.
        static ushort NativeFieldDescriptor(int side, int region, int offset, bool faceUp) =>
            checked((ushort)(side | (region << 1) | (offset << 6) |
                (faceUp ? NativeFieldAttributeVisibility.FaceUpDescriptorFlag : 0)));
        ushort ownSet = NativeFieldDescriptor(0, 0, 2, false);
        ushort enemySet = NativeFieldDescriptor(1, 0, 2, false);
        ushort enemyFaceUp = NativeFieldDescriptor(1, 0, 2, true);
        bool NativeNumbers(ushort descriptor, int state = 0, int counter = 0x100,
            bool visible = true, ushort cardId = 77, ushort occupiedId = 77) =>
            NativeFieldAttributeVisibility.ShouldDrawNative(
                descriptor, perspectiveSide: 0,
                cardResourceId: cardId, fieldOccupantResourceId: occupiedId,
                objectVisible: visible, movementCounter: counter,
                animationState: state);
        check(NativeNumbers(ownSet) &&
              !NativeNumbers(enemySet) &&
              NativeNumbers(enemyFaceUp) &&
              !NativeNumbers(ownSet, counter: 0xe0) &&
              !NativeNumbers(ownSet, state: NativeFieldAttributeVisibility.ExitAnimationState) &&
              !NativeNumbers(ownSet, visible: false) &&
              !NativeNumbers(ownSet, occupiedId: 78) &&
              !NativeNumbers(NativeFieldDescriptor(0, 1, 2, true)) &&
              !NativeNumbers(NativeFieldDescriptor(0, 0, 11, true)),
              "Native attribute Draw gate: settled field occupant, perspective, visibility and identity");
        check(NativeFieldAttributeVisibility.ShouldDrawNative(
                  NativeFieldDescriptor(1, 0, 4, false), 1, 42, 42, true, 256, 0) &&
              !NativeFieldAttributeVisibility.ShouldDrawClient(
                  ownMonster: false, faceDown: true,
                  settled: true, hiddenDefenderBeforeImpact: false) &&
              NativeFieldAttributeVisibility.ShouldDrawClient(
                  ownMonster: true, faceDown: true,
                  settled: true, hiddenDefenderBeforeImpact: true) &&
              !NativeFieldAttributeVisibility.ShouldDrawClient(
                  ownMonster: false, faceDown: false,
                  settled: true, hiddenDefenderBeforeImpact: true) &&
              NativeFieldAttributeVisibility.ShouldDrawClient(
                  ownMonster: false, faceDown: false,
                  settled: true, hiddenDefenderBeforeImpact: false) &&
              !NativeFieldAttributeVisibility.ShouldDrawClient(
                  ownMonster: true, faceDown: false,
                  settled: false, hiddenDefenderBeforeImpact: false),
              "Native attribute client gate: same mask for F12 and draw, no enemy stats before sword impact");

        // 00419B70 distinguishes type and attribute changes against
        // CARD_Prop: glyph banks #5 (class) and #4 (attribute). These are
        // NOT ordinary raw ATK/DEF values and must not be conflated.
        var bothChanged = NativeCardStatusOverlay.IdentityChanges(
            originalClassCode: 1, effectiveClassCode: 5,
            originalAttributeCode: 2, effectiveAttributeCode: 6);
        check(bothChanged == new NativeCardIdentityOverlays(true, true) &&
              NativeCardStatusOverlay.ChangedAttributeIndex == 4 &&
              NativeCardStatusOverlay.ChangedTypeIndex == 5 &&
              NativeCardStatusOverlay.IdentityChanges(1, 1, 2, 2) ==
                  new NativeCardIdentityOverlays(false, false) &&
              NativeCardStatusOverlay.IdentityChanges(1, 0, 2, 7) ==
                  new NativeCardIdentityOverlays(false, false) &&
              NativeCardStatusOverlay.IdentityChanges(1, 20, 2, 5) ==
                  new NativeCardIdentityOverlays(true, true),
              "Native status change: resource indices, original type/attribute comparison and range gate");

        // FUN_0041A070: bits 2..5, icon #6, 3-frame idle cycling;
        // state 3 uses frames 2..5 before 0x100 and tail icon #7 after.
        var status6 = NativeCardStatusOverlay.First(
            fieldFlags: 0x14, animationState: 0,
            animationCounter: 0, nativeGlobalCounter: 32);
        var status6Begin = NativeCardStatusOverlay.First(
            fieldFlags: 0, animationState: 3,
            animationCounter: 0, nativeGlobalCounter: 0);
        var status6End = NativeCardStatusOverlay.First(
            fieldFlags: 0, animationState: 3,
            animationCounter: 255, nativeGlobalCounter: 0);
        var status6Tail = NativeCardStatusOverlay.First(
            fieldFlags: 0, animationState: 3,
            animationCounter: 272, nativeGlobalCounter: 16);
        check(status6 == new NativeCardStatusFrame(6, 2, 5, null, null) &&
              status6Begin == new NativeCardStatusFrame(6, 2, 0, null, null) &&
              status6End == new NativeCardStatusFrame(6, 5, 0, null, null) &&
              status6Tail == new NativeCardStatusFrame(6, 1, 0, 7, 7) &&
              NativeCardStatusOverlay.First(0, 0, 0, 0) is null,
              "Native status icon 6/7: exact flag bits, idle and transition frame routing");

        // FUN_0041A3B0: bits 6..8, icon #8 and tail #9; state 4
        // samples frames 0..3 in 64-counter intervals.
        var status8 = NativeCardStatusOverlay.Second(
            fieldFlags: 0x140, animationState: 0, animationCounter: 0);
        var status8Mid = NativeCardStatusOverlay.Second(
            fieldFlags: 0, animationState: 4, animationCounter: 128);
        var status8Tail = NativeCardStatusOverlay.Second(
            fieldFlags: 0, animationState: 4, animationCounter: 272);
        check(status8 == new NativeCardStatusFrame(8, 0, 5, null, null) &&
              status8Mid == new NativeCardStatusFrame(8, 2, 0, null, null) &&
              status8Tail == new NativeCardStatusFrame(8, 0, 0, 9, 7) &&
              NativeCardStatusOverlay.Second(0, 0, 0) is null,
              "Native status icon 8/9: bits 6..8, transition frames and tail");

        check(NativeCardStatusOverlay.AttributeChangeFlashIndex(true, 0) == 10 &&
              NativeCardStatusOverlay.AttributeChangeFlashIndex(true, 4) == 11 &&
              NativeCardStatusOverlay.AttributeChangeFlashIndex(false, 0) == 12 &&
              NativeCardStatusOverlay.AttributeChangeFlashIndex(false, 4) == 13,
              "Native ATK/DEF change flash: global counter selects original card bank 10..13");
        bool invalidStatusCounterRejected = false;
        try { NativeCardStatusOverlay.Second(0, 4, -1); }
        catch (ArgumentOutOfRangeException) { invalidStatusCounterRejected = true; }
        check(invalidStatusCounterRejected, "Native status cues reject negative native counters");

        // Status overlay geometry: these offsets use the miniature-card
        // image height DAT_005F3008, not the ATK/DEF digit atlas height.
        // X depends on sprite dimensions that Ghidra currently mis-types;
        // do not assert imaginary absolute icon positions.
        var statusSameSide = NativeCardStatusGeometry.FromNative(
            new CardPoint(525, 352), miniHeight: 72,
            perspectiveSide: 0, descriptorSide: 0);
        var statusOtherSide = NativeCardStatusGeometry.FromNative(
            new CardPoint(525, 352), miniHeight: 72,
            perspectiveSide: 0, descriptorSide: 1);
        var statusHeightOdd = NativeCardStatusGeometry.FromNative(
            new CardPoint(525, 248), miniHeight: 73,
            perspectiveSide: 1, descriptorSide: 1);
        check(statusSameSide == new NativeCardStatusGeometry(1, 316, 383) &&
              statusOtherSide == new NativeCardStatusGeometry(-1, 388, 321) &&
              statusHeightOdd == new NativeCardStatusGeometry(1, 212, 279) &&
              NativeCardStatusGeometry.SourceFrame(96, 18, 6, 4) ==
                  new CardBounds(64, 0, 16, 18) &&
              NativeCardStatusGeometry.SourceFrame(160, 20, 8, 7) ==
                  new CardBounds(140, 0, 20, 20),
              "Native card status: card-height-driven vertical anchors and horizontal frame slicing");
        bool invalidStatusAtlasRejected = false;
        try { _ = NativeCardStatusGeometry.SourceFrame(97, 18, 6, 4); }
        catch (ArgumentException) { invalidStatusAtlasRejected = true; }
        check(invalidStatusAtlasRejected,
              "Native card status: non-divisible DAT atlas must not be silently cropped");

        // 005B9470 routes opcodes 0x5F (set four-bit first status),
        // 0x60 (saturating add) and 0x69 (three-bit minimum/replace).
        // Both animated branches set their objects' transition counter
        // to 0x200, not 0x100; last digit/flag semantics remain pending.
        check(NativeCardStatusCounters.DirectSetFirstCommand == 0x5f &&
              NativeCardStatusCounters.AddCommand == 0x60 &&
              NativeCardStatusCounters.MinimumCommand == 0x69 &&
              NativeCardStatusCounters.TransitionCounter == 0x200 &&
              NativeCardStatusCounters.SetFirst(0x2f) == 15 &&
              NativeCardStatusCounters.AddFirst(12, 2) == 14 &&
              NativeCardStatusCounters.AddFirst(14, 2) == 15 &&
              NativeCardStatusCounters.AddFirst(0, 0) == 0 &&
              NativeCardStatusCounters.MinimumSecond(0, 5) == 5 &&
              NativeCardStatusCounters.MinimumSecond(5, 3) == 3 &&
              NativeCardStatusCounters.MinimumSecond(3, 5) == 3 &&
              NativeCardStatusCounters.MinimumSecond(0, 8) == 0,
              "Native card status mutation: add capped at 15, minimum masked to 3 bits");
        bool invalidNativeStatusInputRejected = false;
        try { _ = NativeCardStatusCounters.MinimumSecond(-1, 4); }
        catch (ArgumentOutOfRangeException) { invalidNativeStatusInputRejected = true; }
        check(invalidNativeStatusInputRejected,
              "Native card status mutation rejects negative current values");

        // 00418B60 updates states 3/4 only after the card has settled:
        // the countdown starts at 512 and moves backward by 16/update.
        // A complete status effect lasts 32 native Updates; index 7/9
        // is visible first, index 6/8 follows after the 0x100 boundary.
        var statusTransition = NativeCardStatusTransition.First();
        check(statusTransition.Active &&
              statusTransition.State == 3 &&
              statusTransition.Counter == 512 &&
              statusTransition.Advance(cardSettled: false) == statusTransition &&
              NativeCardStatusTransition.Second().State == 4 &&
              NativeCardStatusTransition.TotalUpdates == 32,
              "Native card status timeline: initial state and movement gate");
        statusTransition = statusTransition.Advance(cardSettled: true);
        var firstTailSample = NativeCardStatusOverlay.First(
            0, statusTransition.State, statusTransition.Counter, 0);
        check(statusTransition.Counter == 496 &&
              firstTailSample is { TailResourceIndex: 7, TailFrame: 1 },
              "Native card status timeline: decreasing initial tail frames");
        for (int i = 1; i < 16; i++)
            statusTransition = statusTransition.Advance(cardSettled: true);
        var lastTailSample = NativeCardStatusOverlay.First(
            0, statusTransition.State, statusTransition.Counter, 0);
        check(statusTransition.Counter == 256 &&
              lastTailSample is { TailResourceIndex: 7, TailFrame: 0 },
              "Native card status timeline: 0x100 still uses tail");
        statusTransition = statusTransition.Advance(cardSettled: true);
        var firstMainSample = NativeCardStatusOverlay.First(
            0, statusTransition.State, statusTransition.Counter, 0);
        check(statusTransition.Counter == 240 &&
              firstMainSample is { MainFrame: 5, TailResourceIndex: null },
              "Native card status timeline: state 3 main strip begins at 0xF0");
        for (int i = 17; i < NativeCardStatusTransition.TotalUpdates; i++)
            statusTransition = statusTransition.Advance(cardSettled: true);
        check(statusTransition == default &&
              !statusTransition.Active &&
              NativeCardStatusTransition.Second().Advance(cardSettled: true) ==
                  new NativeCardStatusTransition(4, 496),
              "Native card status timeline: exit after 32 updates and state 4 shares countdown");

        // Both native status Draws select resource 8 of font/number.txt,
        // the 70x10 yellow atlas with ten 7x10 numeral cells. This is
        // original DAT metadata, independent of speculative icon sprites.
        check(NativeCardStatusDigits.OriginalFontResourceIndex == 8 &&
              NativeCardStatusDigits.FontResource == @"j\duel\font\duel_fontm_y.bmp" &&
              NativeCardStatusDigits.Columns == 10 &&
              NativeCardStatusDigits.EvidenceAtlasWidth == 70 &&
              NativeCardStatusDigits.EvidenceAtlasHeight == 10 &&
              NativeLifeHud.TryFontColumns("duel_fontm_y.bmp 10\n",
                  "duel_fontm_y.bmp") == 10,
              "Native status digits: original yellow number atlas path and column count");

        // 00480690 centers the full digit string, then calls 004803C0;
        // that callee advances right-to-left, drawing units first.
        var centeredStatusDigits = NativeCardStatusDigits.Centered(
            new CardPoint(525, 383), 15, 70, 10);
        check(centeredStatusDigits.Length == 2 &&
              centeredStatusDigits[0] == new NativeCardStatusDigitPass(
                  5, new CardBounds(35, 0, 7, 10), new CardPoint(525, 378)) &&
              centeredStatusDigits[1] == new NativeCardStatusDigitPass(
                  1, new CardBounds(7, 0, 7, 10), new CardPoint(518, 378)),
              "Native status numeric A: centered 15 and right-to-left source/destination order");

        // The B Draw calls 004803C0 directly with a right-edge X.
        var rightStatusDigits = NativeCardStatusDigits.RightAnchored(
            rightEdge: 542, centerY: 321, value: 7, atlasWidth: 70, atlasHeight: 10);
        var zeroStatusDigits = NativeCardStatusDigits.Centered(
            new CardPoint(525, 383), 0, 70, 10);
        check(rightStatusDigits.Length == 1 &&
              rightStatusDigits[0] == new NativeCardStatusDigitPass(
                  7, new CardBounds(49, 0, 7, 10), new CardPoint(535, 316)) &&
              zeroStatusDigits.Length == 1 &&
              zeroStatusDigits[0] == new NativeCardStatusDigitPass(
                  0, new CardBounds(0, 0, 7, 10), new CardPoint(521, 378)),
              "Native status numeric B: right-anchored digit, zero renders one glyph");

        // Assembly received in ghidra-card-status-contract.zip:
        // main atlas widths are divided by the manifest count before
        // the draw and the mini-card width supplies both edge anchors.
        // Use synthetic sprite dimensions, not invented DAT facts.
        var firstLayout = NativeStatusDrawLayout.First(525, 383, 50,
            new NativeCardStatusFrame(6, 2, 15, 7, 4),
            new NativeStatusAtlas(72, 15, 6), new NativeStatusAtlas(160, 19, 8));
        check(firstLayout.Main == new NativeStatusSpritePass(6,
                  new CardBounds(24, 0, 12, 15), new CardPoint(500, 375)) &&
              firstLayout.NumberAnchor == new CardPoint(518, 383) &&
              firstLayout.NumberCentered &&
              firstLayout.Transition == new NativeStatusSpritePass(7,
                  new CardBounds(80, 0, 20, 19), new CardPoint(508, 374)),
              "Native status A: mini width, source frame width, rounded Y and transition center");
        var secondLayout = NativeStatusDrawLayout.Second(525, 321, 50,
            new NativeCardStatusFrame(8, 1, 7, 9, 3),
            new NativeStatusAtlas(48, 13, 4), new NativeStatusAtlas(128, 17, 8));
        check(secondLayout.Main == new NativeStatusSpritePass(8,
                  new CardBounds(12, 0, 12, 13), new CardPoint(527, 315)) &&
              secondLayout.NumberAnchor == new CardPoint(547, 321) &&
              !secondLayout.NumberCentered &&
              secondLayout.Transition == new NativeStatusSpritePass(9,
                  new CardBounds(48, 0, 16, 17), new CardPoint(535, 313)),
              "Native status B: right edge minus frame+8, tail centered at right-4");
        check(NativeStatusDrawLayout.SecondRegionEligible(0x8008) &&
              !NativeStatusDrawLayout.SecondRegionEligible(0x800a),
              "Native status B has additional descriptor region gate");
        // 00419B70 uses code-1 in the atlas and opposite mirrored
        // mini-card edges. These dimensions are synthetic test fixtures.
        var typeIcon = NativeStatusDrawLayout.IdentityIcon(false, 5,
            new CardPoint(525, 352), 50, statusSameSide, new NativeStatusAtlas(240, 15, 20));
        var attrIcon = NativeStatusDrawLayout.IdentityIcon(true, 6,
            new CardPoint(525, 352), 50, statusOtherSide, new NativeStatusAtlas(72, 15, 6));
        check(typeIcon == new NativeStatusSpritePass(5,
                  new CardBounds(48, 0, 12, 15), new CardPoint(494, 309)) &&
              attrIcon == new NativeStatusSpritePass(4,
                  new CardBounds(60, 0, 12, 15), new CardPoint(494, 381)),
              "Native identity change icons: effective code minus one, mirrored edges");
        check(NativeStatusDrawLayout.AttributeFlash(545, 393, 200, 6) ==
                  new NativeStatusFlash(11, new CardPoint(545, 383), 256) &&
              NativeStatusDrawLayout.AttributeFlash(505, 393, -200, 6) ==
                  new NativeStatusFlash(13, new CardPoint(505, 387), 256) &&
              NativeStatusDrawLayout.AttributeFlash(505, 393, 0, 6) is null,
              "Native stat flash: sign of pre-smoothing delta, global-counter bob and intensity");

        bool missingTailDimensionsRejected = false;
        try
        {
            _ = NativeStatusDrawLayout.First(525, 383, 50,
                new NativeCardStatusFrame(6, 2, 15, 7, 4),
                new NativeStatusAtlas(72, 15, 6));
        }
        catch (ArgumentException) { missingTailDimensionsRejected = true; }
        check(missingTailDimensionsRejected, "Native status transition requires its actual atlas dimensions");
        var oddFieldFont = NativeFieldAttributeFrame.Compose(new CardPoint(525, 398),
            7, 11, 1200, 1000, 1200, 1000, false);
        check(oddFieldFont.Defense.CenterX == 545 &&
              oddFieldFont.Attack.CenterX == 505 &&
              oddFieldFont.SeparatorX == 521 &&
              oddFieldFont.SeparatorY == 393,
              "Native ATK/DEF odd glyph width uses two separate divisions and right-anchored separator");

        bool invalidStatusDigitAtlasRejected = false;
        try
        {
            _ = NativeCardStatusDigits.Centered(
                new CardPoint(525, 383), 15, atlasWidth: 69, atlasHeight: 10);
        }
        catch (ArgumentException) { invalidStatusDigitAtlasRejected = true; }
        bool negativeStatusDigitRejected = false;
        try
        {
            _ = NativeCardStatusDigits.RightAnchored(525, 383, -1, 70, 10);
        }
        catch (ArgumentOutOfRangeException) { negativeStatusDigitRejected = true; }
        check(invalidStatusDigitAtlasRejected && negativeStatusDigitRejected,
              "Native status numeric layout rejects malformed atlas and negative value");

        // The owner sees ATK/DEF for their own face-down Set monster.
        // Keep the opponent's hidden monster attributes concealed. Attack
        // and defense position do not affect ownership or visibility.
        foreach (bool defense in new[] { false, true })
        {
            var ownFaceDown = DuelCardLayout.OnBoard(
                DuelCardZone.Monster, nearSide: true, slot: 2,
                defense: defense, faceDown: true);
            var opponentFaceDown = DuelCardLayout.OnBoard(
                DuelCardZone.Monster, nearSide: false, slot: 2,
                defense: defense, faceDown: true);
            var opponentFaceUp = DuelCardLayout.OnBoard(
                DuelCardZone.Monster, nearSide: false, slot: 2,
                defense: defense, faceDown: false);
            check(ownFaceDown.FaceDown &&
                  ownFaceDown.AttributesCenter is { } ownText &&
                  ownText.Y == DuelCardLayout.Center(
                      DuelCardZone.Monster, nearSide: true, slot: 2).Y + 46 &&
                  opponentFaceDown.AttributesCenter is null &&
                  opponentFaceUp.AttributesCenter is not null,
                  $"Face-down monster attributes: visible only to owner; defense={defense}");
        }
        check(DuelCardLayout.OnBoard(DuelCardZone.Spell, true, 2, false, false)
                  .AttributesCenter is null,
              "No ATK/DEF in non-monster zones");

        var revealStart = NativeHandDrawReveal.FromUpdate(0, nearSide: true);
        var revealMid = NativeHandDrawReveal.FromUpdate(4, nearSide: true);
        var revealEnd = NativeHandDrawReveal.FromUpdate(CardTravel.Updates, nearSide: true);
        check(revealStart.FaceDown && !revealMid.FaceDown && !revealEnd.FaceDown &&
              Math.Abs(revealStart.HorizontalScale - 1) < 0.00001 &&
              revealMid.HorizontalScale < 0.00001 &&
              Math.Abs(revealEnd.HorizontalScale - 1) < 0.00001,
              "Own draw: face-down at deck, edge-on halfway, face-up when landing");
        check(Math.Abs(NativeHandDrawReveal.FromUpdate(2, true).HorizontalScale -
                       NativeHandDrawReveal.FromUpdate(6, true).HorizontalScale) < 0.00001 &&
              NativeHandDrawReveal.FromUpdate(2, false).FaceDown &&
              NativeHandDrawReveal.FromUpdate(7, false).FaceDown &&
              NativeHandDrawReveal.FromUpdate(7, false).HorizontalScale == 1,
              "CPU drawn card stays hidden; player reveal has a symmetric preview");

        check(NativeCardSelectionFrame.LargeSprite(nearSide: true) ==
                  @"j\duel\card\card_waku#l.bmp" &&
              NativeCardSelectionFrame.LargeSprite(nearSide: false) ==
                  @"j\duel\card\card_waku#l2.bmp" &&
              NativeCardSelectionFrame.SmallSprite(nearSide: true) ==
                  @"j\duel\card\card_waku#s.bmp" &&
              NativeCardSelectionFrame.SmallSprite(nearSide: false) ==
                  @"j\duel\card\card_waku#s2.bmp" &&
              NativeCardSelectionFrame.IsFrame(@"J\DUEL\CARD\CARD_WAKU#L.BMP") &&
              !NativeCardSelectionFrame.IsFrame(@"j\duel\card\card_ura.bmp"),
              "Native card selection: original four DAT sprite slots, own/far variants");
        check(NativeCardSelectionFrame.Outline(new CardBounds(375, 450, 100, 145), 108, 153) ==
                  new CardBounds(371, 446, 108, 153) &&
              NativeCardSelectionFrame.Outline(new CardBounds(325, 300, 50, 72), 58, 80) ==
                  new CardBounds(321, 296, 58, 80),
              "Native selection frame uses four-pixel padding around both card sizes");

        check(HandPresentation.Bounds(0, 1, nearSide: false, lift: 32).Y -
                  HandPresentation.Bounds(0, 1, nearSide: false).Y == 32 &&
              HandPresentation.Bounds(0, 1, nearSide: true, lift: 32).Y -
                  HandPresentation.Bounds(0, 1, nearSide: true).Y == -32,
              "Original near hand lifts 32px; Joey selected far-side card drops 32px");
        check(NativeCardSelectionFrame.Outline(
                  new CardBounds(320, 250, 72, 50), 80, 58) ==
                  new CardBounds(316, 246, 80, 58),
              "Field selection: original small frame has 4px padding in defense orientation");

        // A 58x80 selection BMP has a 80x58 footprint AFTER a 90/270
        // rotation. The DrawTexturePro destination stays source-sized.
        foreach (int degrees in new[] { 90, 270 })
            check(NativeCardSelectionFrame.ProjectedBounds(
                      new CardBounds(320, 250, 72, 50), 58, 80, degrees) ==
                      new CardBounds(316, 246, 80, 58),
                  $"Field defense focus: 58x80 rotates exactly once ({degrees} degrees)");
        foreach (int degrees in new[] { 0, 180 })
            check(NativeCardSelectionFrame.ProjectedBounds(
                      new CardBounds(325, 300, 50, 72), 58, 80, degrees) ==
                      new CardBounds(321, 296, 58, 80),
                  $"Field attack focus: 58x80 footprint stays upright ({degrees} degrees)");
        check(NativeDetailPanel.PreviewBounds ==
                  new CardBounds(8, 75, 200, 290) &&
              NativeDetailPanel.PreviewBounds.Y +
                  NativeDetailPanel.PreviewBounds.Height == NativeDetailPanel.TextY,
              "Native sidebar: 200x290 at x=8 aligned with original paired screenshot");

        check(NativeCardRaster.Project(200, 290, 50, 72, 0) == (50, 72) &&
              NativeCardRaster.Project(200, 290, 72, 50, 90) == (50, 72) &&
              NativeCardRaster.Project(200, 290, 200, 290, 0) == (200, 290) &&
              NativeCardRaster.Project(200, 290, 100, 145, 0) == (100, 145),
              "Card artwork draws at integer pixel dimensions in field/hand/sidebar");

        check(NativeCardRaster.IsNativeMiniDestination(50, 72) &&
              NativeCardRaster.IsNativeMiniDestination(72, 50) &&
              !NativeCardRaster.IsNativeMiniDestination(100, 145) &&
              !NativeCardRaster.IsNativeMiniDestination(200, 290) &&
              !NativeCardRaster.IsNativeMiniDestination(50, 70),
              "Native mini art used only by 50x72 field cards (including defense)");
        // Native window: the original Draw is 0043B280, Update 0043B0F0.
        // These are pure-contract tests; there is no rendered C# window yet.
        var fiveSelection = new NativeSelectionWindow(7);
        check(fiveSelection.TotalCards == 7 &&
              fiveSelection.VisibleCount == 5 &&
              fiveSelection.MaximumScroll == 2 &&
              fiveSelection.SelectedCardIndex == 0 &&
              fiveSelection.OpeningCounter == 0x100 &&
              !fiveSelection.EntranceComplete,
              "Native selection: starts with five visible cards and +256 opening counter");
        for (int n = 1; n <= 8; n++)
        {
            fiveSelection.AdvanceOpening();
            check(fiveSelection.OpeningCounter == 0x100 - n * 0x20,
                  $"Native selection: opening tick {n}/8 decrements by 0x20");
        }
        check(fiveSelection.EntranceComplete &&
              !fiveSelection.MovePrevious() &&
              fiveSelection.SelectedCardIndex == 0,
              "Native selection: entrance complete and previous at first item is a no-op");

        var selectionTrace = new (int Slot, int Scroll, int Absolute)[]
        {
            (0, 0, 0), (1, 0, 1), (2, 0, 2), (3, 0, 3),
            (4, 0, 4), (4, 1, 5), (4, 2, 6)
        };
        foreach (var (slot, scroll, absolute) in selectionTrace)
        {
            check(fiveSelection.SelectedSlot == slot &&
                  fiveSelection.ScrollOffset == scroll &&
                  fiveSelection.SelectedCardIndex == absolute &&
                  fiveSelection.CardIndexAtSlot(slot) == absolute,
                  $"Native selection: sliding window ({slot}, {scroll}) -> index {absolute}");
            if (absolute != 6) check(fiveSelection.MoveNext(),
                                     $"Native selection: advance from absolute card {absolute}");
        }
        check(!fiveSelection.MoveNext() &&
              fiveSelection.CardIndexAtSlot(0) == 2 &&
              fiveSelection.CardIndexAtSlot(4) == 6 &&
              fiveSelection.CardIndexAtSlot(5) == -1,
              "Native selection: scrolling clamps at final item and keeps exactly five slots");
        for (int i = 0; i < 6; i++) check(fiveSelection.MovePrevious(),
                                            $"Native selection: backwards step {i + 1}");
        check(fiveSelection.SelectedSlot == 0 && fiveSelection.ScrollOffset == 0,
              "Native selection: reversed scrolling reaches the first item");
        // 0043E0A0 explicitly arms exit at -1; 0043E280 then decrements
        // by 0x20 until it crosses below -0xFF on update 8.
        check(fiveSelection.BeginClosing() &&
              fiveSelection.TransitionCounter == -1 &&
              !fiveSelection.EntranceComplete &&
              !fiveSelection.IsClosed &&
              !fiveSelection.MoveNext(),
              "Native selection: closing starts at -1 and gates further input");
        for (int step = 1; step <= 8; step++)
        {
            fiveSelection.AdvanceClosing();
            check(fiveSelection.ClosingCounter == -1 - step * 0x20 &&
                  fiveSelection.TransitionCounter == fiveSelection.ClosingCounter &&
                  fiveSelection.IsClosed == (step == 8),
                  $"Native selection: close update {step}/8 reaches exact counter");
        }
        check(!fiveSelection.BeginClosing() && !fiveSelection.MovePrevious(),
              "Native selection: object deactivates once when counter is below -255");
        fiveSelection.AdvanceClosing();
        check(fiveSelection.ClosingCounter == -257 &&
              fiveSelection.IsClosed, "Native selection: deactivated object cannot tick again");
        var shortSelection = new NativeSelectionWindow(3);
        check(!shortSelection.MoveNext(),
              "Native selection: input is gated during the eight opening updates");
        shortSelection.AdvanceOpening(8);
        check(shortSelection.VisibleCount == 3 && shortSelection.MaximumScroll == 0 &&
              shortSelection.CardIndexAtSlot(3) == -1 &&
              shortSelection.MoveNext() && shortSelection.MoveNext() &&
              !shortSelection.MoveNext(),
              "Native selection: no extra cards or phantom scrolling for short lists");
        var emptySelection = new NativeSelectionWindow(0);
        check(emptySelection.VisibleCount == 0 && emptySelection.SelectedCardIndex == -1 &&
              !emptySelection.MoveNext() && !emptySelection.MovePrevious(),
              "Native selection: empty list is inert");

        check(NativeSelectionWindow.SlotAnchor(525, 300, 39, 191, 0) ==
                  new CardPoint(397, 244) &&
              NativeSelectionWindow.SlotAnchor(525, 300, 39, 191, 4) ==
                  new CardPoint(653, 244),
              "Native selection: slot X spacing from 0043ABA0 and Y uses supplied resource fields");
        // Assembly 0043B2F6..0043B31B gives NEGATIVE index offsets.
        // A cosine-symmetric test fixture previously concealed that our
        // sign was reversed; distinguish all indices explicitly.
        check(NativeSelectionWindow.TransitionCosineIndex(0) == 0 &&
              NativeSelectionWindow.TransitionCosineIndex(128) == 448 &&
              NativeSelectionWindow.TransitionCosineIndex(-128) == 448 &&
              NativeSelectionWindow.TransitionCosineIndex(256) == 384 &&
              NativeSelectionWindow.TransitionCosineIndex(-1) == 511 &&
              NativeSelectionWindow.TransitionCosineIndex(-33) == 495 &&
              NativeSelectionWindow.TransitionCosineIndex(-225) == 399,
              "Native selection: x86 cosine index uses signed -(abs(counter)+1)/2 and rounds odd counts");
        var nativeSelectionCosine = new int[512];
        nativeSelectionCosine[0] = 65536;
        nativeSelectionCosine[448] = 32768;
        nativeSelectionCosine[384] = 0;
        nativeSelectionCosine[511] = 49152;
        check(NativeSelectionWindow.TransitionScaleFixed8(0, nativeSelectionCosine) == 256 &&
              NativeSelectionWindow.TransitionScaleFixed8(128, nativeSelectionCosine) == 128 &&
              NativeSelectionWindow.TransitionScaleFixed8(-128, nativeSelectionCosine) == 128 &&
              NativeSelectionWindow.TransitionScaleFixed8(256, nativeSelectionCosine) == 0 &&
              NativeSelectionWindow.TransitionScaleFixed8(-1, nativeSelectionCosine) == 192,
              "Native selection: C# must use the x86 cosine lookup, including closing's odd start -1");
        var transitionStart = new CardPoint(525, 300);
        var transitionEnd = new CardPoint(620, 320);
        check(NativeSelectionWindow.TransitionFrame(
                  0, transitionStart, transitionEnd, 451, 191, nativeSelectionCosine) ==
              new NativeSelectionTransitionFrame(transitionEnd, 451, 191) &&
              NativeSelectionWindow.TransitionFrame(
                  256, transitionStart, transitionEnd, 451, 191, nativeSelectionCosine) ==
              new NativeSelectionTransitionFrame(transitionStart, 0, 0) &&
              NativeSelectionWindow.TransitionFrame(
                  128, transitionStart, transitionEnd, 451, 191, nativeSelectionCosine) ==
              new NativeSelectionTransitionFrame(new CardPoint(572, 310), 225, 95) &&
              NativeSelectionWindow.TransitionFrame(
                  -1, transitionStart, transitionEnd, 451, 191, nativeSelectionCosine) ==
              new NativeSelectionTransitionFrame(new CardPoint(596, 315), 338, 143),
              "Native selection: Draw transforms center and both dimensions, truncating divisions like x86");
        check(NativeSelectionWindow.TransitionOriginDescriptor(1) == 0xC &&
              NativeSelectionWindow.TransitionOriginDescriptor(2) == 0xD &&
              NativeSelectionWindow.TransitionOriginDescriptor(3) == 0xE &&
              NativeSelectionWindow.TransitionOriginDescriptor(4) == 0xF &&
              NativeSelectionWindow.TransitionOriginDescriptor(0x29) == -1,
              "Native selection: Draw chooses 0045C140 origin descriptor by window type");
        check(NativeSelectionWindow.ManifestPath(1, 2) == @"j\duel\list\fusionlist2.txt" &&
              NativeSelectionWindow.ManifestPath(2, 1) == @"j\duel\list\selectlist1.txt" &&
              NativeSelectionWindow.ManifestPath(3, 2) == @"j\duel\list\gravelist2.txt" &&
              NativeSelectionWindow.ManifestPath(4, 1) == @"j\duel\list\excludelist1.txt",
              "Native selection: constructor switch maps modes 1/3/4 and default to DAT manifests");

        var classic = new DuelLookAndFeel();
        var fast = new FastLookAndFeel();
        var silent = new SilentLookAndFeel();
        var instant = new InstantLookAndFeel();
        check(classic.Duration(DuelPresentationCue.FlipEffect) == TimeSpan.FromMilliseconds(500) &&
              fast.Duration(DuelPresentationCue.FlipEffect) == TimeSpan.FromMilliseconds(250),
              "LookAndFeel: speed profiles scale presentation durations without touching duel state");
        check(silent.Duration(DuelPresentationCue.JoeySpeech) == TimeSpan.Zero &&
              silent.Duration(DuelPresentationCue.FlipEffect) > TimeSpan.Zero &&
              instant.Duration(DuelPresentationCue.Battle) == TimeSpan.Zero,
              "LookAndFeel: disabling speech and animation are independent policies");
        check(new CustomLook().Duration(DuelPresentationCue.FlipEffect) == TimeSpan.FromMilliseconds(200),
              "LookAndFeel: subclass overrides support per-cue timing changes");
        var tickClock = new PresentationTickClock();
        int splitSteps = Enumerable.Range(0, 27)
            .Sum(_ => tickClock.Advance(TimeSpan.FromMilliseconds(10), classic));
        tickClock.Reset();
        int oneFrameSteps = tickClock.Advance(TimeSpan.FromMilliseconds(270), classic);
        tickClock.Reset();
        int fastSteps = tickClock.Advance(TimeSpan.FromMilliseconds(135), fast);
        check(splitSteps == CardTravel.Updates && oneFrameSteps == splitSteps && fastSteps == splitSteps &&
              classic.UpdatesPerSecond == 30 && fast.UpdatesPerSecond == 60,
              "Presentation ticks: both rates reach the same card travel endpoint independently of frame partitioning");
        check(tickClock.Advance(TimeSpan.FromSeconds(1), instant) == 0,
              "Presentation ticks: Instant skips elapsed animations");
        // Review mode: freeze only presentation; F7 advances one nominal
        // update in Classic and Fast, with no timers or Core mutation.
        var reviewClock = new PresentationTickClock();
        TimeSpan reviewStep = VisualReviewTiming.Elapsed(
            TimeSpan.FromSeconds(1), classic, paused: true, stepRequested: true);
        check(VisualReviewTiming.Elapsed(TimeSpan.FromSeconds(1), classic,
                  paused: true, stepRequested: false) == TimeSpan.Zero &&
              reviewClock.Advance(reviewStep, classic) == 1 &&
              VisualReviewTiming.Elapsed(TimeSpan.FromSeconds(3), classic,
                  paused: false, stepRequested: false) == TimeSpan.FromSeconds(1),
              "Visual review: F6 freezes and F7 steps exactly one nominal Classic update");
        reviewClock.Reset();
        check(reviewClock.Advance(VisualReviewTiming.Elapsed(
                  TimeSpan.Zero, fast, paused: true, stepRequested: true), fast) == 1 &&
              VisualReviewTiming.Elapsed(TimeSpan.FromSeconds(1), instant,
                  paused: true, stepRequested: true) == TimeSpan.Zero,
              "Visual review: Fast uses its own tick and Instant has no visual steps");

        var sequence = new PresentationSequence<string>();
        sequence.Enqueue("travel", CardTravel.Updates);
        sequence.Enqueue("glow", ActivationEffectPlayback.MaxUpdates);
        sequence.Advance(7);
        check(sequence.Current?.Kind == "travel" && sequence.ElapsedUpdates == 7,
              "Presentation sequence: glow waits until the last movement step");
        sequence.Advance(1);
        check(sequence.Current?.Kind == "glow" && sequence.ElapsedUpdates == 0,
              "Presentation sequence: glow starts at zero when movement finishes");
        sequence.Clear();
        sequence.Enqueue("travel", CardTravel.Updates);
        sequence.Enqueue("glow", ActivationEffectPlayback.MaxUpdates);
        sequence.Advance(12);
        check(sequence.Current?.Kind == "glow" && sequence.ElapsedUpdates == 4,
              "Presentation sequence: a skipped render preserves elapsed time across the FIFO boundary");
        sequence.Advance(int.MaxValue);
        check(sequence.Current is null && sequence.ElapsedUpdates == 0,
              "Presentation sequence: completion discards surplus time");
        sequence.Enqueue("first monster", NativeExplosionTimeline.Updates);
        sequence.Enqueue("second monster", NativeExplosionTimeline.Updates);
        sequence.Advance(NativeExplosionTimeline.Updates - 1);
        check(sequence.Pending.Count() == 2 && sequence.Current?.Kind == "first monster" &&
              NativeExplosionTimeline.PassesAt(sequence.ElapsedUpdates * NativeExplosionTimeline.UpdateIncrement) ==
                  NativeExplosionPasses.Fragments,
              "Destruction FIFO: second monster is still pending in the first monster's final sample");
        sequence.Advance(1);
        check(sequence.Pending.Count() == 1 && sequence.Current?.Kind == "second monster" &&
              sequence.ElapsedUpdates == 0,
              "Destruction FIFO: second monster starts after the first finishes");
        check(NativeDuelLayout.GraveBounds(true).X > NativeDuelLayout.MonsterBounds(4, true).X &&
              NativeDuelLayout.GraveBounds(false).X < NativeDuelLayout.MonsterBounds(4, false).X &&
              DuelCardLayout.FrameCenter(NativeDuelLayout.GraveBounds(true)) == new CardPoint(760, 366) &&
              DuelCardLayout.FrameCenter(NativeDuelLayout.GraveBounds(false)) == new CardPoint(290, 234) &&
              DuelCardLayout.Center(DuelCardZone.Field, true) == new CardPoint(286, 353) &&
              DuelCardLayout.Center(DuelCardZone.Field, false) == new CardPoint(766, 236),
              "Duel layout: graveyards align with the monster rows opposite their field spell");
        // Deck shuffle 00453540/00453560: five 8-counter BMP phases
        // and travel from deck to central stage and back.
        var deckOrigin = new CardPoint(760, 366);
        var shuffleCenter = new CardPoint(525, 300);
        var shuffle0 = NativeShuffleFrame.FromUpdate(0);
        var shuffle1 = NativeShuffleFrame.FromUpdate(1);
        var shuffle5 = NativeShuffleFrame.FromUpdate(5);
        var shuffle7 = NativeShuffleFrame.FromUpdate(7);
        var shuffle8 = NativeShuffleFrame.FromUpdate(8);
        var shuffle32 = NativeShuffleFrame.FromUpdate(32);
        var shuffle33 = NativeShuffleFrame.FromUpdate(33);
        var shuffle40 = NativeShuffleFrame.FromUpdate(40);
        check(NativeShuffleFrame.Updates == 41 && shuffle0.Counter == 0 &&
              shuffle40.Counter == 320 && shuffle0.SpriteFrame == 0 &&
              shuffle1.SpriteFrame == 1 && shuffle5.SpriteFrame == 0 &&
              shuffle7.Moving && !shuffle8.Moving && !shuffle32.Moving &&
              shuffle33.Moving && shuffle40.Moving,
              "Deck shuffle: five native frame variants and 41-Update travel gates");
        check(shuffle0.Position(deckOrigin, shuffleCenter) == deckOrigin &&
              shuffle8.Position(deckOrigin, shuffleCenter) == shuffleCenter &&
              shuffle32.Position(deckOrigin, shuffleCenter) == shuffleCenter &&
              shuffle40.Position(deckOrigin, shuffleCenter) == deckOrigin &&
              shuffle7.DeckWeightFixed8 == 32 &&
              shuffle33.DeckWeightFixed8 == 32,
              "Deck shuffle: native 8.8 weight moves sprite into and out of stage center");
        bool invalidShuffleRejected = false;
        try { NativeShuffleFrame.FromUpdate(NativeShuffleFrame.Updates); }
        catch (ArgumentOutOfRangeException) { invalidShuffleRejected = true; }
        check(invalidShuffleRejected,
              "Deck shuffle: do not sample a deactivated counter after t=320");

        // Exodia 00454000/00454010: the first Draw branch places five
        // DAT miniature images on a cosine ring with 16-counter stagger.
        var exodiaCosine = ActivationEffectPlayback.ApproximateCosineTable();
        var exodia0 = NativeExodiaOpeningFrame.FromUpdate(0, exodiaCosine);
        var exodia4 = NativeExodiaOpeningFrame.FromUpdate(4, exodiaCosine);
        var exodia8 = NativeExodiaOpeningFrame.FromUpdate(8, exodiaCosine);
        var exodia16 = NativeExodiaOpeningFrame.FromUpdate(16, exodiaCosine);
        var exodia20 = NativeExodiaOpeningFrame.FromUpdate(20, exodiaCosine);
        var exodiaEnd = NativeExodiaOpeningFrame.FromUpdate(78, exodiaCosine);
        check(NativeExodiaOpeningFrame.OpeningUpdates == 79 &&
              exodia0.Counter == 0 && exodiaEnd.Counter == 312 &&
              exodia0.Cards.Count == 5 &&
              exodia0.Cards[0] == new NativeExodiaCardPose(0, new CardPoint(400, 164), 0) &&
              exodia4.Cards[0] == new NativeExodiaCardPose(0, new CardPoint(400, 100), 255),
              "Exodia first stage: native 79 samples and delayed top card fade/arrival");
        check(exodia4.Cards[1].Opacity == 0 &&
              exodia8.Cards[1].Opacity == 255 &&
              exodia16.Cards[4].Opacity == 0 &&
              exodia20.Cards.All(card => card.Opacity == 255) &&
              exodiaEnd.Cards.All(card => card.Opacity == 255),
              "Exodia first stage: five miniatures stagger every 16 native counter units");
        check(exodiaEnd.Cards.All(card =>
                  Math.Abs(Math.Sqrt(Math.Pow(card.Center.X - 400, 2) +
                      Math.Pow(card.Center.Y - 300, 2)) - 200) <= 2) &&
              exodiaEnd.Cards.Select(card => card.Center).Distinct().Count() == 5,
              "Exodia first stage: all five image centers settle on distinct 200px orbit points");
        bool exodiaOutOfRangeRejected = false;
        try { NativeExodiaOpeningFrame.FromUpdate(79, exodiaCosine); }
        catch (ArgumentOutOfRangeException) { exodiaOutOfRangeRejected = true; }
        check(exodiaOutOfRangeRejected,
              "Exodia first stage: stop before the unknown Draw branch at native counter 316");
        var exodiaQueue = new PresentationSequence<string>();
        exodiaQueue.Enqueue("fifth card draw", CardTravel.Updates);
        exodiaQueue.Enqueue("exodia orbit", NativeExodiaOpeningFrame.OpeningUpdates);
        exodiaQueue.Advance(CardTravel.Updates);
        check(exodiaQueue.Current?.Kind == "exodia orbit" &&
              exodiaQueue.ElapsedUpdates == 0,
              "Exodia: finish fifth-card flight before Exodia's first visual phase");

        // Duel opening 00451950/00451980: 40 native updates, centered
        // logo shrinks from 1.5x, stays opaque, then fades away.
        var noticeCosine = ActivationEffectPlayback.ApproximateCosineTable();
        var start0 = NativeDuelStartFrame.FromUpdate(0, noticeCosine);
        var startMid = NativeDuelStartFrame.FromUpdate(8, noticeCosine);
        var startHold = NativeDuelStartFrame.FromUpdate(32, noticeCosine);
        var startFinal = NativeDuelStartFrame.FromUpdate(39, noticeCosine);
        check(NativeDuelStartFrame.Updates == 40 &&
              start0.Counter == 0 && start0.Opacity == 0 &&
              start0.ScaleFixed8 == 384 &&
              startMid.Counter == 64 && startMid.ScaleFixed8 == 256 &&
              startMid.Opacity == 255 && startHold.Opacity == 255 &&
              startFinal.Counter == 312 && startFinal.Opacity is > 0 and < 255,
              "Duel start: native logo scale and three fade/hold windows");
        bool outOfRangeStartRejected = false;
        try { NativeDuelStartFrame.FromUpdate(40, noticeCosine); }
        catch (ArgumentOutOfRangeException) { outOfRangeStartRejected = true; }
        check(outOfRangeStartRejected,
              "Duel start: stop after last live Draw sample at counter 312");
        var introQueue = new PresentationSequence<string>();
        introQueue.Enqueue("Duel Start", NativeDuelStartFrame.Updates);
        introQueue.Enqueue("opening hand", CardTravel.Updates);
        introQueue.Advance(NativeDuelStartFrame.Updates);
        check(introQueue.Current?.Kind == "opening hand" &&
              introQueue.ElapsedUpdates == 0,
              "Duel start: hand dealing follows the opening notice in the presentation FIFO");

        // Duel result 00451E40/00451E60: counter +4 and native
        // fade-in over 128; the later Draw fade-out is not assumed
        // reachable once Update clears the secondary flag at 0x200.
        var end0 = NativeDuelEndFrame.FromUpdate(0, noticeCosine);
        var endMid = NativeDuelEndFrame.FromUpdate(16, noticeCosine);
        var endOpaque = NativeDuelEndFrame.FromUpdate(32, noticeCosine);
        var endHeld = NativeDuelEndFrame.FromUpdate(400, noticeCosine);
        check(end0.Counter == 0 && end0.Opacity == 0 &&
              endMid.Counter == 64 && endMid.Opacity is > 0 and < 255 &&
              endOpaque.Counter == 128 && endOpaque.Opacity == 255 &&
              endHeld.Counter == 512 && endHeld.Opacity == 255,
              "Duel result: fade-in and stable banner after the native update limit");
        bool negativeResultRejected = false;
        try { NativeDuelEndFrame.FromUpdate(-1, noticeCosine); }
        catch (ArgumentOutOfRangeException) { negativeResultRejected = true; }
        check(negativeResultRejected, "Duel result: reject negative native update");

        // Sacrifice 00486D80/00487060/004870B0: constructor mode 0
        // waits at 128, released mode 1 advances to 384 and eight star
        // particles launch 16 counter units apart after t=128.
        var tributeCosine = ActivationEffectPlayback.ApproximateCosineTable();
        var tributeStart = NativeTributeFrame.FromUpdate(0, released: true, tributeCosine);
        var tributeHeld = NativeTributeFrame.FromUpdate(int.MaxValue, released: false, tributeCosine);
        var tributeRelease = NativeTributeFrame.FromUpdate(16, released: true, tributeCosine);
        var tributeFirstStar = NativeTributeFrame.FromUpdate(17, released: true, tributeCosine);
        var tributeEnd = NativeTributeFrame.FromUpdate(48, released: true, tributeCosine);
        check(NativeTributeFrame.ReleasedUpdates == 49 &&
              tributeStart.Counter == 0 && tributeHeld.Counter == 128 &&
              tributeRelease.Counter == 128 && tributeEnd.Counter == 384 &&
              tributeRelease.CardOpacity == 128 &&
              tributeStart.Particles.Count == 0 &&
              tributeHeld.Particles.Count == 0 && tributeRelease.Particles.Count == 0,
              "Sacrifice: native 49-update released sequence and unlimited 128-counter hold");
        check(tributeFirstStar.Particles.Count == 1 &&
              tributeFirstStar.Particles[0] == new NativeSetParticle(0, 1, 0) &&
              tributeEnd.Particles.Count == 8 &&
              tributeEnd.Particles[0].RisePixels == 64 &&
              tributeEnd.Particles[7].RisePixels == 20,
              "Sacrifice: eight delayed native particles alternate sprites and rise quadratically");
        var sacrificeRadii = Enumerable.Repeat(32, NativeSetFrame.ParticleCount).ToArray();
        check(NativeTributeFrame.ParticleOrigins(new CardPoint(400, 300),
                  sacrificeRadii, tributeCosine).SequenceEqual(
                      NativeSetFrame.ParticleOrigins(new CardPoint(400, 300), sacrificeRadii, tributeCosine)),
              "Sacrifice constructor: same eight radius/angle origins as native Set");
        bool tributeAfterEndRejected = false;
        try { NativeTributeFrame.FromUpdate(49, released: true, tributeCosine); }
        catch (ArgumentOutOfRangeException) { tributeAfterEndRejected = true; }
        check(tributeAfterEndRejected,
              "Sacrifice: reject native released counter after deactivation at 392");

        // Special Summon 004500E0/00450100: 41 native Updates, a/d
        // variants with four frames cycling every two Updates, plus card
        // overlay for 64 < t < 320.
        var specialCosine = ActivationEffectPlayback.ApproximateCosineTable();
        var special0 = NativeSpecialSummonFrame.FromUpdate(0, specialCosine);
        var special1 = NativeSpecialSummonFrame.FromUpdate(1, specialCosine);
        var special2 = NativeSpecialSummonFrame.FromUpdate(2, specialCosine);
        var special9 = NativeSpecialSummonFrame.FromUpdate(9, specialCosine);
        var special23 = NativeSpecialSummonFrame.FromUpdate(23, specialCosine);
        var special24 = NativeSpecialSummonFrame.FromUpdate(24, specialCosine);
        var special39 = NativeSpecialSummonFrame.FromUpdate(39, specialCosine);
        var special40 = NativeSpecialSummonFrame.FromUpdate(40, specialCosine);
        check(NativeSpecialSummonFrame.Updates == 41 &&
              special0.Counter == 0 && special40.Counter == 320 &&
              special0.SpiralFrame == 0 && special1.SpiralFrame == 0 &&
              special2.SpiralFrame == 1 && special23.SpiralFrame == 3 &&
              !special24.SpiralFrame.HasValue && !special40.SpiralFrame.HasValue,
              "Special summon: four native sprite frames cycle during first 24 updates");
        check(!special0.DrawCardOverlay && !NativeSpecialSummonFrame.FromUpdate(8, specialCosine).DrawCardOverlay &&
              special9.DrawCardOverlay && special24.DrawCardOverlay &&
              special39.DrawCardOverlay && !special40.DrawCardOverlay &&
              special9.OverlayScaleFixed8 > 0 && special9.SpiralOpacity is >= 0 and <= 255,
              "Special summon: transformed card overlay is active strictly from t>64 to t<320");
        bool specialAfterEndRejected = false;
        try { NativeSpecialSummonFrame.FromUpdate(NativeSpecialSummonFrame.Updates, specialCosine); }
        catch (ArgumentOutOfRangeException) { specialAfterEndRejected = true; }
        check(specialAfterEndRejected,
              "Special summon: no native sample after Update deactivates at t=328");

        // Normal Summon: 0044F9F0 increments by 8 through t=256,
        // and 0044FA10 has different rendering branches either side of 64.
        var summonCosine = ActivationEffectPlayback.ApproximateCosineTable();
        var summonStart = NativeNormalSummonFrame.FromUpdate(0, summonCosine);
        var summonLastExpand = NativeNormalSummonFrame.FromUpdate(8, summonCosine);
        var summonFirstWrap = NativeNormalSummonFrame.FromUpdate(9, summonCosine);
        var summonEnd = NativeNormalSummonFrame.FromUpdate(32, summonCosine);
        check(NativeNormalSummonFrame.Updates == 33 &&
              summonStart.Counter == 0 && summonEnd.Counter == 256 &&
              summonStart.Stage == NativeNormalSummonStage.ExpandMiniature &&
              summonStart.ScaleFixed16 == 0x4000 &&
              summonLastExpand.Counter == 64 && summonLastExpand.ScaleFixed16 == 0x8000 &&
              summonFirstWrap.Counter == 72 &&
              summonFirstWrap.Stage == NativeNormalSummonStage.WrappedMiniature,
              "Normal summon: native 33 updates, initial scale and branch at t=64/72");
        check(summonStart.Opacity > summonLastExpand.Opacity &&
              summonFirstWrap.Opacity > 0 &&
              NativeNormalSummonFrame.FromUpdate(20, summonCosine).Opacity >
                  summonFirstWrap.Opacity &&
              summonFirstWrap.WrappedSourceOffset(72) == 45 &&
              summonEnd.WrappedSourceOffset(72) == 16,
              "Normal summon: cosine intensities and 5/8 scroll wrapping use native counters");
        bool invalidSummonSampleRejected = false;
        try { NativeNormalSummonFrame.FromUpdate(33, summonCosine); }
        catch (ArgumentOutOfRangeException) { invalidSummonSampleRejected = true; }
        check(invalidSummonSampleRejected,
              "Normal summon: never sample deactivated native counter t=264");
        var summonQueue = new PresentationSequence<string>();
        summonQueue.Enqueue("hand flight", CardTravel.Updates);
        summonQueue.Enqueue("normal summon", NativeNormalSummonFrame.Updates);
        summonQueue.Advance(CardTravel.Updates);
        check(summonQueue.Current?.Kind == "normal summon" &&
              summonQueue.ElapsedUpdates == 0,
              "Normal summon: card landing precedes native summon visual timeline");

        var setCosine = ActivationEffectPlayback.ApproximateCosineTable();
        var setStart = NativeSetFrame.FromUpdate(0, setCosine);
        var setMid = NativeSetFrame.FromUpdate(11, setCosine);
        var setEnd = NativeSetFrame.FromUpdate(21, setCosine);
        check(NativeSetFrame.Updates == 22 && setStart.Counter == 0 &&
              setEnd.Counter == 252 && setStart.ScaleFixed16 == 65536 &&
              setMid.ScaleFixed16 == (132 << 9) &&
              setStart.MainOpacity == 0 && setMid.MainOpacity > 0 &&
              setMid.Particles.Count == NativeSetFrame.ParticleCount &&
              setStart.Particles.Count == 1 &&
              setStart.Particles[0] == new NativeSetParticle(0, 0, 0),
              "Native Set: 0044F370 counter, cosine fade, scale and staggered launches");
        check(setMid.Particles[1].Variant == 0 &&
              setMid.Particles[0].RisePixels > 0 && setEnd.MainOpacity < setMid.MainOpacity,
              "Native Set: alternating particles rise quadratically; X origin awaits native constructor");
        // 0044EFD0: eight MT-derived radii on the 8-way cosine ring,
        // at offsets obj+104h..140h. Test independent of random MT state.
        int[] fixedRadii = Enumerable.Repeat(32, NativeSetFrame.ParticleCount).ToArray();
        var origin = new CardPoint(400, 300);
        var setOrigins = NativeSetFrame.ParticleOrigins(origin, fixedRadii, setCosine);
        check(setOrigins.Length == 8 &&
              setOrigins[0] == new CardPoint(432, 300) &&
              setOrigins[2] == new CardPoint(400, 332) &&
              setOrigins[4] == new CardPoint(368, 300) &&
              setOrigins[6] == new CardPoint(400, 268),
              "Set constructor: eight origins form a native 8-way cosine ring around the card");
        fixedRadii[0] = NativeSetFrame.MinimumRadius;
        fixedRadii[4] = NativeSetFrame.MaximumRadius;
        setOrigins = NativeSetFrame.ParticleOrigins(origin, fixedRadii, setCosine);
        check(setOrigins[0] == new CardPoint(416, 300) &&
              setOrigins[4] == new CardPoint(353, 300),
              "Set constructor: MT low five bits select native radius 16..47");
        bool rejected = false;
        try
        {
            NativeSetFrame.ParticleOrigins(origin, new int[7], setCosine);
        }
        catch (ArgumentException) { rejected = true; }
        check(rejected, "Set constructor: fail closed on an incomplete list of native radii");

        var setQueue = new PresentationSequence<string>();
        setQueue.Enqueue("travel", CardTravel.Updates);
        setQueue.Enqueue("set", NativeSetFrame.Updates);
        setQueue.Advance(CardTravel.Updates);
        check(setQueue.Current?.Kind == "set" && setQueue.ElapsedUpdates == 0,
              "Set placement: effect starts after hand-to-field flight");

        check(NativeLifeHud.FloatingNumberAnchor(nearSide: true) == new CardPoint(525, 406) &&
              NativeLifeHud.FloatingNumberAnchor(nearSide: false) == new CardPoint(525, 194),
              "Floating LP glyphs occupy the gap between monster/spell rows, not sidebar");
        check(NativeAttackFrame.ImpactStartUpdate == 16 &&
              NativeAttackFrame.Updates == 32,
              "Hidden defender stays face-down through sword approach and flips at impact");
        var deferredLife = new NativeLifePointPresentation(8000);
        // Core has already applied a 500 LP change while activation is still
        // active; do not present it before that visual barrier completes.
        bool cardMotionBusy = true;
        if (!cardMotionBusy) deferredLife.SetLogicalLife(7500);
        check(deferredLife.Displayed == 8000 && deferredLife.PendingDelta == 0,
              "Life order: logical damage does not begin before card activation ends");
        cardMotionBusy = false;
        if (!cardMotionBusy) deferredLife.SetLogicalLife(7500);
        check(deferredLife.PendingDelta == -500 &&
              deferredLife.RampCounter == 0,
              "Life order: damage starts its own native ramp after activation/attack");
        check(NativeLifeHud.DisplayGlyphScale == 3 &&
              NativeLifeHud.DisplayGlyphHeight == 36 &&
              NativeLifeHud.DisplayRightX == 195 &&
              NativeLifeHud.DisplayGlyphWidth(120, 12, 10) == 36 &&
              NativeLifeHud.DisplayTopY == -1 &&
              NativeLifeHud.DisplayBottomY == 532,
              "LP digits: client uses 3x bitmap glyph scale, right-aligned to atlas evidence");
        // 0047C660 uses displayed (not logical) LP for the native bar,
        // saturating at 8000 even though the numeric display reaches 65535.
        check(NativeLifeHud.FilledBarPixels(0, 137) == 0 &&
              NativeLifeHud.FilledBarPixels(4000, 137) == 68 &&
              NativeLifeHud.FilledBarPixels(7999, 137) == 136 &&
              NativeLifeHud.FilledBarPixels(8000, 137) == 137 &&
              NativeLifeHud.FilledBarPixels(65535, 137) == 137 &&
              NativeLifeHud.FilledBarPixels(-100, 137) == 0,
              "Native LP HUD: bar uses truncated displayed/8000 ratio and saturates");
        check(NativeLifeHud.DecimalGlyphs(0).SequenceEqual([0]) &&
              NativeLifeHud.DecimalGlyphs(8000).SequenceEqual([8, 0, 0, 0]) &&
              NativeLifeHud.DecimalGlyphs(int.MaxValue).SequenceEqual([6, 5, 5, 3, 5]),
              "Native LP HUD: decimal digits use visual clamp to 65535");

        // Original user's DAT report: 80x12/10, 70x10/10 and 192x40/12.
        string observedFontManifest = "duel_fontl.bmp 10\n" +
            "duel_fontm_y.bmp 10\n" +
            "damage00.bmp 12\n" + "damage01.bmp 12\n";
        check(NativeLifeHud.TryFontColumns(observedFontManifest, "duel_fontl.bmp") == 10 &&
              NativeLifeHud.TryFontColumns(observedFontManifest, "duel_fontm_y.bmp") == 10 &&
              NativeLifeHud.TryFontColumns(observedFontManifest, "damage00.bmp") == 12 &&
              NativeLifeHud.TryFontColumns(observedFontManifest, "damage01.bmp") == 12 &&
              NativeLifeHud.GlyphWidth(80, 10) == 8 &&
              NativeLifeHud.GlyphWidth(70, 10) == 7 &&
              NativeLifeHud.GlyphWidth(192, 12) == 16 &&
              NativeLifeHud.DisplayGlyphWidth(80, 12, 10) == 24 &&
              NativeLifeHud.DisplayHeightForAtlas(12) == 36 &&
              NativeLifeHud.DisplayGlyphWidth(70, 10, 10) == 21 &&
              NativeLifeHud.DisplayHeightForAtlas(10) == 30 &&
              NativeLifeHud.DisplayTopY + NativeLifeHud.DisplayGlyphHeight == 35 &&
              NativeLifeHud.DisplayBottomY + NativeLifeHud.DisplayGlyphHeight == 568,
              "LP font manifests agree with integer 3x presentation raster/anchors");
        string fontManifest = "duel_fontl.bmp 10\n" +
            "j/duel/font/duel_fontm_y.bmp 12 // sprite count\n" +
            "duel_fonts.bmp 8\n";
        check(NativeLifeHud.TryFontColumns(fontManifest, "DUEL_FONTM_Y.BMP") == 12 &&
              NativeLifeHud.TryFontColumns(fontManifest, "duel_fontl.bmp") == 10 &&
              NativeLifeHud.TryFontColumns(fontManifest, "missing.bmp") is null &&
              NativeLifeHud.TryFontColumns("duel_fontm_y.bmp ?\n", "duel_fontm_y.bmp") is null &&
              NativeLifeHud.GlyphWidth(240, 12) == 20,
              "Native LP HUD: glyph columns derive from DAT manifest rather than guessed image width");
        bool invalidGlyphAtlasRejected = false;
        try { NativeLifeHud.GlyphWidth(239, 12); }
        catch (ArgumentException) { invalidGlyphAtlasRejected = true; }
        check(invalidGlyphAtlasRejected,
              "Native LP HUD: reject an invalid atlas width instead of slicing wrong glyphs");

        // 004806F0: damage/heal glyphs appear successively from the
        // least significant digit, moving through two cosine Y windows.
        var floatCosine = ActivationEffectPlayback.ApproximateCosineTable();
        var floatStart = NativeFloatingLifeDigits.Sample(-250, 0, 10, 24, 12, floatCosine);
        var floatRise = NativeFloatingLifeDigits.Sample(-250, 64, 10, 24, 12, floatCosine);
        var floatBounce = NativeFloatingLifeDigits.Sample(-250, 160, 10, 24, 12, floatCosine);
        var floatEnd = NativeFloatingLifeDigits.Sample(-250, 256, 10, 24, 12, floatCosine);
        check(floatStart.Count == 0 && floatRise.Count == 4 &&
              floatRise[0] == new NativeFloatingLifeGlyph(0, -10, -24, 64) &&
              floatBounce[0].YOffsetPixels == -6 &&
              floatEnd.Select(x => x.GlyphIndex).SequenceEqual([0, 5, 2, 11]) &&
              floatEnd.All(x => x.YOffsetPixels == 0),
              "Floating LP: digits appear low to high, move in two cosine windows then settle");
        check(NativeFloatingLifeDigits.Sample(205, 256, 10, 24, 12, floatCosine)
                  .Select(x => x.GlyphIndex).SequenceEqual([5, 0, 2, 10]) &&
              NativeFloatingLifeDigits.Sample(205, 256, 10, 24, 10, floatCosine)
                  .Select(x => x.GlyphIndex).SequenceEqual([5, 0, 2]) &&
              NativeFloatingLifeDigits.Sample(int.MinValue, 256, 10, 24, 12, floatCosine)
                  .Last().GlyphIndex == 11,
              "Floating LP: plus/minus glyphs depend on DAT atlas and handle signed minimum");
        bool invalidFloatColumnsRejected = false;
        try { NativeFloatingLifeDigits.Sample(-250, 256, 10, 24, 9, floatCosine); }
        catch (ArgumentOutOfRangeException) { invalidFloatColumnsRejected = true; }
        check(invalidFloatColumnsRejected,
              "Floating LP: no guessed glyph count for unrecognized DAT atlas");

        // 0047C4F0/0047C560: Core LP updates immediately, but the native
        // visual counter has a 32-update ramp before consuming signed deltas.
        var lp = new NativeLifePointPresentation(8000);
        lp.SetLogicalLife(7750);
        for (int i = 0; i < NativeLifePointPresentation.RampUpdates; i++)
        {
            lp.SetLogicalLife(7750); // no restart on unchanged logical target
            lp.AdvanceNativeUpdate();
        }
        check(lp.Displayed == 8000 && lp.PendingDelta == -250 &&
              lp.RampCounter == NativeLifePointPresentation.RampEnd,
              "Native LP: a delta is held throughout 32 Update ramp samples");
        lp.AdvanceNativeUpdate();
        check(lp.Displayed == 7900 && lp.PendingDelta == -150,
              "Native LP: first damage display update subtracts 100");
        lp.AdvanceNativeUpdate();
        check(lp.Displayed == 7800 && lp.PendingDelta == -50,
              "Native LP: second damage display update subtracts another 100");
        for (int i = 0; i < 5; i++) lp.AdvanceNativeUpdate();
        check(lp.Displayed == 7750 && lp.PendingDelta == 0 && lp.FlashCounter > 0,
              "Native LP: the remaining 50 LP is displayed in five -10 steps");

        var healing = new NativeLifePointPresentation(1000);
        healing.SetLogicalLife(1205);
        for (int i = 0; i < NativeLifePointPresentation.RampUpdates + 7; i++)
            healing.AdvanceNativeUpdate();
        check(healing.Displayed == 1205 && healing.PendingDelta == 0,
              "Native LP: healing uses +100/+10/+1 visual steps");

        var retargeted = new NativeLifePointPresentation(8000);
        retargeted.SetLogicalLife(7000);
        for (int i = 0; i < NativeLifePointPresentation.RampUpdates + 1; i++)
            retargeted.AdvanceNativeUpdate();
        retargeted.SetLogicalLife(7900);
        check(retargeted.Displayed == 7900 && retargeted.PendingDelta == 0 &&
              retargeted.RampCounter == 0,
              "Native LP: a second logical change retargets from currently displayed LP");
        retargeted.SetLogicalLife(200000, animate: false);
        check(retargeted.Displayed == 65535 && retargeted.PendingDelta == 0 &&
              retargeted.RampCounter == 256,
              "Native LP: Instant presentation jumps to target and clips only the display to 16-bit");
        retargeted.SetLogicalLife(0, animate: false);
        check(retargeted.Displayed == 0 && !retargeted.Animating,
              "Native LP: zero reaches the screen without altering Core win conditions");

        var clock = new DuelPresentationClock(classic);
        clock.Show(DuelPresentationCue.FlipReveal);
        clock.Advance(TimeSpan.FromMilliseconds(100));
        check(clock.Busy && clock.Remaining == TimeSpan.FromMilliseconds(350) &&
              clock.ActiveCue == DuelPresentationCue.FlipReveal,
              "LookAndFeel: cue clock advances by frame delta without sleeps");
        clock.Advance(TimeSpan.FromSeconds(10));
        check(!clock.Busy && clock.ActiveCue is null,
              "LookAndFeel: cue clock completes without requiring a timer thread");
        clock.SetStyle(instant);
        clock.Show(DuelPresentationCue.FlipReveal);
        check(!clock.Busy && clock.Remaining == TimeSpan.Zero,
              "LookAndFeel: instant policy is immediate");
    }
}
