namespace Joey.LookAndFeel;

/// <summary>Four corresponding corners in source order TL, TR, BR, BL.
/// Pixel endpoints are inclusive, not rectangle widths or normalized UVs.</summary>
public readonly record struct NativeQuad(CardPoint TopLeft, CardPoint TopRight,
    CardPoint BottomRight, CardPoint BottomLeft);

public readonly record struct NativeQuadProjection(NativeQuad Source, NativeQuad Destination,
    bool Degenerate);

/// <summary>Candidate geometry from 004223F0 decompilation; caller
/// 00480C60 assembly confirms the signed angle and center. The callee
/// register-to-dimension mapping needs assembly confirmation (recipe 25).
/// This is not a rasterizer and must not silently drive faithful rendering.</summary>
public static class NativeNoticeProjection
{
    public static NativeQuadProjection Project(NativeTurnNoticeDraw draw, int width,
        int height, ReadOnlySpan<int> cosine)
    {
        NativeFusionMath.Validate(cosine);
        if (width is < 1 or > 32767 || height is < 1 or > 32767)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (draw.AngleIndex is < -128 or > 128)
            throw new ArgumentOutOfRangeException(nameof(draw));
        int c = NativeFusionMath.C(cosine, draw.AngleIndex);
        int s = NativeFusionMath.C(cosine, draw.AngleIndex-128);
        if (c is < 0 or > 65536 || s is < -65536 or > 65536)
            throw new InvalidDataException("Outside native notice cosine range.");

        // The signed correction in 004223F0 implements truncation toward
        // zero. In this caller's angle interval the product is nonnegative.
        int w = (int)(((long)c * width) >> 16);
        int rightHeight = MulDivPositive(height, 65536, 65536+s/2);
        int leftHeight = MulDivPositive(height, 65536, 65536-s/2);
        int left = checked(draw.Center.X-w/2);
        int right = checked(draw.Center.X+w-w/2-1);
        int lt = checked(draw.Center.Y-leftHeight/2);
        int rt = checked(draw.Center.Y-rightHeight/2);
        int lb = checked(lt+leftHeight-1);
        int rb = checked(rt+rightHeight-1);
        return new(
            new(new(0,0),new(width-1,0),new(width-1,height-1),new(0,height-1)),
            new(new(left,lt),new(right,rt),new(right,rb),new(left,lb)),
            w <= 1 || leftHeight <= 1 || rightHeight <= 1);
    }

    // Win32 MulDiv rounds to nearest, unlike a truncating integer quotient.
    private static int MulDivPositive(int n,int m,int d) =>
        checked((int)(((long)n*m+d/2)/d));
}

/// <summary>00422E30 dispatch boundary only. Values 0..255 use the
/// format-specific intensity family; >=256 calls 0051EBD0. No claim
/// that intensity is source alpha, or that opaque means no color key.</summary>
public enum NativeQuadRasterRoute { IntensityFamily, BaseFamily }

public static class NativeQuadRasterDispatch
{
    public static NativeQuadRasterRoute Route(int intensity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(intensity);
        return intensity >= 256 ? NativeQuadRasterRoute.BaseFamily :
            NativeQuadRasterRoute.IntensityFamily;
    }
}
