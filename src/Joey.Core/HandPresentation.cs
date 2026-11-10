namespace Joey.Core;

// Native coordinate/smoothing helpers: 0045C590, 0045C600, 0045C8B0.
// Call frequency is chosen by the demo, not established as native frame timing.
public static class HandPresentation
{
    public const int CardWidth = 108, CardHeight = 153, HoverOffset = 32;
    public static int CenterX(int index, int count)
    {
        if (count < 1 || index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
        int span = Math.Min((CardWidth / 2 + 8) * count, 512) - CardWidth / 2;
        return 525 - span / 2 + (count > 1 ? span * index / (count - 1) : 0);
    }
    public static int Approach(int current, int target) => target + (current - target) * 3 / 4;
}
