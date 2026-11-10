using Joey.Core;
using Raylib_cs;
using System.Numerics;

return Game.Run(args);

static partial class Game
{
    static readonly Color Background = new(16, 23, 35, 255), Panel = new(29, 42, 57, 255), Gold = new(230, 185, 93, 255);
    static List<Card> cards = [], orderedCards = [], joeyCards = [];
    static bool joeyOnly;
    static readonly List<int> tributeZones = [];
    static string screen = "menu", search = "", error = "", message = "Select a hand card, then a monster zone.";
    static int page, selected, hand = -1, attacker = -1, handPage;
    static DemoDuel duel = null!;
    static DatGraphics graphics = null!;
    static bool includeUnused;
    public static int Run(string[] args)
    {
        using var resources = new DatGraphics();
        graphics = resources;
        includeUnused = args.Contains("--include-unused");
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
            var archive = graphics.Open(path);
            cards = Catalog.Load(archive);
            graphics.LoadArtwork(cards);
            orderedCards = CardListResources.Sort(cards, archive.Read(@"bin#\card_sorteng.bin"));
            if (!includeUnused) orderedCards = CardListResources.ForPacks(orderedCards, archive.Read(@"bin#\card_pack.bin"));
            joeyCards = CardListResources.ForPacks(orderedCards, archive.Read(@"bin#\card_pack.bin"), 4);
            Console.WriteLine($"Read {archive.Entries.Count} archive entries and {cards.Count} card records from {path}");
            Console.WriteLine($"Card List: {orderedCards.Count} entries (include unused: {includeUnused})");
            InitializeDeck(args, path);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException or OverflowException)
        { error = ex.Message; Console.Error.WriteLine(error); }
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
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
        Raylib.InitWindow(800, 600, "Joey RE - DAT graphics / demonstration duel");
        Raylib.SetExitKey(KeyboardKey.Null); Raylib.SetTargetFPS(60);
        var canvas = Raylib.LoadRenderTexture(800, 600);
        bool quit = false;
        while (!Raylib.WindowShouldClose() && !quit)
        {
            const int width = 800, height = 600;
            graphics.BeginFrame();
            float scale = Math.Min(Raylib.GetScreenWidth() / (float)width, Raylib.GetScreenHeight() / (float)height);
            Vector2 origin = new((Raylib.GetScreenWidth() - width * scale) / 2, (Raylib.GetScreenHeight() - height * scale) / 2);
            mouse = (Raylib.GetMousePosition() - origin) / scale;
            Raylib.BeginTextureMode(canvas); Raylib.ClearBackground(Background);
            if (screen == "menu")
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
                    if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                    {
                        if (hover == 0) StartDemo();
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
                if (Raylib.IsKeyPressed(KeyboardKey.Escape)) screen = "menu";
                if (screen == "cards") DrawCards();
                else if (screen == "duel") DrawDuel();
                else if (screen == "deck") DrawDeck();
            }

            Raylib.EndTextureMode();
            Raylib.BeginDrawing(); Raylib.ClearBackground(Color.Black);
            Raylib.DrawTexturePro(canvas.Texture, new Rectangle(0, 0, width, -height), new Rectangle(origin.X, origin.Y, width * scale, height * scale), Vector2.Zero, 0, Color.White);
            Raylib.EndDrawing();
        }
        Raylib.UnloadRenderTexture(canvas);
        graphics.Dispose(); Raylib.CloseWindow(); return 0;
    }
    static Vector2 mouse;
    static bool Button(string text, int x, int y, int w, int h, bool enabled = true)
    {
        bool hover = Raylib.CheckCollisionPointRec(mouse, new Rectangle(x, y, w, h));
        Raylib.DrawRectangle(x, y, w, h, enabled && hover ? new Color(65, 86, 103, 255) : Panel);
        Raylib.DrawRectangleLines(x, y, w, h, enabled ? Gold : Color.Gray);
        int font = w < 150 ? 14 : 18;
        string caption = text;
        while (caption.Length > 0 && Raylib.MeasureText(caption, font) > w - 16) caption = caption[..^1];
        if (caption.Length < text.Length && caption.Length > 3) caption = caption[..^3] + "...";
        Label(caption, x + 8, y + (h - font) / 2, font, enabled ? Color.White : Color.Gray);
        return enabled && hover && Raylib.IsMouseButtonPressed(MouseButton.Left);
    }
    static void Label(string text, int x, int y, int size = 20, Color? color = null) => Raylib.DrawText(text, x, y, size, color ?? Color.White);
    static void Wrap(string text, int x, int y, int width, int size)
    {
        string line = "";
        foreach (var word in text.Replace('\r', ' ').Replace('\n', ' ').Split(' '))
        {
            if (Raylib.MeasureText(line + word, size) > width && line.Length > 0) { Label(line, x, y, size); y += size + 5; line = ""; }
            line += word + " ";
        }
        Label(line, x, y, size);
    }
    static void DrawArtwork(Card card, int x, int y, int w, int h)
    {
        string? name = graphics.Artwork.For(card);
        if (name == null) { Wrap(card.Name, x + 3, y + 3, w - 6, 14); return; }
        var texture = graphics.Texture(name);
        float scale = Math.Min(w / (float)texture.Width, h / (float)texture.Height);
        Raylib.DrawTexturePro(texture, new Rectangle(0, 0, texture.Width, texture.Height),
            new Rectangle(x + (w - texture.Width * scale) / 2, y + (h - texture.Height * scale) / 2,
                texture.Width * scale, texture.Height * scale), Vector2.Zero, 0, Color.White);
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
        return enabled && hover && Raylib.IsMouseButtonPressed(MouseButton.Left);
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
                if (line.Length > 0 && Raylib.MeasureText(line + " " + word, size) > width) { lines.Add(line); line = ""; }
                line = line.Length == 0 ? word : line + " " + word;
            }
            lines.Add(line);
        }
        return lines;
    }
}
