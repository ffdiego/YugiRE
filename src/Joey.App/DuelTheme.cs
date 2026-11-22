using Joey.LookAndFeel;
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
    private Font? nativeDetailRegular, nativeDetailBold;

    /// <summary>
    /// Native English Joey uses GDI fonts rather than duel/font/number.txt,
    /// which is a DECIMAL sprite atlas. A theme-supplied font can override
    /// the face, but default duel detail prefers Courier New as selected
    /// by 0041B3E0 -> 0040F650 -> 00544B80 for the English locale.
    /// </summary>
    public Font? DetailTextFont(bool bold) =>
        Text ?? (bold ? nativeDetailBold ?? nativeDetailRegular :
            nativeDetailRegular ?? nativeDetailBold);

    public void LoadNativeDetailFonts()
    {
        // Themes already carry their own explicit font; no need to load
        // another one. Do not package or distribute any operating-system
        // fonts in the repository or the user's output.
        if (Text is not null) return;
        nativeDetailRegular = LoadAvailableFont(NativeDetailPanel.EnglishTextSize,
            "cour.ttf", "LiberationMono-Regular.ttf", "DejaVuSansMono.ttf",
            "Courier New.ttf");
        nativeDetailBold = LoadAvailableFont(NativeDetailPanel.EnglishTextSize,
            "courbd.ttf", "LiberationMono-Bold.ttf", "DejaVuSansMono-Bold.ttf",
            "Courier New Bold.ttf");
        if (nativeDetailRegular is null && nativeDetailBold is null)
            Console.WriteLine("[detail] Courier New-compatible system font unavailable; using raylib fallback.");
    }

    private static Font? LoadAvailableFont(int size, params string[] names)
    {
        string windowsFonts = Path.Combine(
            Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows", "Fonts");
        string[] roots =
        [
            windowsFonts,
            "/System/Library/Fonts/Supplemental",
            "/usr/share/fonts/truetype/liberation2",
            "/usr/share/fonts/truetype/liberation",
            "/usr/share/fonts/liberation-mono",
            "/usr/share/fonts/truetype/dejavu",
            "/usr/share/fonts/dejavu-sans-mono-fonts"
        ];
        int[] glyphs =
        [
            .. Enumerable.Range(32, 95), .. Enumerable.Range(160, 96),
            0x2013, 0x2014, 0x2018, 0x2019, 0x201C, 0x201D
        ];
        foreach (string root in roots)
        foreach (string name in names)
        {
            string file = Path.Combine(root, name);
            if (!File.Exists(file)) continue;
            var font = Raylib.LoadFontEx(file, size, glyphs, glyphs.Length);
            if (!Raylib.IsFontValid(font) ||
                font.Texture.Id == Raylib.GetFontDefault().Texture.Id)
                continue;
            Raylib.SetTextureFilter(font.Texture, TextureFilter.Point);
            return font;
        }
        return null;
    }

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
        if (nativeDetailRegular is Font regular) Raylib.UnloadFont(regular);
        if (nativeDetailBold is Font bold) Raylib.UnloadFont(bold);
        nativeDetailRegular = nativeDetailBold = null;
        if (boardFile is Texture2D board) Raylib.UnloadTexture(board);
        if (backFile is Texture2D back) Raylib.UnloadTexture(back);
        Text = null;
        boardFile = backFile = null;
        boardEntry = backEntry = null;
        dat = null;
    }
}
