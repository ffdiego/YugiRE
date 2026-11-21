namespace Joey.LookAndFeel;

/// <summary>
/// First, five-card phase of Exodia Draw 00454010. Native Update 00454000
/// increases the counter by four; the first Draw branch runs while t < 316.
/// The subsequent full Exodia assembly sequence is not reconstructed here.
/// </summary>
public readonly record struct NativeExodiaCardPose(
    int Sequence, CardPoint Center, int Opacity);

public readonly record struct NativeExodiaOpeningFrame(
    int Counter, IReadOnlyList<NativeExodiaCardPose> Cards)
{
    public const int UpdateIncrement = 4;
    public const int FirstStageEnd = 0x13C;
    public const int OpeningUpdates = FirstStageEnd / UpdateIncrement; // t=0..312 (79 samples)
    public const int CardCount = 5;
    public const int OrbitRadius = 200;
    public static readonly CardPoint NativeStageCenter = new(400, 300);

    /// <summary>
    /// 00454010 renders five miniature cards on a 512-step cosine ring.
    /// Angle i = floor(i*512/5). Each card fades from 0 to 255 over 16
    /// native counter units, delayed by 16 units per card. When invisible
    /// it also sits up to 64 pixels below its target ring position.
    /// </summary>
    public static NativeExodiaOpeningFrame FromUpdate(
        int update, ReadOnlySpan<int> cosineFixed16)
    {
        if (update is < 0 or >= OpeningUpdates)
            throw new ArgumentOutOfRangeException(nameof(update));
        if (cosineFixed16.Length != 512)
            throw new ArgumentException("Expected native 512-entry cosine x16.16 table.",
                nameof(cosineFixed16));

        int counter = update * UpdateIncrement;
        var cards = new NativeExodiaCardPose[CardCount];
        for (int i = 0; i < CardCount; i++)
        {
            int angle = i * 0x200 / CardCount;
            int cosine = cosineFixed16[angle];
            int sine = cosineFixed16[(angle + 0x80) & 511];

            int alpha = Math.Clamp(counter * 16 - i * 0x100, 0, 255);
            int x = NativeStageCenter.X + NativeTruncatedProduct(sine, OrbitRadius);
            int y = NativeStageCenter.Y - NativeTruncatedProduct(cosine, OrbitRadius)
                + (0x100 - alpha) / 4;
            cards[i] = new(i, new CardPoint(x, y), alpha);
        }
        return new(counter, cards);
    }

    private static int NativeTruncatedProduct(int valueFixed16, int pixels)
    {
        int product = checked(valueFixed16 * pixels);
        return (product + (product >> 31 & 0xFFFF)) >> 16;
    }
}
