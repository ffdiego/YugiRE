namespace Joey.LookAndFeel;

/// <summary>The normal attack flight and contact samples of 004142C0, state 4.</summary>
public readonly record struct NativeAttackFrame(
    CardPoint Position, int RotationIndex, bool PartialSword, int ImpactScaleFixed16, int Opacity)
{
    public const int UpdateIncrement = 16;
    public const int Updates = 0x200 / UpdateIncrement;
    // The sword enters its contact/impact branch when counter t >= 0x100.
    public const int ImpactStartUpdate = 0x100 / UpdateIncrement;

    public static CardPoint DirectTarget(bool nearSide) => new(525, nearSide ? 64 : 536);

    // 00414317 and 00414907 both resolve the source through 0045C140.
    public static CardPoint SwordAnchor(int source, bool nearSide) => NativeDuelLayout.MonsterCenter(source, nearSide);

    public static int AimRotation(float dx, float dy) => (int)(Math.Atan2(dx, -dy) * (256 / Math.PI));

    public static NativeAttackFrame Sample(int update, CardPoint origin, CardPoint target,
        bool nearSide, ReadOnlySpan<int> cosineFixed16, bool alreadyAimed = false)
    {
        if (update is < 0 or >= Updates) throw new ArgumentOutOfRangeException(nameof(update));
        if (cosineFixed16.Length != 512)
            throw new ArgumentException("Expected 512 x16.16 cosine values.", nameof(cosineFixed16));
        int t = update * UpdateIncrement;
        int dx = target.X - origin.X, dy = target.Y - origin.Y;
        int start = nearSide ? 0 : 256;
        int turn = nearSide
            ? AimRotation(dx, dy)
            : AimRotation(-dx, -dy);

        if (t <= 0x80)
            return new(origin, start + (alreadyAimed ? turn : turn * t / 0x80), false, 0, 255);
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
