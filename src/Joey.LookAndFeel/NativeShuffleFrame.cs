namespace Joey.LookAndFeel;

/// <summary>
/// Native shuffle 00453540 / 00453560: 41 Draw samples, five cyclic BMP
/// frames, and deck-to-center-to-deck travel. The absolute center
/// DAT_005DB570 has not been recovered, so the client supplies it.
/// </summary>
public readonly record struct NativeShuffleFrame(
    int Counter, int SpriteFrame, bool Moving, int DeckWeightFixed8)
{
    public const int UpdateIncrement = 8;
    public const int Updates = 41; // t=0,8,...320; next Update deactivates.

    public static NativeShuffleFrame FromUpdate(int update)
    {
        if (update is < 0 or >= Updates)
            throw new ArgumentOutOfRangeException(nameof(update));
        int counter = update * UpdateIncrement;
        bool moving = counter < 0x40 || counter > 0x100;
        int nearEnd = Math.Min(counter, 0x140 - counter);
        int weight = moving ? 0x100 - 4 * nearEnd : 0;
        return new(counter, (counter / 8) % 5, moving, weight);
    }

    /// <summary>
    /// 00453560 lerps from the deck center to native shared center and
    /// returns on the last eight updates using the same 8.8 integer weight.
    /// </summary>
    public CardPoint Position(CardPoint deckCenter, CardPoint stageCenter)
    {
        int x = stageCenter.X + (int)((long)(deckCenter.X - stageCenter.X) * DeckWeightFixed8 >> 8);
        int y = stageCenter.Y + (int)((long)(deckCenter.Y - stageCenter.Y) * DeckWeightFixed8 >> 8);
        return new(x, y);
    }
}
