using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;

// GPU resources are owned here; originals stay in the archive and are never written out.
sealed class DatGraphics : IDisposable
{
    public const string DuelBoard = @"j\duel\field\fie_normal.bmp";
    public const string DuelBack = @"card\card_ura.bmp";
    public const string AttackSword = @"j\duel\effect\atk_icon.bmp";
    public const string DuelDetailBackground = @"j\duel\detail\detail.bmp";
    private const string ExplosionManifest = @"j\duel\effect\explosion.txt";
    public static readonly string[] ExplosionSprites = Enumerable.Range(1, 7)
        .Select(index => $@"j\duel\effect\expl{index}.yga").ToArray();
    private static readonly string[] PhaseCodes = ["dr", "st", "m1", "ba", "m2", "en"];
    public static readonly string[] PhaseButtons = PhaseButtonImages('s');
    public static readonly string[] PhaseFlipToBlue = PhaseButtonImages('b');
    public static readonly string[] PhaseFlipToRed = PhaseButtonImages('r');
    public static readonly string[] PhaseBanners = PhaseCodes
        .Select(phase => $@"j\duel\phase_name\e_{phase}_b.bmp").ToArray();

    private static string[] PhaseButtonImages(char variant) => PhaseCodes
        .Select(phase => $@"j\duel\phase\pha_{variant}_{phase}.bmp").ToArray();

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
        Texture2D texture;
        if (name.EndsWith(".yga", StringComparison.OrdinalIgnoreCase))
        {
            var sprite = NativeYgaSprite.Decode(
                (archive ?? throw new InvalidOperationException("No DAT open")).Read(name),
                static (data, size) => Archive.Decode(data, size));
            texture = UploadYga(sprite);
        }
        else
        {
            var image = ReadImage(name);
            try { texture = Raylib.LoadTextureFromImage(image); }
            finally { Raylib.UnloadImage(image); }
        }
        if (texture.Id == 0) throw new InvalidDataException($"Cannot upload texture {name}");
        Raylib.SetTextureFilter(texture, TextureFilter.Point);
        textures.Add(name, texture);
        loaded.Enqueue(name);
        return texture;
    }
    private static unsafe Texture2D UploadYga(NativeYgaSprite sprite)
    {
        fixed (byte* pixels = sprite.Rgba)
        {
            var image = new Image
            {
                Data = pixels, Width = sprite.Width, Height = sprite.Height,
                Mipmaps = 1, Format = PixelFormat.UncompressedR8G8B8A8
            };
            return Raylib.LoadTextureFromImage(image);
        }
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
        if (name is CardListResources.Frame or CardListResources.Left or CardListResources.Right or CardListResources.Digits ||
            name.Equals(AttackSword, StringComparison.OrdinalIgnoreCase) ||
            PhaseBanners.Contains(name, StringComparer.OrdinalIgnoreCase))
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
            CardListResources.All, CardListResources.AllSelected, CardListResources.Joey, CardListResources.JoeySelected,
            @"j\list\list_bg.bmp", @"j\list\e_cardlistmoji2.bmp", @"j\list\e_back1.bmp", @"j\list\e_back2.bmp", @"j\duel\field\fie_normal.bmp" }
            .Concat(archive!.Entries.Where(e => e.Name.StartsWith(@"card\", StringComparison.OrdinalIgnoreCase) && e.Name.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)).Select(e => e.Name));
        names = names.Concat(PhaseBanners).Concat(PhaseButtons).Concat(PhaseFlipToBlue).Concat(PhaseFlipToRed)
            .Append(DuelDetailBackground).Append(AttackSword).Distinct(StringComparer.OrdinalIgnoreCase);
        int count = 0;
        foreach (string name in names)
        {
            var image = ReadImage(name);
            try
            {
                if (PhaseButtons.Contains(name) && (image.Width != 80 || image.Height != 150))
                    throw new InvalidDataException($"Unexpected phase button atlas: {name}");
                if ((PhaseFlipToBlue.Contains(name) || PhaseFlipToRed.Contains(name)) &&
                    (image.Width != DuelSurfaceLayout.PhaseWidth * NativePhaseTurnFlip.FrameCount ||
                     image.Height != DuelSurfaceLayout.PhaseHeight))
                    throw new InvalidDataException($"Unexpected phase turn atlas: {name}");
                if (name == CardListResources.Frame && Raylib.GetImageColor(image, 20, 20).A != 0)
                    throw new InvalidDataException("Card selection frame color key is not transparent");
                if ((name == AttackSword || PhaseBanners.Contains(name)) && Raylib.GetImageColor(image, 0, 0).A != 0)
                    throw new InvalidDataException($"Duel sprite color key is not transparent: {name}");
            }
            finally { Raylib.UnloadImage(image); }
            count++;
        }
        // Also validate actual original activation sprites, with no GPU/window
        // required. The paths and 164px dimensions come from visual RE.
        foreach (string name in ActivationSpriteNames.All)
        {
            var sprite = NativeYgaSprite.Decode(archive.Read(name),
                static (data, size) => Archive.Decode(data, size));
            if (sprite.Width != 164 || sprite.Height != 164)
                throw new InvalidDataException($"Activation YGA dimensions differ: {name}");
            count++;
        }
        var explosionEntries = System.Text.Encoding.ASCII.GetString(archive.Read(ExplosionManifest))
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!explosionEntries.Select(name => @"j\duel\effect\" + name)
            .SequenceEqual(ExplosionSprites, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("Unexpected explosion manifest order");
        int[] explosionSizes = [164, 90, 114, 140, 160, 162, 196];
        for (int i = 0; i < ExplosionSprites.Length; i++)
        {
            var sprite = NativeYgaSprite.Decode(archive.Read(ExplosionSprites[i]),
                static (data, size) => Archive.Decode(data, size));
            if (sprite.Width != explosionSizes[i] || sprite.Height != explosionSizes[i])
                throw new InvalidDataException($"Unexpected explosion YGA dimensions: {ExplosionSprites[i]}");
            count++;
        }
        Console.WriteLine($"Decoded {count} BMP/YGA resources, including activation and explosion sprites; {Artwork.Count} card mappings.");
    }
    public void Dispose()
    {
        foreach (var texture in textures.Values) Raylib.UnloadTexture(texture);
        textures.Clear();
        archive?.Dispose();
    }
}
