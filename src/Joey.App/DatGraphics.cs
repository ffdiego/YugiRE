using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;

// GPU resources are owned here; originals stay in the archive and are never written out.
sealed class DatGraphics : IDisposable
{
    public const string DuelBoard = @"j\duel\field\fie_normal.bmp";
    public const string DuelBack = @"card\card_ura.bmp";
    public const string MiniCardBack = @"mini\card_ura.bmp";
    public const string AttackSword = @"j\duel\effect\atk_icon.bmp";
    public const string AttackSwordPartial = @"j\duel\effect\atk_icon2.bmp";
    public const string AttackImpact = @"j\duel\effect\atk_eff.yga";
    public const string SetAuraBlue = @"j\duel\effect\eff_set_b.bmp";
    public const string SetAuraYellow = @"j\duel\effect\eff_set_y.bmp";
    // Native manifest order, confirmed in SQLite v3.
    public static readonly string[] SetSprites = NativePlacementAssets.SetBank();
    public const string DuelDetailBackground = @"j\duel\detail\detail.bmp";
    public const string DuelDetailManifest = @"j\duel\detail\detail.txt";
    public const string DuelDetailLine = @"j\duel\detail\line.bmp";
    public const string DuelDetailScrollbar = @"j\duel\detail\scroll_bar.bmp";
    public static readonly string[] CursorActionSprites =
        Enum.GetValues<NativeCursorAction>()
            .Select(NativeDuelCursor.ActionSprite).ToArray();
    public const string ShuffleFrameEnglish = @"j\duel\effect\deck_shuf_waku_e.yga";
    public const string ShuffleManifest = @"j\duel\effect\shuffle.txt";
    public static readonly string[] ShuffleSprites = Enumerable.Range(1, 5)
        .Select(i => $@"j\duel\effect\deck_shuf{i}.bmp").ToArray();
    public const string DuelStartEnglish = @"j\duel\effect\duel_start_e.bmp";
    public const string DuelWinEnglish = @"j\duel\effect\duel_win_e.yga";
    public const string DuelLoseEnglish = @"j\duel\effect\duel_lose_e.yga";
    public const string DuelDrawEnglish = @"j\duel\effect\duel_draw_e.yga";
    public static readonly string[] DuelResultsEnglish = [DuelWinEnglish, DuelLoseEnglish, DuelDrawEnglish];
    // Native LP resources indexed in data.dat; the exact bar layer order and
    // font color remain provisional pending frame-paired visual comparison.
    public const string LifeBackgroundNear = @"j\duel\life\lp_jibu1.bmp";
    public const string LifeBackgroundFar = @"j\duel\life\lp_aite1.bmp";
    public const string LifeFillNear = @"j\duel\life\lp_jibu2.bmp";
    public const string LifeFillFar = @"j\duel\life\lp_aite2.bmp";
    // The large LP labels in native screenshots use the large-number
    // atlas; the previously selected medium-yellow atlas was too small.
    // Keep the latter only as a manifest-validated fallback.
    public const string LifeNumberFont = @"j\duel\font\duel_fontl.bmp";
    // 0041A070 / 0041A3B0 select resource #8 from number.txt.
    // The same yellow 10-column bitmap was already validated/loaded
    // as the LP fallback; naming it here does not enable status Draws.
    public const string CardStatusNumberFont = NativeCardStatusDigits.FontResource;
    public const string LifeNumberFallback = CardStatusNumberFont;
    public static readonly string[] LifeFloatingFonts =
    [
        @"j\duel\font\damage00.bmp", @"j\duel\font\damage01.bmp"
    ];
    // 004194D0 selects these three original medium-number atlas bitmaps.
    // Manifest columns are 11 for normal (including slash), 10 for color variants.
    public static readonly string[] FieldAttributeFonts =
    [
        NativeFieldAttributeFonts.Normal,
        NativeFieldAttributeFonts.Increased,
        NativeFieldAttributeFonts.Decreased
    ];
    public const string LifeNumberManifest = @"j\duel\font\number.txt";
    public static readonly string[] LifeSprites =
    [
        LifeBackgroundNear, LifeBackgroundFar, LifeFillNear, LifeFillFar,
        LifeNumberFont, LifeNumberFallback
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
    public string LifeNumberSprite { get; private set; } = LifeNumberFont;
    public int?[] LifeFloatingGlyphColumns { get; } = new int?[2];
    public void LoadArtwork(List<Card> cards) => Artwork = CardArtwork.Load(archive!, cards);

    readonly Dictionary<string, NativeSelectionManifest?> selectionManifests =
        new(StringComparer.OrdinalIgnoreCase);

    public NativeSelectionManifest? SelectionManifest(int mode, int side, int perspective)
    {
        string path = NativeSelectionWindow.ManifestPath(mode,
            NativeSelectionManifest.SideVariant(side, perspective));
        if (selectionManifests.TryGetValue(path, out var cached)) return cached;
        try
        {
            var source = archive ?? throw new InvalidOperationException("No DAT open");
            var manifest = NativeSelectionManifest.Parse(
                System.Text.Encoding.Latin1.GetString(source.Read(path)),
                source.Entries.Select(entry => entry.Name));
            // Validate the actual two DAT images before replacing the legacy
            // UI. No guessed names, sizes, or dimensions enter the layout.
            foreach (var background in new[] { manifest.WithoutScroll, manifest.WithScroll })
            {
                var info = BitmapInfo.Read(source.Read(background.Resource));
                if (info.Width <= 0 || info.Height <= 0)
                    throw new InvalidDataException("Invalid selection background: " + background.Resource);
            }
            selectionManifests.Add(path, manifest);
            return manifest;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or KeyNotFoundException or OverflowException)
        {
            Console.Error.WriteLine("[selection] legacy fallback for " + path + ": " + ex.Message);
            selectionManifests.Add(path, null);
            return null;
        }
    }
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
        LifeNumberSprite = LifeNumberFont;
        LifeNumberGlyphColumns = NativeLifeHud.TryFontColumns(fonts, "duel_fontl.bmp");
        if (LifeNumberGlyphColumns is null)
        {
            LifeNumberSprite = LifeNumberFallback;
            LifeNumberGlyphColumns = NativeLifeHud.TryFontColumns(fonts, "duel_fontm_y.bmp");
        }
        for (int i = 0; i < LifeFloatingFonts.Length; i++)
            LifeFloatingGlyphColumns[i] = NativeLifeHud.TryFontColumns(
                fonts, System.IO.Path.GetFileName(LifeFloatingFonts[i].Replace('\\', '/')));
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
                static (data, size) => Archive.Decode(data, size),
                NativeDuelCursor.UsesSourceAlpha(name)
                    ? YgaTransparencyMode.SourceAlpha
                    : YgaTransparencyMode.BlackColorKey);
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
        if (NativeCardSelectionFrame.IsFrame(name) ||
            name is CardListResources.Frame or CardListResources.Left or CardListResources.Right or CardListResources.Digits ||
            name.Equals(AttackSword, StringComparison.OrdinalIgnoreCase) ||
            name.Equals(AttackSwordPartial, StringComparison.OrdinalIgnoreCase) ||
            NativeExclusionResources.IsParticleBitmap(name) || // diagnostic keying
            name.StartsWith(NativeDisabledChainResources.Prefix, StringComparison.OrdinalIgnoreCase) || // diagnostic green key for V23 BMPs
            NativeCoinResources.IsCoinFaceBitmap(name) || // diagnostic green key; native compositor pending
            NativeDiceResources.IsDiceBitmap(name) || // diagnostic green-key path; native blend pending
            name.Equals(NativeBreakResources.Shine, StringComparison.OrdinalIgnoreCase) || // V22 review; native +AC pending
            IsPhaseBanner(name) || SetSprites.Contains(name, StringComparer.OrdinalIgnoreCase) ||
            name.Equals(LifeNumberFont, StringComparison.OrdinalIgnoreCase) ||
            name.Equals(LifeNumberFallback, StringComparison.OrdinalIgnoreCase) ||
            LifeFloatingFonts.Contains(name, StringComparer.OrdinalIgnoreCase) ||
            FieldAttributeFonts.Contains(name, StringComparer.OrdinalIgnoreCase) ||
            name.Equals(DuelStartEnglish, StringComparison.OrdinalIgnoreCase) ||
            ShuffleSprites.Contains(name, StringComparer.OrdinalIgnoreCase))
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
            .Concat(SetSprites).Append(NativePlacementAssets.Rainbow).Concat(LifeSprites).Concat(LifeFloatingFonts)
            .Concat(FieldAttributeFonts).Concat(ShuffleSprites)
            .Append(DuelDetailBackground).Append(DuelDetailLine)
            .Append(DuelDetailScrollbar).Append(DuelStartEnglish)
            .Append(AttackSword).Append(AttackSwordPartial)
            .Append(DuelBack).Append(MiniCardBack)
            .Concat(archive.Entries.Where(e => e.Name.StartsWith(@"mini\", StringComparison.OrdinalIgnoreCase) &&
                e.Name.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)).Select(e => e.Name))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        int count = 0;
        foreach (string name in names)
        {
            var image = ReadImage(name);
            try
            {
                if (name.Equals(MiniCardBack, StringComparison.OrdinalIgnoreCase) &&
                    (image.Width != DuelCardLayout.MiniWidth || image.Height != DuelCardLayout.MiniHeight))
                    throw new InvalidDataException($"Unexpected native miniature back dimensions: {name}");
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
        foreach (string name in CursorActionSprites.Append(NativeDuelCursor.DefaultPointer)
                     .Append(NativeDuelCursor.AlternatePointer))
        {
            var sprite = NativeYgaSprite.Decode(archive.Read(name),
                static (data, size) => Archive.Decode(data, size),
                YgaTransparencyMode.SourceAlpha);
            if (sprite.Width <= 0 || sprite.Height <= 0)
                throw new InvalidDataException($"Invalid cursor YGA: {name}");
            count++;
        }
        foreach (string name in SpecialSummonAttackSprites.Concat(SpecialSummonDefenseSprites)
                     .Concat(DuelResultsEnglish).Append(ShuffleFrameEnglish))
        {
            var spiral = NativeYgaSprite.Decode(archive.Read(name),
                static (data, size) => Archive.Decode(data, size));
            if (spiral.Width <= 0 || spiral.Height <= 0)
                throw new InvalidDataException($"Invalid effect YGA: {name}");
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
