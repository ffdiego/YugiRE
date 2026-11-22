namespace Joey.LookAndFeel;

/// <summary>
/// Resource order of j/duel/card/card.txt and Draw 00418EF0:
/// 0 small, 1 large (region 11 / hand), 2 small alternate, 3 large alternate.
/// Variant selection in the EXE also consults card owner/face state.
/// Mapping normal/alternate to displayed near/far side here remains a
/// specific client hypothesis until paired pixels establish the exact state.
/// </summary>
public static class NativeCardSelectionFrame
{
    private const string Root = @"j\duel\card\";
    public const string Small = Root + "card_waku#s.bmp";
    public const string Large = Root + "card_waku#l.bmp";
    public const string SmallAlternate = Root + "card_waku#s2.bmp";
    public const string LargeAlternate = Root + "card_waku#l2.bmp";

    public static string LargeSprite(bool nearSide) => nearSide ? Large : LargeAlternate;
    public static string SmallSprite(bool nearSide) => nearSide ? Small : SmallAlternate;

    public static bool IsFrame(string? resource) =>
        resource is not null &&
        (resource.Equals(Small, StringComparison.OrdinalIgnoreCase) ||
         resource.Equals(Large, StringComparison.OrdinalIgnoreCase) ||
         resource.Equals(SmallAlternate, StringComparison.OrdinalIgnoreCase) ||
         resource.Equals(LargeAlternate, StringComparison.OrdinalIgnoreCase));

    public static CardBounds Outline(CardBounds card, int frameWidth, int frameHeight)
    {
        if (frameWidth < card.Width || frameHeight < card.Height)
            throw new ArgumentOutOfRangeException(nameof(frameWidth),
                "Native focus frame must enclose the card");
        return new CardBounds(
            card.X - (frameWidth - card.Width) / 2,
            card.Y - (frameHeight - card.Height) / 2,
            frameWidth, frameHeight);
    }
}
