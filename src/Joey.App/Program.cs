using Joey.Core;
using Raylib_cs;
using System.Numerics;

return Game.Run(args);

static class Game
{
    static readonly Color Background = new(16, 23, 35, 255), Panel = new(29, 42, 57, 255), Gold = new(230, 185, 93, 255);
    static List<Card> cards = [];
    static string screen = "menu", search = "", error = "", message = "Select a hand card, then a monster zone.";
    static int page, selected, hand = -1, attacker = -1, handPage;
    static DemoDuel duel = null!;
    static DatGraphics graphics = null!;
    public static int Run(string[] args)
    {
        using var resources = new DatGraphics();
        graphics = resources;
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
            Console.WriteLine($"Read {archive.Entries.Count} archive entries and {cards.Count} card records from {path}");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or KeyNotFoundException or OverflowException)
        { error = ex.Message; Console.Error.WriteLine(error); }
        if (args.Contains("--check-data")) return error.Length == 0 ? 0 : 1;
        if (args.Contains("--check-graphics"))
        {
            if (error.Length > 0) return 1;
            try { graphics.Validate(); return 0; }
            catch (Exception ex) when (ex is InvalidDataException or IOException or KeyNotFoundException)
            { Console.Error.WriteLine(ex.Message); return 1; }
        }
        if (OperatingSystem.IsLinux() && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        { Console.Error.WriteLine("No graphical session available. Run from a desktop terminal; use --check-data for data validation."); return 2; }
        duel = new(cards);
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
        Raylib.InitWindow(800, 600, "Joey RE - DAT graphics / demonstration duel");
        Raylib.SetExitKey(KeyboardKey.Null); Raylib.SetTargetFPS(60);
        var demoCanvas = Raylib.LoadRenderTexture(1200, 800);
        var menuCanvas = Raylib.LoadRenderTexture(800, 600);
        bool quit = false;
        while (!Raylib.WindowShouldClose() && !quit)
        {
            bool menuFrame = screen == "menu";
            int width = menuFrame ? 800 : 1200, height = menuFrame ? 600 : 800;
            var canvas = menuFrame ? menuCanvas : demoCanvas;
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
                        bool enabled = row is 0 or 2 or 4;
                        var texture = graphics.Texture(enabled && hover == row ? MenuResources.Highlight : MenuResources.Normal);
                        Raylib.DrawTextureRec(texture, new Rectangle(0, row * 50, 400, 50),
                            new Vector2(menu.Buttons.X, menu.Buttons.Y + row * 50), enabled ? Color.White : new Color(150, 150, 150, 255));
                    }
                    if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                    {
                        if (hover == 0) { screen = "duel"; duel = new(cards); hand = attacker = -1; handPage = 0; message = "Local two-player demo. Draw or summon, then enter battle."; }
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
                if (Button("< Menu", 24, 18, 130, 38) || Raylib.IsKeyPressed(KeyboardKey.Escape)) screen = "menu";
                if (screen == "cards") DrawCards();
                else if (screen == "duel") DrawDuel();
            }
            if (!menuFrame) Label("Temporary layout | Local DAT resources | Demonstration build", 24, 775, 16, Gold);
            Raylib.EndTextureMode();
            Raylib.BeginDrawing(); Raylib.ClearBackground(Color.Black);
            Raylib.DrawTexturePro(canvas.Texture, new Rectangle(0, 0, width, -height), new Rectangle(origin.X, origin.Y, width * scale, height * scale), Vector2.Zero, 0, Color.White);
            Raylib.EndDrawing();
        }
        Raylib.UnloadRenderTexture(demoCanvas); Raylib.UnloadRenderTexture(menuCanvas);
        graphics.Dispose(); Raylib.CloseWindow(); return 0;
    }
    static Vector2 mouse;
    static bool Button(string text, int x, int y, int w, int h, bool enabled = true)
    {
        bool hover = Raylib.CheckCollisionPointRec(mouse, new Rectangle(x, y, w, h));
        Raylib.DrawRectangle(x, y, w, h, enabled && hover ? new Color(65, 86, 103, 255) : Panel);
        Raylib.DrawRectangleLines(x, y, w, h, enabled ? Gold : Color.Gray);
        string caption = text;
        while (caption.Length > 0 && Raylib.MeasureText(caption, 18) > w - 22) caption = caption[..^1];
        if (caption.Length < text.Length && caption.Length > 3) caption = caption[..^3] + "...";
        Label(caption, x + 10, y + (h - 18) / 2, 18, enabled ? Color.White : Color.Gray);
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
    static void DrawCards()
    {
        Label("CARD LIST - all resource entries available", 190, 26, 23, Gold);
        int ch; while ((ch = Raylib.GetCharPressed()) > 0) if (ch >= 32 && ch < 127 && search.Length < 50) { search += (char)ch; page = selected = 0; }
        if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && search.Length > 0) { search = search[..^1]; page = selected = 0; }
        Label("Type to search: " + search + "_", 25, 82, 22);
        var filtered = cards.Where(c => c.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        page = Math.Clamp(page, 0, Math.Max(0, (filtered.Count - 1) / 12));
        for (int row = 0; row < 12 && page * 12 + row < filtered.Count; row++)
        {
            int index = page * 12 + row; var c = filtered[index];
            string name = c.Name.Length == 0 ? "[unnamed resource]" : c.Name;
            if (Button($"{c.Id}: {name}", 25, 125 + row * 44, 550, 38)) selected = index;
        }
        if (Button("Previous", 25, 670, 140, 38, page > 0)) page--;
        if (Button("Next", 180, 670, 140, 38, (page + 1) * 12 < filtered.Count)) page++;
        Label($"Page {page + 1} | {filtered.Count} records", 25, 725);
        if (filtered.Count == 0) { Label("No matching records", 630, 140); return; }
        selected = Math.Clamp(selected, 0, filtered.Count - 1); var card = filtered[selected];
        Raylib.DrawRectangle(620, 120, 550, 620, Panel);
        Wrap(card.Name.Length == 0 ? "[Unnamed resource]" : card.Name, 640, 140, 510, 28);
        Label($"Resource ID {card.Id} | Index {card.Index} | Class {card.ClassCode}", 640, 225, 19, Gold);
        if (card.IsMonster) Label($"ATK {card.Attack}  DEF {card.Defense}  Level {card.Level}", 640, 270, 23);
        Wrap(card.Description, 640, 330, 500, 22);
        Label("Illustration not implemented", 640, 700, 18, Gold);
    }
    static void DrawDuel()
    {
        Label("LOCAL DEMO", 190, 26, 24, Gold);
        if (Button("Draw", 650, 18, 120, 38)) duel.Draw(out message);
        if (Button("Battle", 785, 18, 120, 38)) { duel.BeginBattle(out message); hand = -1; }
        if (Button("Use card", 24, 150, 115, 45)) { if (duel.Play(hand, out message)) hand = -1; }
        if (Button("Direct attack", 24, 210, 115, 45)) duel.Attack(attacker, -1, out message);
        if (Button("Cancel", 24, 270, 115, 38)) { hand = attacker = -1; message = "Selection cleared."; }
        Label(duel.Winner is int winner ? $"PLAYER {winner + 1} WINS" : $"Phase: {duel.Phase}", 390, 120, 20, Gold);
        Label($"Player 2 LP {duel.Life[1]}", 30, 85, 23);
        Label($"Turn {duel.Turn} | {($"Player {duel.ActivePlayer + 1}")}", 440, 85, 23);
        Label($"Player 1 LP {duel.Life[0]}", 920, 85, 23);
        for (int player = 0; player < 2; player++)
        {
            int y = player == 1 ? 145 : 365;
            for (int z = 0; z < 5; z++)
            {
                var card = duel.Monsters[player, z];
                int x = 150 + z * 180;
                if (Button(card == null ? "Monster" : "", x, y, 162, 105))
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
                    else if (duel.Summon(hand, player, z, true, out message)) hand = -1;
                }
                if (card != null) { Wrap(card.Name, x + 8, y + 8, 145, 16); Label($"{(duel.DefensePosition[player, z] ? "DEF" : "ATK")} {duel.AttackValue(player, z)}/{duel.DefenseValue(player, z)}", x + 8, y + 77, 17, Gold); }
                var equip = duel.EquipmentCards.FirstOrDefault(e => e.Owner == player && e.SourceZone == z);
                if (Button(equip == null ? "Spell / Trap" : equip.Card.Name, x, y + 112, 162, 50)) message = equip == null ? "For Sword select the card, then a Warrior monster." : $"Equipment targets player {equip.TargetPlayer + 1}, zone {equip.TargetZone + 1}.";
            }
        }
        handPage = Math.Clamp(handPage, 0, Math.Max(0, (duel.Hand.Count - 1) / 6));
        Label($"Active hand: {duel.Hand.Count} | Deck P1/P2: {duel.DeckCount(0)}/{duel.DeckCount(1)} | Grave P1/P2: {duel.Graveyard(0).Count}/{duel.Graveyard(1).Count}", 25, 535, 18);
        for (int slot = 0; slot < 6 && handPage * 6 + slot < duel.Hand.Count; slot++)
        {
            int i = handPage * 6 + slot, x = 30 + slot * 185;
            if (Button("", x, 560, 175, 155)) { hand = i; attacker = -1; message = $"Selected {duel.Hand[i].Name}. Summon, Use card, or target a Warrior with Sword."; }
            Wrap(duel.Hand[i].Name, x + 9, 575, 155, 18);
            Label(i == hand ? "SELECTED" : (duel.Hand[i].IsMonster ? $"ATK {duel.Hand[i].Attack}" : "Use card"), x + 9, 685, 16, Gold);
        }
        if (Button("Hand <", 24, 330, 115, 35, handPage > 0)) handPage--;
        if (Button("Hand >", 24, 375, 115, 35, (handPage + 1) * 6 < duel.Hand.Count)) handPage++;
        if (Button("End turn", 920, 18, 120, 38)) { duel.NextTurn(); hand = attacker = -1; handPage = 0; message = "Turn changed. Draw once; summon once; Battle to attack."; }
        if (Button("Reset", 1050, 18, 120, 38)) { duel = new(cards); hand = attacker = -1; handPage = 0; message = "Demo reset."; }
        Label(message, 25, 738, 19, Gold);
    }
}
