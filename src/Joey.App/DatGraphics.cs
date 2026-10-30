using Joey.Core;
using Raylib_cs;

// GPU resources are owned here; originals stay in the archive and are never written out.
sealed class DatGraphics : IDisposable
{
    readonly Dictionary<string, Texture2D> textures = new(StringComparer.OrdinalIgnoreCase);
    Archive? archive;
    public MenuResources? Menu { get; private set; }
    public Archive Open(string path)
    {
        archive = new Archive(path);
        Menu = MenuResources.Load(archive);
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
        return image;
    }
    public void Validate()
    {
        foreach (string name in new[] { MenuResources.Background, MenuResources.Normal, MenuResources.Highlight })
        {
            var image = ReadImage(name);
            Console.WriteLine($"Decoded {name}: {image.Width}x{image.Height}");
            Raylib.UnloadImage(image);
        }
    }
    public void Dispose()
    {
        foreach (var texture in textures.Values) Raylib.UnloadTexture(texture);
        textures.Clear();
        archive?.Dispose();
    }
}
