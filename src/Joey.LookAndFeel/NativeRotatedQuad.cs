namespace Joey.LookAndFeel;

/// <summary>Integer destination corners for the shared native rotated blit
/// 00422840 / 00422C00. Source bounds are half-open; the returned corners
/// are inclusive. The dimensions supplied by virtual +38 are independent
/// of the stored source rectangle (+18..+24). No pixel blending here.</summary>
public static class NativeRotatedQuad
{
    /// <summary>Positive scale subset used by the attack sprite passes.
    /// Null means the native zero-scale/zero-extent early return.
    /// Negative scales and x86 overflow are outside this adapter's domain.</summary>
    public static NativeQuadProjection? Project(CardPoint center, CardBounds source,
        int width, int height, int angleIndex, int scaleX, int scaleY,
        ReadOnlySpan<int> cosine)
    {
        NativeFusionMath.Validate(cosine);
        if (width is < 1 or > 16384 || height is < 1 or > 16384)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (source.Width < 1 || source.Height < 1)
            throw new ArgumentOutOfRangeException(nameof(source));
        ArgumentOutOfRangeException.ThrowIfNegative(scaleX);
        ArgumentOutOfRangeException.ThrowIfNegative(scaleY);
        if (scaleX == 0 || scaleY == 0) return null;
        long w = ((long)(width-1)*scaleX+32768)/65536;
        long h = ((long)(height-1)*scaleY+32768)/65536;
        if (w == 0 || h == 0) return null;
        if (w > 16384 || h > 16384)
            throw new ArgumentOutOfRangeException(nameof(scaleX));
        int c = NativeFusionMath.C(cosine,angleIndex);
        int s = NativeFusionMath.C(cosine,unchecked(angleIndex-128));
        if (c is < -65536 or > 65536 || s is < -65536 or > 65536)
            throw new InvalidDataException("Outside native cosine range.");

        // Native MulDiv rounds positive products to nearest; the rotation
        // itself uses SAR 16 (floor, not truncation toward zero).
        long left = -w/2, right = (w+1)/2;
        long top = -h/2, bottom = (h+1)/2;
        CardPoint Rotate(long x,long y) => new(
            checked(center.X+(int)((x*c-y*s)>>16)),
            checked(center.Y+(int)((y*c+x*s)>>16)));
        var src = new NativeQuad(new(source.X,source.Y),
            new(checked(source.X+source.Width-1),source.Y),
            new(checked(source.X+source.Width-1),checked(source.Y+source.Height-1)),
            new(source.X,checked(source.Y+source.Height-1)));
        var dst = new NativeQuad(Rotate(left,top),Rotate(right,top),
            Rotate(right,bottom),Rotate(left,bottom));
        // Quantized corners can collapse even when native extents are nonzero.
        // Signal this to callers without inventing a minimum size.
        long Cross(CardPoint a,CardPoint b,CardPoint d) =>
            ((long)b.X-a.X)*((long)d.Y-b.Y)-((long)b.Y-a.Y)*((long)d.X-b.X);
        bool degenerate = Cross(dst.TopLeft,dst.TopRight,dst.BottomRight)<=0 ||
            Cross(dst.TopRight,dst.BottomRight,dst.BottomLeft)<=0 ||
            Cross(dst.BottomRight,dst.BottomLeft,dst.TopLeft)<=0 ||
            Cross(dst.BottomLeft,dst.TopLeft,dst.TopRight)<=0;
        return new NativeQuadProjection(src,dst,degenerate);
    }
}
