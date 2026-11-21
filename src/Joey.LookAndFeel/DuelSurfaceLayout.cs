namespace Joey.LookAndFeel;

public static class DuelSurfaceLayout
{
    public const int SidebarWidth = 230;
    public const int PhaseCount = 6;
    public const int PhaseWidth = 40, PhaseHeight = 75;

    // phase.txt packs the coordinates as X * 1000 + Y (0048169C–004816BD).
    public static CardBounds PhaseButton(int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)PhaseCount);
        return new(221, 82 + index * PhaseHeight, PhaseWidth, PhaseHeight);
    }

    public static CardBounds Board => new(SidebarWidth, 0,
        NativeDuelLayout.CanvasWidth - SidebarWidth, NativeDuelLayout.CanvasHeight);
}
