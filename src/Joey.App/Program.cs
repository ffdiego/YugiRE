using Joey.LookAndFeel;
using Joey.Core;
using Raylib_cs;
using System.Numerics;

return Game.Run(args);

static partial class Game
{
    static readonly Color Background = new(16, 23, 35, 255), Panel = new(29, 42, 57, 255), Gold = new(230, 185, 93, 255);
    static List<Card> cards = [], orderedCards = [], joeyCards = [], playableCards = [];
    static bool joeyOnly;
    static readonly List<int> tributeZones = [];
    static string screen = "menu", search = "", error = "", message = "Select a hand card, then a monster zone.";
    static int page, selected, hand = -1, attacker = -1;
    static DemoDuel duel = null!;
    static DatGraphics graphics = null!;
    static DuelTheme duelTheme = null!;
    static bool includeUnused;
    static readonly DuelPresentationClock presentation = new(new FastLookAndFeel());
    static string previousDuelMessage = "";
    // Keep explicit legacy profile precedence; Fast is now the App default.
    static DuelLookAndFeel SelectPresentationStyle(string[] args) =>
        args.Contains("--instant") ? new InstantLookAndFeel()
        : args.Contains("--fast") ? new FastLookAndFeel()
        : args.Contains("--mute-joey") ? new SilentLookAndFeel()
        : args.Contains("--classic") ? new DuelLookAndFeel()
        : new FastLookAndFeel();

