using Raylib_cs;

/// <summary>Optional GPU resources supplied by the player for the duel screen.</summary>
sealed class DuelTheme : IDisposable
{
    private Texture2D? boardFile, backFile;
    private DatGraphics? dat;
    private string? boardEntry, backEntry;

    public Texture2D? Board => boardEntry is string name ? dat!.Texture(name) : boardFile;
    public Texture2D? CardBack => backEntry is string name ? dat!.Texture(name) : backFile;
    public Font? Text { get; private set; }

    public void Load(string directory)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Duel theme directory not found: {directory}");

        boardFile = LoadTexture(directory, "board.png");
        backFile = LoadTexture(directory, "card-back.png");
        string path = Path.Combine(directory, "font.ttf");
        if (File.Exists(path))
        {
            int[] glyphs = [.. Enumerable.Range(32, 95), .. Enumerable.Range(160, 96), 0x2013, 0x2014, 0x2019];
            var font = Raylib.LoadFontEx(path, 48, glyphs, glyphs.Length);
            if (!Raylib.IsFontValid(font) || font.Texture.Id == Raylib.GetFontDefault().Texture.Id)
                throw new InvalidDataException($"Cannot load duel theme font: {path}");
            Text = font;
            Raylib.SetTextureFilter(font.Texture, TextureFilter.Bilinear);
        }

        if (boardFile is null && backFile is null && Text is null)
            throw new InvalidDataException("Duel theme needs at least one of board.png, card-back.png, or font.ttf.");
    }

    public void LoadDatArt(DatGraphics graphics, string? board, string? back)
    {
        foreach (string? name in new[] { board, back })
        {
            if (name is null) continue;
            if (!name.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) &&
                !name.EndsWith(".yga", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Duel theme DAT entry must be BMP or YGA: {name}");
            graphics.Texture(name);
        }
        dat = graphics;
        boardEntry = board;
        backEntry = back;
    }

    private static Texture2D? LoadTexture(string directory, string name)
    {
        string path = Path.Combine(directory, name);
        if (!File.Exists(path)) return null;
        var texture = Raylib.LoadTexture(path);
        if (!Raylib.IsTextureValid(texture))
            throw new InvalidDataException($"Cannot load duel theme image: {path}");
        return texture;
    }

    public void Dispose()
    {
        if (Text is Font font) Raylib.UnloadFont(font);
        if (boardFile is Texture2D board) Raylib.UnloadTexture(board);
        if (backFile is Texture2D back) Raylib.UnloadTexture(back);
        Text = null;
        boardFile = backFile = null;
        boardEntry = backEntry = null;
        dat = null;
    }
}
