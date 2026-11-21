namespace Joey.LookAndFeel;

/// <summary>Position interpolation at 00418B60. Sizes, elapsed time and the cosine implementation are client policy.</summary>
public static class CardTravel
{
    public const int Updates = 8;
    private const int CounterStep = 0x20;
    private const int FixedOne = 0x10000;

    public static CardBounds Sample(CardBounds origin, CardBounds destination, int update)
    {
        if (update is < 0 or > Updates) throw new ArgumentOutOfRangeException(nameof(update));
        int index = (update * CounterStep / 2 - 0x80) & 0x1ff;
        int weight = (int)(Math.Cos(index * (Math.PI / 256)) * FixedOne);
        int Axis(int a, int b) => a + ((b - a) * weight >> 16);
        int width = Axis(origin.Width, destination.Width), height = Axis(origin.Height, destination.Height);
        int centerX = Axis(origin.X + origin.Width / 2, destination.X + destination.Width / 2);
        int centerY = Axis(origin.Y + origin.Height / 2, destination.Y + destination.Height / 2);
        return new(centerX - width / 2, centerY - height / 2, width, height);
    }
}
