using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;

// GPU resources are owned here; originals stay in the archive and are never written out.
sealed class DatGraphics : IDisposable
{
    public const string DuelBoard = @"j\duel\field\fie_normal.bmp";
    public const string DuelBack = @"card\card_ura.bmp";
    public const string AttackSword = @"j\duel\effect\atk_icon.bmp";
    public const string AttackSwordPartial = @"j\duel\effect\atk_icon2.bmp";
    public const string AttackImpact = @"j\duel\effect\atk_eff.yga";
    public const string SetAuraBlue = @"j\duel\effect\eff_set_b.bmp";
    public const string SetAuraYellow = @"j\duel\effect\eff_set_y.bmp";
    // Six original BMPs in the DAT. Native index order and side-color selection
    // still require inspecting Set constructor 0044EFD0.
    public static readonly string[] SetSprites =
    [
        SetAuraBlue, SetAuraYellow,
        @"j\duel\effect\eff_star0_b.bmp",
        @"j\duel\effect\eff_star1_b.bmp",
        @"j\duel\effect\eff_star0_y.bmp",
        @"j\duel\effect\eff_star1_y.bmp"
    ];
    public const string DuelDetailBackground = @"j\duel\detail\detail.bmp";
    // Native LP resources indexed in data.dat; the exact bar layer order and
    // font color remain provisional pending frame-paired visual comparison.
    public const string LifeBackgroundNear = @"j\duel\life\lp_jibu1.bmp";
    public const string LifeBackgroundFar = @"j\duel\life\lp_aite1.bmp";
    public const string LifeFillNear = @"j\duel\life\lp_jibu2.bmp";
    public const string LifeFillFar = @"j\duel\life\lp_aite2.bmp";
    public const string LifeNumberFont = @"j\duel\font\duel_fontm_y.bmp";
    public const string LifeNumberManifest = @"j\duel\font\number.txt";
    public static readonly string[] LifeSprites =
    [
        LifeBackgroundNear, LifeBackgroundFar, LifeFillNear, LifeFillFar, LifeNumberFont
    ];
    public const string SpecialSummonManifest = @"j\duel\effect\sp_summon.txt";
    public static readonly string[] SpecialSummonAttackSprites =
        Enumerable.Range(1, NativeSpecialSummonFrame.SpiralFrameCount)
            .Select(frame => $@"j\duel\effect\sp_sum_a{frame}.yga").ToArray();
    public static readonly string[] SpecialSummonDefenseSprites =
        Enumerable.Range(1, NativeSpecialSummonFrame.SpiralFrameCount)
            .Select(frame => $@"j\duel\effect\sp_sum_d{frame}.yga").ToArray();
    private const string ExplosionManifest = @"j\duel\effect\explosion.txt";
    public static readonly string[] ExplosionSprites = Enumerable.Range(1, 7)
        .Select(index => $@"j\duel\effect\expl{index}.yga").ToArray();
    private static readonly string[] PhaseCodes = ["dr", "st", "m1", "ba", "m2", "en"];
    public static readonly string[] PhaseButtons = PhaseButtonImages('s');
    public static readonly string[] PhaseFlipToBlue = PhaseButtonImages('b');
    public static readonly string[] PhaseFlipToRed = PhaseButtonImages('r');
    public static readonly string[] PhaseBannersBlue = PhaseBannerImages('b');
    public static readonly string[] PhaseBannersRed = PhaseBannerImages('r');
    public static readonly string[] BattleBannersBlue = BattleBannerImages('b');
    public static readonly string[] BattleBannersRed = BattleBannerImages('r');

    private static string[] BattleBannerImages(char side) => Enumerable.Range(1, NativePhaseNotice.BattleFrameCount)
        .Select(frame => $@"j\duel\phase_name\e_ba_{side}{frame}.bmp").ToArray();

    private static string[] PhaseBannerImages(char side) => PhaseCodes
        .Select(phase => $@"j\duel\phase_name\e_{phase}_{side}.bmp").ToArray();

    private static bool IsPhaseBanner(string name) =>
        PhaseBannersBlue.Contains(name, StringComparer.OrdinalIgnoreCase) ||
        PhaseBannersRed.Contains(name, StringComparer.OrdinalIgnoreCase) ||
        BattleBannersBlue.Contains(name, StringComparer.OrdinalIgnoreCase) ||
        BattleBannersRed.Contains(name, StringComparer.OrdinalIgnoreCase);

    private static string[] PhaseButtonImages(char variant) => PhaseCodes
        .Select(phase => $@"j\duel\phase\pha_{variant}_{phase}.bmp").ToArray();

    readonly Dictionary<string, Texture2D> textures = new(StringComparer.OrdinalIgnoreCase);
    readonly Queue<string> loaded = new();
    Archive? archive;
    public MenuResources? Menu { get; private set; }
    public CardListResources CardList { get; private set; } = null!;
    public CardArtwork Artwork { get; private set; } = null!;
    public int? LifeNumberGlyphColumns { get; private set; }
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
        string fonts = System.Text.Encoding.Latin1.GetString(archive.Read(LifeNumberManifest));
        LifeNumberGlyphColumns = NativeLifeHud.TryFontColumns(fonts, "duel_fontm_y.bmp");
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
            name.Equals(AttackSwordPartial, StringComparison.OrdinalIgnoreCase) ||
            IsPhaseBanner(name) || SetSprites.Contains(name, StringComparer.OrdinalIgnoreCase) ||
            name.Equals(LifeNumberFont, StringComparison.OrdinalIgnoreCase))
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
        names = names.Concat(PhaseBannersBlue).Concat(PhaseBannersRed).Concat(BattleBannersBlue).Concat(BattleBannersRed)
            .Concat(PhaseButtons).Concat(PhaseFlipToBlue).Concat(PhaseFlipToRed)
            .Concat(SetSprites).Concat(LifeSprites)
            .Append(DuelDetailBackground).Append(AttackSword).Append(AttackSwordPartial)
            .Distinct(StringComparer.OrdinalIgnoreCase);
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
                if ((name is AttackSword or AttackSwordPartial || IsPhaseBanner(name)) && Raylib.GetImageColor(image, 0, 0).A != 0)
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
        _ = NativeYgaSprite.Decode(archive.Read(AttackImpact), static (data, size) => Archive.Decode(data, size));
        count++;
        foreach (string name in SpecialSummonAttackSprites.Concat(SpecialSummonDefenseSprites))
        {
            var spiral = NativeYgaSprite.Decode(archive.Read(name),
                static (data, size) => Archive.Decode(data, size));
            if (spiral.Width <= 0 || spiral.Height <= 0)
                throw new InvalidDataException($"Invalid special-summon YGA: {name}");
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
