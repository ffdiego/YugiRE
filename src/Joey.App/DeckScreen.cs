using Joey.Core;
using Raylib_cs;

static partial class Game
{
    static DeckFile? importedDeck;
    static IReadOnlyList<Card>[] deckGroups = [];
    static string deckPath = "", deckError = "";
    static int deckGroup, deckPage, deckSelected;
    static bool useImportedDeck;

    static void InitializeDeck(string[] args, string archivePath)
    {
        string? option = args.FirstOrDefault(a => a.StartsWith("--deck=", StringComparison.OrdinalIgnoreCase));
        deckPath = option == null ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(archivePath))!, "deck.ydc") : option[7..];
        LoadDeck();
    }
    static void LoadDeck()
    {
        try
        {
            var parsed = DeckFile.Load(deckPath);
            var resolved = Enumerable.Range(0, 3).Select(group => parsed.Resolve(group, cards)).ToArray();
            importedDeck = parsed; deckGroups = resolved; deckError = "";
            useImportedDeck = false;
            deckGroup = deckPage = deckSelected = 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            importedDeck = null; deckGroups = []; useImportedDeck = false;
            deckError = ex.Message;
        }
    }
    static int CheckDeck()
    {
        if (error.Length > 0) return 1;
        if (importedDeck == null) { Console.Error.WriteLine(deckError); return 1; }
        Console.WriteLine($"YDC: {deckPath}; header {importedDeck.HeaderHex}; main size accepted: {importedDeck.HasDuelSize}");
        for (int group = 0; group < deckGroups.Length; group++)
        {
            Console.WriteLine($"List {group}: {deckGroups[group].Count}");
            foreach (var card in deckGroups[group]) Console.WriteLine($"  {card.Id}: {card.Name}");
        }
        return 0;
    }
    static DemoDuel NewDemo() => useImportedDeck && importedDeck?.HasDuelSize == true
        ? DemoDuel.FromDecks(deckGroups[0], deckGroups[0]) : new(cards);
    static void StartDemo()
    {
        tributeZones.Clear();
        duel = NewDemo(); screen = "duel"; hand = attacker = -1; handPage = 0;
        message = useImportedDeck ? "Imported main deck for both local players. Only selected effects work in this demo." :
            "Local two-player demo. Draw or summon, then enter battle.";
    }
    static void DrawDeck()
    {
        Raylib.DrawTexture(graphics.Texture(@"j\list\list_bg.bmp"), 0, 0, Color.White);
        if (Button("< Menu", 8, 8, 100, 30)) screen = "menu";
        Label("DECK INSPECTOR", 225, 20, 26, Gold);
        if (Button("Reload", 680, 16, 100, 30)) LoadDeck();
        string filename = Path.GetFileName(deckPath);
        while (filename.Length > 0 && Raylib.MeasureText(filename, 16) > 540) filename = filename[1..];
        Label(filename, 225, 58, 16, Color.Black);
        if (importedDeck == null)
        {
            Wrap("Open the deck through --deck=path/to/deck.ydc, or place deck.ydc beside data.dat and press Reload.", 225, 120, 530, 20);
            Wrap(deckError, 225, 260, 530, 18);
            Label("Read-only deck viewer / temporary interface", 225, 540, 14, Color.Black);
            return;
        }
        for (int group = 0; group < 3; group++)
        {
            string caption = (group == 0 ? "Main" : $"List {group}") + $" ({deckGroups[group].Count})";
            if (Button(caption, 225 + group * 180, 85, 175, 23)) { deckGroup = group; deckPage = deckSelected = 0; }
            if (deckGroup == group) Raylib.DrawRectangleLines(225 + group * 180, 85, 175, 23, Color.White);
        }
        var current = deckGroups[deckGroup];
        var layout = graphics.CardList;
        int last = CardGrid.LastPage(current.Count);
        int direction = SpriteButton(CardListResources.Left, layout.Previous, deckPage > 0) ? -1 : 0;
        if (SpriteButton(CardListResources.Right, layout.Next, deckPage < last)) direction = 1;
        if (direction != 0) { deckPage = Math.Clamp(deckPage + direction, 0, last); deckSelected = deckPage * CardGrid.PageSize; }
        int hover = layout.Grid.Hit(mouse.X, mouse.Y);
        if (hover >= 0 && deckPage * CardGrid.PageSize + hover < current.Count) deckSelected = deckPage * CardGrid.PageSize + hover;
        for (int slot = 0; slot < CardGrid.PageSize && deckPage * CardGrid.PageSize + slot < current.Count; slot++)
        {
            int index = deckPage * CardGrid.PageSize + slot;
            var cell = layout.Grid.Cell(slot);
            DrawArtwork(current[index], cell.X, cell.Y, CardGrid.CardWidth, CardGrid.CardHeight);
            if (index == deckSelected) Raylib.DrawTexture(graphics.Texture(CardListResources.Frame), cell.X - 4, cell.Y - 4, Color.White);
        }
        SpriteNumber(current.Count == 0 ? 0 : deckPage + 1, layout.CurrentPage);
        SpriteNumber(current.Count == 0 ? 0 : last + 1, layout.TotalPages);
        Label("/", 499, layout.CurrentPage.Y - 10, 20, Gold);
        if (current.Count > 0)
        {
            var card = current[Math.Clamp(deckSelected, 0, current.Count - 1)];
            DrawArtwork(card, 6, 87, 200, 290);
            var lines = TextLines(card.Name + "\n" + card.Description, 170, 14);
            for (int i = 0; i < Math.Min(10, lines.Count); i++) Label(lines[i], 16, 397 + i * 18, 14, Color.Black);
        }
        if (Button("Play this deck", 225, 532, 165, 28, importedDeck.HasDuelSize)) { useImportedDeck = true; StartDemo(); }
        if (Button("Demo fixture", 600, 532, 175, 28)) { useImportedDeck = false; StartDemo(); }
        Label("Read-only deck / demo uses main list for both players", 225, 590, 12, Color.Black);
    }
}