    public static int Run(string[] args)
    {
        if (args.Contains("--legacy-actions"))
        {
            Console.Error.WriteLine("Legacy action mode has been removed. The typed action queue is required.");
            return 1;
        }
        if (args is ["--check-console-startup"]) return CheckConsoleStartup();
        if (args.Contains("--inspect-voice-dat") || Option(args,"--review-voice=") is not null)
            return VoiceAudioReview.Run(Option(args,"--voice-dat="),
                args.Contains("--inspect-voice-dat"), Option(args,"--review-voice="),
                args.Contains("--mute-joey"));
        using var resources = new DatGraphics();
        graphics = resources;
        includeUnused = args.Contains("--include-unused");
        useLegacySelection = args.Contains("--legacy-selection");
        presentation.SetStyle(SelectPresentationStyle(args));
        if (Option(args, "--native-cosine=") is string cosinePath)
        {
            try
            {
                effectCosine = NativeCosineCapture.Read(cosinePath);
                effectCosineSource = "native-runtime-capture";
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine("[visual] " + ex.Message);
                return 1;
            }
        }
        Console.WriteLine($"[visual] cosine source: {effectCosineSource}");
        string? dataArchivePath = null;
        try
        {
            string? path = args.FirstOrDefault(a => !a.StartsWith("--"));
            if (path == null)
            {
                for (var d = new DirectoryInfo(Environment.CurrentDirectory); d != null; d = d.Parent)
                {
                    string candidate = Path.Combine(d.FullName, "passion", "data.dat");
                    if (File.Exists(candidate)) { path = candidate; break; }
                }
            }
            if (path == null) throw new FileNotFoundException("data.dat not found. Pass its path: dotnet run -- /path/to/data.dat");
            if (args.Contains("--inspect-visual-dat"))
            {
                using var inspected = new Archive(path);
                return VisualManifestProbe.Export(inspected);
            }
            var archive = graphics.Open(path);
            ConfigureDisabledChainReview(args, archive);
            ConfigureChainReview(args, archive);
            ConfigureTurnNotice(archive);
            ValidatePlacementBank(archive);
            cards = Catalog.Load(archive);
            graphics.LoadArtwork(cards);
            ConfigureCardStatusReview(args, archive);
            ConfigureFusionReview(args, archive);
            ConfigureGeneralFusionReview(args, archive);
            ConfigureLiveGeneralFusion(archive);
            ConfigureLayerFontReview(args, archive);
            ConfigureDiceReview(args, archive);
            ConfigureCoinReview(args, archive);
            ConfigureAlternativeExclusionReview(args, archive);
            if (new[] { chainReview is not null, statusReviewCard is not null, disabledChainReviewTarget is not null, fusionReviewCard is not null, generalFusionReview is not null, layerFontReviewMode is not null, diceReview is not null, coinReview is not null, altExReviewMode is not null }.Count(active => active) > 1)
                throw new InvalidDataException("Choose one isolated review mode.");
            orderedCards = CardListResources.Sort(cards, archive.Read(@"bin#\card_sorteng.bin"));
            byte[] packs = archive.Read(@"bin#\card_pack.bin");
            playableCards = CardListResources.ForPacks(orderedCards, packs);
            DuelArtworkPolicy.ValidateMappings(playableCards, graphics.Artwork.For, graphics.Artwork.MiniFor);
            if (!includeUnused) orderedCards = playableCards;
            joeyCards = CardListResources.ForPacks(playableCards, packs, 4);
            Console.WriteLine($"Read {archive.Entries.Count} archive entries and {cards.Count} card records from {path}");
            Console.WriteLine($"Card List: {orderedCards.Count} entries (include unused: {includeUnused})");
            InitializeDeck(args, path);
            dataArchivePath = path;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException or OverflowException)
        { error = ex.Message; Console.Error.WriteLine(error); }
        if (args.Contains("--inspect-visual-dat")) return 1;
        if ((Option(args, "--review-chain=") is not null || Option(args, "--review-disabled-chain=") is not null || Option(args, "--review-card-status=") is not null || Option(args, "--review-attached-fusion=") is not null || args.Contains("--review-general-fusion") || Option(args, "--review-general-fusion-cards=") is not null || Option(args, "--review-layerfont=") is not null || Option(args, "--review-dice=") is not null || Option(args, "--review-coin=") is not null || Option(args, "--review-alternative=") is not null || Option(args, "--review-exclusion=") is not null) && error.Length > 0) return 1;
        if (args.Contains("--alpha-headless"))
        {
            if (error.Length > 0) return 1;
            try { return RunAlphaHeadless(); }
            catch (InvalidDataException ex) { Console.Error.WriteLine(ex.Message); return 1; }
        }
        if (args.Contains("--check-deck")) return CheckDeck();
        if (args.Contains("--check-data")) return error.Length == 0 ? 0 : 1;
        if (args.Contains("--check-graphics"))
        {
            if (error.Length > 0) return 1;
            try { graphics.Validate(); return 0; }
            catch (Exception ex) when (ex is InvalidDataException or IOException or KeyNotFoundException)
            { Console.Error.WriteLine(ex.Message); return 1; }
        }
        if (OperatingSystem.IsLinux() && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        { Console.Error.WriteLine("No graphical session available. Run from a desktop terminal; use --check-data or --check-graphics for validation."); return 2; }
        duel = new(cards);
        string? labScriptPath = Option(args, "--duel-script=");
        if (args.Contains("--duel-console") || labScriptPath is not null)
        {
            if (error.Length > 0) { Console.Error.WriteLine(error); return 1; }
            try { StartConsoleLab(labScriptPath is null ? null : File.ReadAllLines(labScriptPath)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            { Console.Error.WriteLine(ex.Message); return 1; }
        }
        else if (error.Length == 0 && chainReview is null && disabledChainReviewTarget is null && statusReviewCard is null && fusionReviewCard is null && generalFusionReview is null && layerFontReviewMode is null && diceReview is null && coinReview is null && altExReviewMode is null && args.Contains("--alpha")) StartAlpha();
        // Suppress routine INFO texture/cache messages, not warnings/errors.
        // Do not initialize raylib solely to configure logging in headless modes.
        Raylib.SetTraceLogLevel(args.Contains("--raylib-verbose") ? TraceLogLevel.Info : TraceLogLevel.Warning);
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
        Raylib.InitWindow(800, 600, "Joey RE - DAT graphics / demonstration duel");
        duelTheme = new();
        try
        {
            if (Option(args, "--duel-theme=") is string directory) duelTheme.Load(directory);
            string? board = Option(args, "--duel-board-dat=");
            string? back = Option(args, "--duel-back-dat=");
            if (error.Length > 0 && (board is not null || back is not null))
                throw new InvalidDataException(error);
            if (error.Length == 0)
            {
                duelTheme.LoadDatArt(graphics,
                    board ?? (duelTheme.Board is null ? DatGraphics.DuelBoard : null),
                    back ?? (duelTheme.CardBack is null ? DatGraphics.DuelBack : null));
                if (duelTheme.UsesNativeCardBack)
                    _ = graphics.Texture(DatGraphics.MiniCardBack);
                // Native detail uses an English GDI face, not the default
                // raylib bitmap font; keep this confined to the sidebar.
                duelTheme.LoadNativeDetailFonts();
                if (chainReview is not null)
                {
                    foreach (string resource in chainReviewResources) _ = graphics.Texture(resource);
                    screen = "active-chain-review";
                    Console.WriteLine("[V23 review] F5 release, F7 step, F8 reset, F12 capture; diagnostic compositor");
                }
                if (disabledChainReviewTarget is not null)
                {
                    foreach (string resource in disabledChainReviewResources) _ = graphics.Texture(resource);
                    screen = "chain-review";
                    Console.WriteLine("[V12 review] F7 step, F8 reset, F12 capture, Escape exit; blend not verified");
                }
                if (statusReviewCard is not null)
                {
                    ValidateCardStatusReview();
                    screen = "status-review";
                    Console.WriteLine("[V08 review] F7 step, F8 reset, F12 capture, Escape exit; synthetic state, blend not verified");
                }
                if (altExReviewMode is not null)
                {
                    InitializeAlternativeExclusionReview();
                    screen = "alt-ex-review";
                    Console.WriteLine("[V25/V26 review] F7 native step, F9 Draw only for exclusion, F8 reset, F12 capture");
                }
                if (coinReview is not null)
                {
                    InitializeCoinReview();
                    screen = "coin-review";
                    Console.WriteLine("[V20 review] F7 step, F8 reset, F12 capture; sparkles included; reflection plan only; diagnostic compositor");
                }
                if (diceReview is not null)
                {
                    InitializeDiceReview();
                    screen = "dice-review";
                    Console.WriteLine("[V21 review] F7 update+draw, F9 Draw only, F8 reset, F12 capture; diagnostic compositor");
                }
                if (layerFontReviewMode is not null)
                {
                    foreach (string resource in layerFontReviewResources) _ = graphics.Texture(resource);
                    screen = "layerfont-review";
                    Console.WriteLine("[V27/V28 review] F5 display gate, F7 one Draw, F8 reset, F12 capture; provisional compositor");
                }
                if (generalFusionReview is not null)
                {
                    InitializeGeneralFusionReview();
                    screen = "general-fusion-review";
                    Console.WriteLine("[V11 review] General fusion review: F7 update+draw, F9 draw only, F8 reset, F12 capture; cards opt-in, scratch/result overlay omitted");
                }
                if (fusionReviewCard is not null)
                {
                    _ = graphics.Texture(fusionReviewResources[0]);
                    screen = "fusion-review";
                    Console.WriteLine("[V11 review] F5 release, F6 associated card, F7 update+draw, F8 reset, F9 draw only, F12 capture, Escape exit");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or KeyNotFoundException)
        {
            Console.Error.WriteLine(ex.Message);
            duelTheme.Dispose();
            graphics.Dispose();
            Raylib.CloseWindow();
            return 1;
        }
        Raylib.SetExitKey(KeyboardKey.Null); Raylib.SetTargetFPS(60);
        // For the entire lifetime of an initialized DAT application, the
        // window uses ONE original YGA cursor. Hide the operating-system
        // pointer once at the window level, not inside the duel renderer.
        bool useNativeCursor = error.Length == 0;
        if (useNativeCursor)
        {
            _ = graphics.Texture(NativeDuelCursor.DefaultPointer);
            Raylib.HideCursor();
        }
        var canvas = Raylib.LoadRenderTexture(800, 600);
        // Keep the 800x600 original DAT pixels discrete when scaling the window.
        Raylib.SetTextureFilter(canvas.Texture, TextureFilter.Point);
        using var audio = error.Length == 0 ? DuelAudioPlayer.Open(Option(args,"--voice-dat="),dataArchivePath,args.Contains("--no-audio")) : null;
        duelAudio = audio;
        bool quit = false;
        while (!Raylib.WindowShouldClose() && !quit)
        {
            const int width = 800, height = 600;
            duelAudio?.UpdateMusic(args.Contains("--no-music") ? null : DuelSoundAssets.Music(screen));
            graphics.BeginFrame();
            float scale = Math.Min(Raylib.GetScreenWidth() / (float)width, Raylib.GetScreenHeight() / (float)height);
            Vector2 origin = new((Raylib.GetScreenWidth() - width * scale) / 2, (Raylib.GetScreenHeight() - height * scale) / 2);
            mouse = (Raylib.GetMousePosition() - origin) / scale;
            debugOverlay.ResetFrame();
            Raylib.BeginTextureMode(canvas); Raylib.ClearBackground(Background);
            if (screen == "alt-ex-review")
            {
                if (Raylib.IsKeyPressed(KeyboardKey.Escape)) quit = true;
                DrawAlternativeExclusionReview();
            }
            else if (screen == "coin-review")
            {
                if (Raylib.IsKeyPressed(KeyboardKey.Escape)) quit = true;
                DrawCoinReview();
            }
            else if (screen == "dice-review")
            {
                if (Raylib.IsKeyPressed(KeyboardKey.Escape)) quit = true;
                DrawDiceReview();
            }
            else if (screen == "layerfont-review")
            {
                if (Raylib.IsKeyPressed(KeyboardKey.Escape)) quit = true;
                DrawLayerFontReview();
            }
            else if (screen == "general-fusion-review")
            {
                if (Raylib.IsKeyPressed(KeyboardKey.Escape)) quit = true;
                DrawGeneralFusionReview();
            }
            else if (screen == "fusion-review")
            {
                if (Raylib.IsKeyPressed(KeyboardKey.Escape)) quit = true;
                DrawFusionReview();
            }
            else if (screen == "status-review")
            {
                if (Raylib.IsKeyPressed(KeyboardKey.Escape)) quit = true;
                DrawCardStatusReview();
            }
            else if (screen == "active-chain-review")
            {
                if (Raylib.IsKeyPressed(KeyboardKey.Escape)) quit = true;
                DrawChainReview();
            }
            else if (screen == "chain-review")
            {
                if (Raylib.IsKeyPressed(KeyboardKey.Escape)) quit = true;
                DrawDisabledChainReview();
            }
            else if (screen == "menu")
            {
                if (graphics.Menu is { } menu && error.Length == 0)
                {
                    Raylib.DrawTexture(graphics.Texture(MenuResources.Background), 0, 0, Color.White);
                    int hover = menu.Hit(mouse.X, mouse.Y);
                    for (int row = 0; row < 5; row++)
                    {
                        bool enabled = row is 0 or 1 or 2 or 4;
                        var texture = graphics.Texture(enabled && hover == row ? MenuResources.Highlight : MenuResources.Normal);
                        Raylib.DrawTextureRec(texture, new Rectangle(0, row * 50, 400, 50),
                            new Vector2(menu.Buttons.X, menu.Buttons.Y + row * 50), enabled ? Color.White : new Color(150, 150, 150, 255));
                    }
                    if (Button("Console lab (\u0027 / F10)", 535, 488, 240, 38) || ConsoleTogglePressed())
                        StartConsoleLab(null);
                    if (screen == "menu" && Button("Alpha - vs CPU", 535, 532, 240, 42)) StartAlpha();
                    bool debugMenuClicked = screen == "menu" && Button(duelDebug ? "Ocultar debug" : "Ativar debug", 535, 576, 175, 22);
                    if (debugMenuClicked) debugOverlay.Toggle();
                    if (alphaError.Length > 0) Wrap(alphaError, 535, 475, 240, 13);
                    if (!debugMenuClicked && screen == "menu" && Raylib.IsMouseButtonPressed(MouseButton.Left))
                    {
                        if (hover == 0) { useDemoFixture = false; useImportedDeck = false; StartDemo(); }
                        if (hover == 1) screen = "deck";
                        if (hover == 2) screen = "cards";
                        if (hover == 4) quit = true;
                    }
                }
                else
                {
                    Wrap(error, 30, 80, 740, 20);
                    if (Button("Quit", 30, 500, 150, 50)) quit = true;
                }
            }
            else
            {
                if (screen == "duel") ReadDuelConsoleToggle();
                if (Raylib.IsKeyPressed(KeyboardKey.Escape) && !ConsoleCapturesInput) screen = "menu";
                if (screen == "cards") DrawCards();
                else if (screen == "duel")
                {
                    debugOverlay.BeginDuelFrame(Raylib.IsKeyPressed(KeyboardKey.F3),
                        !ConsoleCapturesInput && Raylib.IsMouseButtonPressed(MouseButton.Left), !ConsoleCapturesInput && Raylib.IsMouseButtonPressed(MouseButton.Right),
                        mouse.X, mouse.Y, alphaCpu is null);
                    if (Raylib.IsKeyPressed(KeyboardKey.F6))
                    {
                        visualReviewPaused = !visualReviewPaused;
                        Console.WriteLine(visualReviewPaused ? "[visual-review] paused" : "[visual-review] resumed");
                    }
                    visualReviewStep = visualReviewPaused && Raylib.IsKeyPressed(KeyboardKey.F7);
                    DrawDuel();
                }
                else if (screen == "deck") DrawDeck();
            }

            Raylib.EndTextureMode();
            if ((screen is "duel" or "active-chain-review" or "chain-review" or "status-review" or "fusion-review" or "general-fusion-review" or "layerfont-review" or "dice-review" or "coin-review" or "alt-ex-review") && Raylib.IsKeyPressed(KeyboardKey.F12))
            {
                try
                {
                    if (screen == "active-chain-review") CaptureChainReview(canvas);
                    if (screen == "chain-review") CaptureDisabledChainReview(canvas);
                    else if (screen == "status-review") CaptureCardStatusReview(canvas);
                    else if (screen == "fusion-review") CaptureFusionReview(canvas);
                    else if (screen == "general-fusion-review") CaptureGeneralFusionReview(canvas);
                    else if (screen == "layerfont-review") CaptureLayerFontReview(canvas);
                    else if (screen == "dice-review") CaptureDiceReview(canvas);
                    else if (screen == "coin-review") CaptureCoinReview(canvas);
                    else if (screen == "alt-ex-review") CaptureAlternativeExclusionReview(canvas);
                    else CaptureVisualReview(canvas);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
                { Console.Error.WriteLine("[visual-review] screenshot failed: " + ex.Message); }
            }
            Raylib.BeginDrawing(); Raylib.ClearBackground(Color.Black);
            Raylib.DrawTexturePro(canvas.Texture, new Rectangle(0, 0, width, -height),
                new Rectangle(origin.X, origin.Y, width * scale, height * scale),
                Vector2.Zero, 0, Color.White);
            if (useNativeCursor) DrawWindowCursor(scale);
            Raylib.EndDrawing();
        }
        duelAudio = null;
        audio?.Dispose();
        if (useNativeCursor) Raylib.ShowCursor();
        Raylib.UnloadRenderTexture(canvas);
        duelTheme.Dispose();
        graphics.Dispose(); Raylib.CloseWindow(); return 0;
    }
    static string? Option(string[] args, string prefix) =>
        args.FirstOrDefault(arg => arg.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
    static Vector2 mouse;
    static readonly DebugOverlayState debugOverlay = new();
    static bool duelDebug => debugOverlay.Enabled;
    static bool PointerPressed(MouseButton button) =>
        !ConsoleCapturesInput && !debugOverlay.CapturesPointerPress && Raylib.IsMouseButtonPressed(button);
    static bool Button(string text, int x, int y, int w, int h, bool enabled = true)
    {
        bool hover = Raylib.CheckCollisionPointRec(mouse, new Rectangle(x, y, w, h));
        Raylib.DrawRectangle(x, y, w, h, enabled && hover ? new Color(65, 86, 103, 255) : Panel);
        Raylib.DrawRectangleLines(x, y, w, h, enabled ? Gold : Color.Gray);
        int font = w < 150 ? 14 : 18;
        string caption = text;
        while (caption.Length > 0 && MeasureLabel(caption, font) > w - 16) caption = caption[..^1];
        if (caption.Length < text.Length && caption.Length > 3) caption = caption[..^3] + "...";
        Label(caption, x + 8, y + (h - font) / 2, font, enabled ? Color.White : Color.Gray);
        return enabled && hover && PointerPressed(MouseButton.Left);
    }
    static int MeasureLabel(string text, int size) => screen == "duel" && duelTheme.Text is Font font
        ? (int)MathF.Ceiling(Raylib.MeasureTextEx(font, text, size, 1).X)
        : Raylib.MeasureText(text, size);
    static void Label(string text, int x, int y, int size = 20, Color? color = null)
    {
        if (screen == "duel" && duelTheme.Text is Font font)
            Raylib.DrawTextEx(font, text, new(x, y), size, 1, color ?? Color.White);
        else
            Raylib.DrawText(text, x, y, size, color ?? Color.White);
    }
    static void Wrap(string text, int x, int y, int width, int size)
    {
        string line = "";
        foreach (var word in text.Replace('\r', ' ').Replace('\n', ' ').Split(' '))
        {
            if (MeasureLabel(line + word, size) > width && line.Length > 0) { Label(line, x, y, size); y += size + 5; line = ""; }
            line += word + " ";
        }
        Label(line, x, y, size);
    }
    static readonly HashSet<(int Index, bool Miniature)> reportedMissingInspectionArtwork = [];
    static void ValidatePlacementBank(Archive archive)
    {
        NativePlacementAssets.Validate(System.Text.Encoding.Latin1.GetString(
            archive.Read(NativePlacementAssets.Prefix+"set.txt")),NativePlacementAssets.SetBank());
        NativePlacementAssets.Validate(System.Text.Encoding.Latin1.GetString(
            archive.Read(NativePlacementAssets.Prefix+"summon.txt")),
            [NativePlacementAssets.Rainbow,NativePlacementAssets.Star(false,0),NativePlacementAssets.Star(false,1)]);
        NativePlacementAssets.Validate(System.Text.Encoding.Latin1.GetString(
            archive.Read(NativePlacementAssets.Prefix+"sacrifice.txt")),
            [NativePlacementAssets.Prefix+"card_pika_h.yga",NativePlacementAssets.Aura(false),
             NativePlacementAssets.TributeStar(0),NativePlacementAssets.TributeStar(1)]);
    }

    static void DrawArtwork(Card card, int x, int y, int w, int h, int degrees = 0)
    {
        // Native 0041A800 loads independent card/ and mini/ catalogs. Do
        // not downscale a 200x290 texture to 50x72 for the field: the
        // original game ships precomputed 50x72 card miniatures.
        bool miniature = NativeCardRaster.IsNativeMiniDestination(w, h);
        string? name = miniature ? graphics.Artwork.MiniFor(card) : graphics.Artwork.For(card);
        if (name is null && DuelArtworkPolicy.AllowInspectionFallback(includeUnused, screen == "duel",
            playableCards.Any(c => c.Index == card.Index)))
        {
            if (reportedMissingInspectionArtwork.Add((card.Index, miniature)))
                Console.Error.WriteLine($"[artwork inspection] Missing original image for ID {card.Id}, index {card.Index}, mini={miniature}");
            Wrap("Missing art: " + card.Name, x + 3, y + 3, w - 6, 14);
            return;
        }
        var texture = graphics.Texture(DuelArtworkPolicy.Require(card, name, miniature));
        // Integer-sized destination avoids a fractional 49.655-pixel wide
        // field card fitted into a 50x72 viewport.
        var (width, height) = NativeCardRaster.Project(
            texture.Width, texture.Height, w, h, degrees);
        Raylib.DrawTexturePro(texture, new Rectangle(0, 0, texture.Width, texture.Height),
            new Rectangle(x + w / 2f, y + h / 2f, width, height),
            new Vector2(width / 2f, height / 2f), degrees, Color.White);
    }
    static void DrawCards()
    {
        Raylib.DrawTexture(graphics.Texture(@"j\list\list_bg.bmp"), 0, 0, Color.White);
        Raylib.DrawTexture(graphics.Texture(@"j\list\e_cardlistmoji2.bmp"), 205, 0, Color.White);
        bool back = Raylib.CheckCollisionPointRec(mouse, new Rectangle(692, 0, 108, 93));
        Raylib.DrawTexture(graphics.Texture(back ? @"j\list\e_back2.bmp" : @"j\list\e_back1.bmp"), 692, 0, Color.White);
        if (back && Raylib.IsMouseButtonPressed(MouseButton.Left)) screen = "menu";
        int ch; while ((ch = Raylib.GetCharPressed()) > 0) if (ch >= 32 && ch < 127 && search.Length < 50) { search += (char)ch; page = selected = detailScroll = 0; }
        if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && search.Length > 0) { search = search[..^1]; page = selected = detailScroll = 0; }
        Raylib.DrawRectangle(225, 85, 455, 23, Panel);
        string shown = search;
        while (shown.Length > 0 && Raylib.MeasureText("Search: " + shown + "_", 16) > 440) shown = shown[1..];
        Label("Search: " + shown + "_", 230, 88, 16);
        var resources = graphics.CardList;
        if (SpriteButton(joeyOnly ? CardListResources.All : CardListResources.AllSelected, resources.AllPosition, joeyOnly, false))
        { joeyOnly = false; page = selected = detailScroll = 0; }
        if (SpriteButton(joeyOnly ? CardListResources.JoeySelected : CardListResources.Joey, resources.JoeyPosition, !joeyOnly, false))
        { joeyOnly = true; page = selected = detailScroll = 0; }
        var filtered = (joeyOnly ? joeyCards : orderedCards).Where(c => c.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        int lastPage = CardGrid.LastPage(filtered.Count);
        page = Math.Clamp(page, 0, lastPage);
        int direction = SpriteButton(CardListResources.Left, resources.Previous, page > 0) ? -1 : 0;
        if (SpriteButton(CardListResources.Right, resources.Next, page < lastPage)) direction = 1;
        if (Raylib.IsKeyPressed(KeyboardKey.PageUp) && page > 0) direction = -1;
        if (Raylib.IsKeyPressed(KeyboardKey.PageDown) && page < lastPage) direction = 1;
        if (direction != 0) { page = Math.Clamp(page + direction, 0, lastPage); selected = page * CardGrid.PageSize; detailScroll = 0; }
        int hover = resources.Grid.Hit(mouse.X, mouse.Y);
        if (hover >= 0 && page * CardGrid.PageSize + hover < filtered.Count)
        {
            int index = page * CardGrid.PageSize + hover;
            if (selected != index) { selected = index; detailScroll = 0; }
        }
        for (int slot = 0; slot < CardGrid.PageSize && page * CardGrid.PageSize + slot < filtered.Count; slot++)
        {
            int index = page * CardGrid.PageSize + slot;
            var cell = resources.Grid.Cell(slot);
            DrawArtwork(filtered[index], cell.X, cell.Y, CardGrid.CardWidth, CardGrid.CardHeight);
            if (index == selected) Raylib.DrawTexture(graphics.Texture(CardListResources.Frame), cell.X - 4, cell.Y - 4, Color.White);
        }
        SpriteNumber(filtered.Count == 0 ? 0 : page + 1, resources.CurrentPage);
        SpriteNumber(filtered.Count == 0 ? 0 : lastPage + 1, resources.TotalPages);
        Label("/", 499, resources.CurrentPage.Y - 10, 20, Gold);
        Label($"{filtered.Count} {(includeUnused && !joeyOnly ? "records" : "cards")} / { (joeyOnly ? "Joey" : "All games")}", 340, 540, 12, Color.Black);
        if (filtered.Count == 0) { Label("No matches", 230, 120, 18); return; }
        selected = Math.Clamp(selected, 0, filtered.Count - 1); var card = filtered[selected];
        DrawArtwork(card, 6, 87, 200, 290);
        // Limit the description to its panel; scrolling/wrapping is local demonstration behavior.
        string detail = $"{card.Name}\nID {card.Id} / index {card.Index}\n" +
            (card.IsMonster ? $"ATK {card.Attack} DEF {card.Defense} LV {card.Level}\n" : "") + card.Description;
        var lines = TextLines(detail, 170, 14);
        int wheel = (int)Raylib.GetMouseWheelMove();
        if (mouse.X < 210) detailScroll -= wheel * 3;
        detailScroll = Math.Clamp(detailScroll, 0, Math.Max(0, lines.Count - 10));
        for (int i = 0; i < 10 && detailScroll + i < lines.Count; i++)
            Label(lines[detailScroll + i], 16, 397 + i * 18, 14, Color.Black);
        if (lines.Count > 10) Label("Scroll here for text", 16, 580, 12, Color.Black);
    }
    static bool SpriteButton(string name, SceneSprite sprite, bool enabled, bool dimDisabled = true)
    {
        var texture = graphics.Texture(name);
        bool hover = Raylib.CheckCollisionPointRec(mouse, new Rectangle(sprite.X, sprite.Y, texture.Width, texture.Height));
        Raylib.DrawTexture(texture, sprite.X, sprite.Y, !enabled && dimDisabled ? new Color(110, 110, 110, 255) :
            hover ? new Color(255, 255, 200, 255) : Color.White);
        return enabled && hover && PointerPressed(MouseButton.Left);
    }
    static void SpriteNumber(int value, SceneSprite sprite)
    {
        var texture = graphics.Texture(CardListResources.Digits);
        int width = texture.Width / 10;
        string digits = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        // Native 0x00480690 centers the number on its scene position.
        for (int i = 0; i < digits.Length; i++)
            Raylib.DrawTextureRec(texture, new Rectangle((digits[i] - '0') * width, 0, width, texture.Height),
                new Vector2(sprite.X - digits.Length * width / 2 + i * width, sprite.Y - texture.Height / 2), Color.White);
    }
    static int detailScroll;
    static List<string> TextLines(string text, int width, int size)
    {
        List<string> lines = [];
        foreach (var paragraph in text.Replace("\r", "").Split('\n'))
        {
            string line = "";
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length > 0 && MeasureLabel(line + " " + word, size) > width) { lines.Add(line); line = ""; }
                line = line.Length == 0 ? word : line + " " + word;
            }
            lines.Add(line);
        }
        return lines;
    }
}
