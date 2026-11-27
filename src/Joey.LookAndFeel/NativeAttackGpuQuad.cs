using System.Numerics;

namespace Joey.LookAndFeel;

/// <summary>Adapter from verified integer inclusive corner centers to
/// the client's textured GPU quad. Native polygon rasterization is NOT
/// reproduced by this adapter: pixel cells and interpolation are a
/// display approximation until the packed compositor is ported.</summary>
public readonly record struct NativeAttackGpuQuad(
    Vector2 TopLeft, Vector2 TopRight, Vector2 BottomRight, Vector2 BottomLeft)
{
    public static NativeAttackGpuQuad? FromProjection(NativeQuadProjection projection)
    {
        if (projection.Degenerate) return null;
        var d = projection.Destination;
        static Vector2 Center(CardPoint point) => new(point.X + 0.5f, point.Y + 0.5f);
        static Vector2 Outer(Vector2 corner, Vector2 adjacentA, Vector2 adjacentB) =>
            corner + 0.5f * (Vector2.Normalize(corner - adjacentA) +
                              Vector2.Normalize(corner - adjacentB));
        var tl = Center(d.TopLeft);
        var tr = Center(d.TopRight);
        var br = Center(d.BottomRight);
        var bl = Center(d.BottomLeft);
        // For axis-aligned geometry this maps the inclusive pixel endpoints
        // to exactly their enclosing half-open pixel rectangle. For rotated
        // geometry the GPU still interpolates, so this is not a raster claim.
        // Leave out-of-canvas coordinates intact for viewport clipping.
        return new(Outer(tl, tr, bl), Outer(tr, tl, br),
            Outer(br, tr, bl), Outer(bl, tl, br));
    }
}
