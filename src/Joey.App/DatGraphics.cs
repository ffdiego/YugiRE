using Joey.Core;
using Raylib_cs;

// GPU resources are owned here; originals stay in the archive and are never written out.
sealed class DatGraphics : IDisposable
{
    readonly Dictionary<string, Texture2D> textures = new(StringComparer.OrdinalIgnoreCase);
    readonly Queue<string> loaded = new();
    Archive? archive;
    public MenuResources? Menu { get; private set; }
    public CardListResources CardList { get; private set; } = null!;
    public CardArtwork Artwork { get; private set; } = null!;
    public void LoadArtwork(List<Card> cards) => Artwork = CardArtwork.Load(archive!, cards);
    // Evict between frames, after the previous Raylib batch has been submitted.
    public void BeginFrame()
    {
        while (textures.Count > 64)
        {
            string name = loaded.Dequeue();
            Raylib.UnloadTexture(textures[name]);
            textures.Remove(name);
        }
    }
    public Archive Open(string path)
    {
        archive = new Archive(path);
        Menu = MenuResources.Load(archive);
        CardList = CardListResources.Load(archive);
        return archive;
    }
    public Texture2D Texture(string name)
    {
        if (textures.TryGetValue(name, out var cached)) return cached;
        var image = ReadImage(name);
        Texture2D texture;
        try { texture = Raylib.LoadTextureFromImage(image); }
        finally { Raylib.UnloadImage(image); }
        if (texture.Id == 0) throw new InvalidDataException($"Cannot upload texture {name}");
        Raylib.SetTextureFilter(texture, TextureFilter.Point);
        textures.Add(name, texture);
        loaded.Enqueue(name);
        return texture;
    }
    Image ReadImage(string name)
    {
        byte[] bytes = (archive ?? throw new InvalidOperationException("No DAT open")).Read(name);
        var info = BitmapInfo.Read(bytes);
        var image = Raylib.LoadImageFromMemory(".bmp", bytes);
        if (image.Width != info.Width || image.Height != info.Height)
        {
            Raylib.UnloadImage(image);
            throw new InvalidDataException($"Cannot decode BMP {name}");
        }
        if (name is CardListResources.Frame or CardListResources.Left or CardListResources.Right or CardListResources.Digits)
        {
            Raylib.ImageFormat(ref image, PixelFormat.UncompressedR8G8B8A8);
            Raylib.ImageColorReplace(ref image, new Color(0, 255, 0, 255), Color.Blank);
        }
        return image;
    }
    public void Validate()
    {
        Raylib.SetTraceLogLevel(TraceLogLevel.Warning);
        var names = new[] { MenuResources.Background, MenuResources.Normal, MenuResources.Highlight,
            CardListResources.Frame, CardListResources.Left, CardListResources.Right, CardListResources.Digits,
            @"j\list\list_bg.bmp", @"j\list\e_cardlistmoji2.bmp", @"j\list\e_back1.bmp", @"j\list\e_back2.bmp", @"j\duel\field\fie_normal.bmp" }
            .Concat(archive!.Entries.Where(e => e.Name.StartsWith(@"card\", StringComparison.OrdinalIgnoreCase) && e.Name.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)).Select(e => e.Name));
        int count = 0;
        foreach (string name in names)
        {
            var image = ReadImage(name);
            try
            {
                if (name == CardListResources.Frame && Raylib.GetImageColor(image, 20, 20).A != 0)
                    throw new InvalidDataException("Card selection frame color key is not transparent");
            }
            finally { Raylib.UnloadImage(image); }
            count++;
        }
        Console.WriteLine($"Decoded {count} BMP resources, including card artwork; {Artwork.Count} card mappings.");
    }
    public void Dispose()
    {
        foreach (var texture in textures.Values) Raylib.UnloadTexture(texture);
        textures.Clear();
        archive?.Dispose();
    }
}
