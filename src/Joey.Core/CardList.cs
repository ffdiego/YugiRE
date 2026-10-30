using System.Buffers.Binary;

namespace Joey.Core;

// Native hit testing: 0x0047DE80; drawing loop: 0x0047E750.
public readonly record struct CardGrid(int X, int Y)
{
    public const int Columns = 10, Rows = 5, PageSize = Columns * Rows;
    public const int PitchX = 55, PitchY = 87, CardWidth = 50, CardHeight = 72;
    public (int X, int Y) Cell(int slot)
    {
        if (slot < 0 || slot >= PageSize) throw new ArgumentOutOfRangeException(nameof(slot));
        return (X + slot % Columns * PitchX, Y + slot / Columns * PitchY);
    }
    public int Hit(float x, float y)
    {
        x -= X; y -= Y;
        if (x < 0 || y < 0 || x >= Columns * PitchX || y >= Rows * PitchY ||
            x % PitchX >= CardWidth || y % PitchY >= CardHeight) return -1;
        return (int)(x / PitchX) + (int)(y / PitchY) * Columns;
    }
    public static int LastPage(int count) => Math.Max(0, (count - 1) / PageSize);
}

public sealed record CardListResources(CardGrid Grid, SceneSprite Previous, SceneSprite Next,
    SceneSprite CurrentPage, SceneSprite TotalPages)
{
    public const string Frame = @"j\list\card_waku_s.bmp", Left = @"j\list\pe_yaji_l.bmp",
        Right = @"j\list\pe_yaji_r.bmp", Digits = @"j\list\page_font.bmp";
    public const string All = @"j\list\e_allcards1.bmp", AllSelected = @"j\list\e_allcards2.bmp",
        Joey = @"j\list\joey_only1.bmp", JoeySelected = @"j\list\joey_only2.bmp";
    public SceneSprite AllPosition { get; init; }
    public SceneSprite JoeyPosition { get; init; }
    public static CardListResources Load(Archive archive)
    {
        byte[] scene = archive.Read(@"j\list\list_scene.txt");
        var grid = SceneSprite.Find(scene, "card_waku_s.bmp");
        return new(new(grid.X, grid.Y), SceneSprite.Find(scene, "pe_yaji_l.bmp"),
            SceneSprite.Find(scene, "pe_yaji_r.bmp"), SceneSprite.Find(scene, "page_font.bmp"),
            SceneSprite.Find(scene, "page_font.bmp", 1))
        {
            AllPosition = SceneSprite.Find(scene, "?_allcards1.bmp"),
            JoeyPosition = SceneSprite.Find(scene, "joey_only1.bmp")
        };
    }
    public static List<Card> Sort(IEnumerable<Card> cards, byte[] ranks)
    {
        if (ranks.Length % 2 != 0) throw new InvalidDataException("Truncated card sort table");
        var ranked = cards.Select(card =>
        {
            if (card.Index < 0 || card.Index >= ranks.Length / 2) throw new InvalidDataException("Card index outside sort table");
            return (Card: card, Rank: BinaryPrimitives.ReadUInt16LittleEndian(ranks.AsSpan(card.Index * 2)));
        }).ToList();
        // Unnamed resource records stay available, after named cards in this unrestricted demo.
        return ranked.OrderBy(c => c.Card.Name.Length == 0).ThenBy(c => c.Rank).ThenBy(c => c.Card.Index)
            .Select(c => c.Card).ToList();
    }
    // 0x004024E0 reads CARD_Pack.bin; 0x0047DBB0 tests membership against a version mask.
    // A mask of 7 includes all three releases and does not check save/unlock state.
    public static List<Card> ForPacks(IEnumerable<Card> cards, byte[] packs, ushort mask = 7)
    {
        if (packs.Length % 2 != 0) throw new InvalidDataException("Truncated card pack table");
        return cards.Where(card =>
        {
            if (card.Index < 0 || card.Index >= packs.Length / 2) throw new InvalidDataException("Card index outside pack table");
            return (BinaryPrimitives.ReadUInt16LittleEndian(packs.AsSpan(card.Index * 2)) & mask) != 0;
        }).ToList();
    }
}
