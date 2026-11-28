namespace Joey.LookAndFeel;

/// <summary>Approximate V24 scratch renderer, using the original 50x50
/// icon as a blue-channel mask over the original scrolling background.
/// 004ED5E0 extracts the RGB565 low five bits for the native remap.
/// Linear alpha expansion here is intentional, not native pixel parity.
/// Precompute only the 32 possible Update phases, no texture churn.</summary>
public static class NativeAffectPixels
{
    public const int Width=50,Height=50,LoopWidth=100,LoopHeight=69,Phases=32;
    public static byte[] BuildAtlas(ReadOnlySpan<byte> background,ReadOnlySpan<byte> mask)
    {
        if(background.Length!=LoopWidth*LoopHeight*4 || mask.Length!=Width*Height*4)
            throw new InvalidDataException("Unexpected affect scratch dimensions.");
        byte[] atlas=new byte[Width*Height*Phases*4];
        for(int t=0;t<Phases;t++)
        for(int y=0;y<Height;y++)
        for(int x=0;x<Width;x++)
        {
            int src=((y+t)%LoopHeight*LoopWidth+x)*4;
            int m=(y*Width+x)*4;
            int dst=((t*Height+y)*Width+x)*4;
            background.Slice(src,3).CopyTo(atlas.AsSpan(dst,3));
            atlas[dst+3]=(byte)((mask[m+2]>>3)*255/31);
        }
        return atlas;
    }

    public static CardBounds Source(int phase) =>
        phase is >=0 and <Phases ? new(0,phase*Height,Width,Height)
            : throw new ArgumentOutOfRangeException(nameof(phase));
}
