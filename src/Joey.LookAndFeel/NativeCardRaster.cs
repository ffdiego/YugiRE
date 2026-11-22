namespace Joey.LookAndFeel;

/// <summary>
/// Project card artwork onto integer-sized destination pixels; keeps source
/// textures and point filtering unchanged. Does not assert native mip parity.
/// </summary>
public static class NativeCardRaster
{
    public static (int Width, int Height) Project(
        int sourceWidth, int sourceHeight, int targetWidth, int targetHeight,
        int degrees)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 ||
            targetWidth <= 0 || targetHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceWidth));
        if (degrees % 90 != 0)
            throw new ArgumentOutOfRangeException(nameof(degrees));
        bool sideways = degrees % 180 != 0;
        int availableWidth = sideways ? targetHeight : targetWidth;
        int availableHeight = sideways ? targetWidth : targetHeight;
        float scale = Math.Min(
            availableWidth / (float)sourceWidth,
            availableHeight / (float)sourceHeight);
        return (
            Math.Clamp((int)MathF.Round(sourceWidth * scale), 1, availableWidth),
            Math.Clamp((int)MathF.Round(sourceHeight * scale), 1, availableHeight));
    }
}
