using Raylib_cs;

/// <summary>Optional GPU resources supplied by the player for the duel screen.</summary>
sealed class DuelTheme : IDisposable
{
    public Texture2D? Board { get; private set; }
    public Texture2D? CardBack { get; private set; }
    public Font? Text { get; private set; }

    public void Load(string directory)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Duel theme directory not found: {directory}");

        Board = LoadTexture(directory, "board.png");
        CardBack = LoadTexture(directory, "card-back.png");
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

        if (Board is null && CardBack is null && Text is null)
            throw new InvalidDataException("Duel theme needs at least one of board.png, card-back.png, or font.ttf.");
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
        if (Board is Texture2D board) Raylib.UnloadTexture(board);
        if (CardBack is Texture2D back) Raylib.UnloadTexture(back);
        Text = null;
        Board = CardBack = null;
    }
}
