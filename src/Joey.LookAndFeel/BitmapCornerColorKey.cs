namespace Joey.LookAndFeel;

/// <summary>Diagnostic BMP color-key conversion. The caller opts in when
/// drawing a keyed surface; opaque backgrounds and source-alpha YGAs must
/// not use this operation. The key comes from decoded pixel (0,0).</summary>
public static class BitmapCornerColorKey
{
    public static void Prepare(Span<byte> rgba)
    {
        if(rgba.Length<4 || rgba.Length%4!=0)
            throw new ArgumentException("Expected nonempty RGBA8 pixels.",nameof(rgba));
        byte red=rgba[0],green=rgba[1],blue=rgba[2];
        for(int i=0;i<rgba.Length;i+=4)
            if(rgba[i]==red && rgba[i+1]==green && rgba[i+2]==blue)
                rgba[i+3]=0;
    }
}
