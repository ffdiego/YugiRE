namespace Joey.LookAndFeel;

/// <summary>The normal attack flight and contact samples of 004142C0, state 4.</summary>
public readonly record struct NativeAttackFrame(
    CardPoint Position, int RotationIndex, bool PartialSword, int ImpactScaleFixed16, int Opacity)
{
    public const int UpdateIncrement = 16;
    public const int Updates = 0x200 / UpdateIncrement;

    public static CardPoint DirectTarget(bool nearSide) => new(525, nearSide ? 64 : 536);

    public static NativeAttackFrame Sample(int update, CardPoint origin, CardPoint target,
        bool nearSide, ReadOnlySpan<int> cosineFixed16)
    {
        if (update is < 0 or >= Updates) throw new ArgumentOutOfRangeException(nameof(update));
        if (cosineFixed16.Length != 512)
            throw new ArgumentException("Expected 512 x16.16 cosine values.", nameof(cosineFixed16));
        int t = update * UpdateIncrement;
        int dx = target.X - origin.X, dy = target.Y - origin.Y;
        int start = nearSide ? 0 : 256;
        int turn = nearSide
            ? (int)(Math.Atan2(dx, -dy) * (256 / Math.PI))
            : (int)(Math.Atan2(-dx, dy) * (256 / Math.PI));

        if (t <= 0x80)
            return new(origin, start + turn * t / 0x80, false, 0, 255);
        if (t < 0x100)
        {
            int weight = cosineFixed16[(t - 0x80) & 511] >> 8;
            var point = new CardPoint((origin.X * weight + target.X * (256 - weight)) / 256,
                (origin.Y * weight + target.Y * (256 - weight)) / 256);
            return new(point, start + turn, false, 0, 255);
        }
        return new(target, start + turn, true, (t - 0x100) << 9, Math.Min(255, 2 * (0x200 - t)));
    }
}
