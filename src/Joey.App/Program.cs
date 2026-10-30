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
    static void SummonFromHand(int player, int zone)
    {
        if (duel.Phase == DemoPhase.Main && player == duel.ActivePlayer && hand >= 0 && hand < duel.Hand.Count && duel.Hand[hand].IsMonster)
        {
            int needed = DemoDuel.NormalTributes(duel.Hand[hand]);
            if (needed > 0 && tributeZones.Count < needed)
            {
                if (duel.Monsters[player, zone] != null)
                {
                    if (!tributeZones.Remove(zone)) tributeZones.Add(zone);
                    message = tributeZones.Count == needed ? "Tributes selected. Click an empty or selected zone to summon; Cancel to reset." :
                        $"Select {needed - tributeZones.Count} more own tribute(s).";
                }
                else message = $"Select {needed - tributeZones.Count} own tribute(s) first.";
                return;
            }
        }
        if (duel.Summon(hand, player, zone, true, out message, tributeZones)) { hand = -1; tributeZones.Clear(); }
    }
    static void DrawDuel()
    {
        Raylib.DrawTexture(graphics.Texture(@"j\duel\field\fie_normal.bmp"), 230, 0, Color.White);
        if (Button("< Menu", 8, 8, 85, 30)) screen = "menu";
        Label("LOCAL DEMO", 105, 16, 16, Gold);
        if (Button("Draw", 240, 8, 80, 30)) duel.Draw(out message);
        if (Button("Battle", 326, 8, 80, 30)) { duel.BeginBattle(out message); hand = -1; tributeZones.Clear(); }
        if (Button("End turn", 412, 8, 105, 30)) { duel.EndTurn(out message); hand = attacker = -1; handPage = 0; tributeZones.Clear(); }
        if (Button("Reset", 523, 8, 75, 30)) StartDemo();
        Label(duel.Winner is int winner ? $"PLAYER {winner + 1} WINS" : $"Turn {duel.Turn} P{duel.ActivePlayer + 1}: {duel.Phase}", 8, 50, 16, Gold);
        Raylib.DrawRectangle(240, 46, 545, 24, Panel);
        Label($"P2 LP {duel.Life[1]}     P1 LP {duel.Life[0]}     Hand: {duel.Hand.Count}", 245, 51, 16);
        if (hand >= 0 && hand < duel.Hand.Count) DrawArtwork(duel.Hand[hand], 8, 78, 200, 290);
        else if (attacker >= 0 && duel.Monsters[duel.ActivePlayer, attacker] is { } selectedAttacker) DrawArtwork(selectedAttacker, 8, 78, 200, 290);
        else Wrap("Select a hand card or an attacking monster to preview its original image.", 12, 95, 195, 18);
        if (Button(duel.PendingEndDiscards > 0 ? "Discard" : "Use card", 8, 380, 98, 30))
        {
            bool used = duel.PendingEndDiscards > 0 ? duel.DiscardForEndTurn(hand, out message) : duel.Play(hand, out message);
            if (used) { hand = attacker = -1; handPage = 0; tributeZones.Clear(); }
        }
        if (Button("Direct attack", 112, 380, 108, 30)) duel.Attack(attacker, -1, out message);
        if (Button("Cancel", 8, 416, 98, 30)) { hand = attacker = -1; tributeZones.Clear(); message = "Selection cleared."; }
        if (Button("Hand <", 8, 452, 98, 30, handPage > 0)) handPage--;
        if (Button("Hand >", 112, 452, 108, 30, (handPage + 1) * 6 < duel.Hand.Count)) handPage++;
        for (int player = 0; player < 2; player++)
        {
            int y = player == 1 ? 192 : 312;
            for (int z = 0; z < 5; z++)
            {
                var card = duel.Monsters[player, z];
                int x = 306 + z * 80;
                if (Button(card == null ? "Monster" : "", x, y, 74, 92))
                {
                    if (duel.Phase == DemoPhase.Battle)
                    {
                        if (player == duel.ActivePlayer && card != null) { attacker = z; hand = -1; message = $"Attacker: {card.Name}. Choose a target."; }
                        else if (player != duel.ActivePlayer) duel.Attack(attacker, z, out message);
                        else message = "Choose an occupied friendly monster as attacker.";
                    }
                    else if (hand >= 0 && hand < duel.Hand.Count && duel.Hand[hand].Id == 300)
                    { if (duel.Equip(hand, player, z, out message)) hand = -1; }
                    else if (card != null && player == duel.ActivePlayer && hand < 0) duel.ChangePosition(z, out message);
                    else SummonFromHand(player, z);
                }
                if (card != null)
                {
                    DrawArtwork(card, x + 3, y + 2, 68, 66);
                    Label(duel.DefensePosition[player, z] ? "DEF" : "ATK", x + 3, y + 67, 10, Gold);
                    Label($"{duel.AttackValue(player, z)}/{duel.DefenseValue(player, z)}", x + 3, y + 79, 10, Gold);
                }
                if (attacker == z && player == duel.ActivePlayer) Raylib.DrawRectangleLines(x, y, 74, 92, Color.White);
                if (player == duel.ActivePlayer && tributeZones.Contains(z)) Raylib.DrawRectangleLines(x, y, 74, 92, Color.Red);
                var equip = duel.EquipmentCards.FirstOrDefault(e => e.Owner == player && e.SourceZone == z);
                if (Button(equip == null ? "Spell" : "Equip", x, player == 1 ? 118 : 410, 74, 35))
                    message = equip == null ? "For Sword select the card, then a Warrior monster." : $"Equipment targets player {equip.TargetPlayer + 1}, zone {equip.TargetZone + 1}.";
            }
        }
        handPage = Math.Clamp(handPage, 0, Math.Max(0, (duel.Hand.Count - 1) / 6));
        for (int slot = 0; slot < 6 && handPage * 6 + slot < duel.Hand.Count; slot++)
        {
            int i = handPage * 6 + slot, x = 236 + slot * 93;
            if (Button("", x, 458, 86, 120))
            {
                hand = i; attacker = -1; tributeZones.Clear();
                message = duel.PendingEndDiscards > 0 ? $"Selected {duel.Hand[i].Name}. Use Discard to finish the turn." :
                    duel.Hand[i].IsMonster && DemoDuel.NormalTributes(duel.Hand[i]) > 0 ?
                    $"Select {DemoDuel.NormalTributes(duel.Hand[i])} own tribute(s), then an empty or selected zone." :
                    $"Selected {duel.Hand[i].Name}. Summon, Use card, or target a Warrior with Sword.";
            }
            DrawArtwork(duel.Hand[i], x + 3, 461, 80, 114);
            if (i == hand) Raylib.DrawRectangleLines(x, 458, 86, 120, Color.White);
        }
        var lines = TextLines(message, 205, 14);
        for (int i = 0; i < Math.Min(4, lines.Count); i++) Label(lines[i], 8, 492 + i * 17, 14, Gold);
        Label($"Deck {duel.DeckCount(0)}/{duel.DeckCount(1)} Grave {duel.Graveyard(0).Count}/{duel.Graveyard(1).Count}", 8, 568, 13);
        Label("Demonstration duel | Simplified layout and rules", 8, 587, 12, Gold);
    }
}
