namespace Joey.LookAndFeel;

/// <summary>Stable selection backgrounds: sample (0,0) and compare in
/// RGB565, as observed for list_grave0 in Proton. The other named
/// backgrounds use their own observed DAT corner colors; their native
/// runtime compositor has not yet been individually confirmed.</summary>
public static class NativeSelectionBackgroundPixels
{
    private const string Prefix = @"j\duel\list\";
    private static readonly Dictionary<string, ushort> Keys = new(StringComparer.OrdinalIgnoreCase)
    {
        [Prefix+"list_grave0.bmp"] = 0x07e0,
        [Prefix+"list_grave1.bmp"] = 0x07e0,
        [Prefix+"list_fusion0.bmp"] = 0x07e0,
        [Prefix+"list_fusion1.bmp"] = 0x07e0,
        [Prefix+"list_jyoga_aite.bmp"] = 0x07e0,
        [Prefix+"list_jyoga_aite2.bmp"] = 0x07e0,
        [Prefix+"list_jyoga_jibu.bmp"] = 0x07e0,
        [Prefix+"list_jyoga_jibu2.bmp"] = 0x07e0,
        [Prefix+"list_select0.bmp"] = 0x07e6,
        [Prefix+"list_select1.bmp"] = 0x07e6
    };

    public static bool IsBackground(string resource) => Keys.ContainsKey(resource);

    /// <summary>Convert one original RGBA8 bitmap into a point-filtered,
    /// binary-alpha client texture. Only the selected resource's sampled
    /// packed key is transparent; do not remove every green pixel.</summary>
    public static void Prepare(string resource, Span<byte> rgba)
    {
        if (!Keys.TryGetValue(resource, out ushort expected))
            throw new ArgumentException("Unsupported selection background.", nameof(resource));
        if (rgba.Length < 4 || rgba.Length % 4 != 0)
            throw new ArgumentException("Expected nonempty RGBA8 pixels.", nameof(rgba));
        ushort key = NativeFieldAttributePixels.Pack(rgba[0],rgba[1],rgba[2]);
        if (key != expected)
            throw new InvalidDataException("Unexpected selection background corner: " + resource);
        for (int i=0; i<rgba.Length; i+=4)
        {
            ushort pixel=NativeFieldAttributePixels.Pack(rgba[i],rgba[i+1],rgba[i+2]);
            if (pixel==key)
            {
                rgba.Slice(i,4).Clear();
                continue;
            }
            int r=pixel>>11,g=(pixel>>5)&63,b=pixel&31;
            rgba[i]=(byte)((r<<3)|(r>>2));
            rgba[i+1]=(byte)((g<<2)|(g>>4));
            rgba[i+2]=(byte)((b<<3)|(b>>2));
            rgba[i+3]=255;
        }
    }
}
