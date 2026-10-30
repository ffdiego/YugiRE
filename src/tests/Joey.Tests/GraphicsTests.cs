using System.Buffers.Binary;
using System.Text;
using Joey.Core;

static class GraphicsTests
{
    public static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        var bmp = new byte[62]; // 1x2 with two padded four-byte rows.
        bmp[0] = (byte)'B'; bmp[1] = (byte)'M';
        BinaryPrimitives.WriteUInt32LittleEndian(bmp.AsSpan(10), 54);
        BinaryPrimitives.WriteUInt32LittleEndian(bmp.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(18), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(22), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bmp.AsSpan(26), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bmp.AsSpan(28), 24);
        check(BitmapInfo.Read(bmp) == new BitmapInfo(1, 2), "BMP padded row dimensions");
        reject(() => BitmapInfo.Read(bmp[..^1]), "Reject truncated BMP padded pixels");
        reject(() => BitmapInfo.Read([]), "Reject missing BMP header");
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(22), -2);
        check(BitmapInfo.Read(bmp) == new BitmapInfo(1, 2), "BMP top-down dimensions");
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(18), int.MaxValue);
        reject(() => BitmapInfo.Read(bmp), "Reject oversized BMP dimensions before allocation");
        var text = Encoding.ASCII.GetBytes("// sample\r\nmenu.bmp\t200320 // position\r\nmask.bmp 5\r\n");
        var sprite = SceneSprite.Find(text, "MENU.BMP");
        check(sprite.X == 200 && sprite.Y == 320, "Parse scene xxxYYY position and ignore comments");
        reject(() => SceneSprite.Find(text, "mask.bmp"), "Do not interpret animation count as coordinates");
        var menu = new MenuResources(sprite);
        check(menu.Hit(200, 320) == 0 && menu.Hit(599, 569) == 4 && menu.Hit(300, 420) == 2,
            "Menu button strip hit regions");
        check(menu.Hit(600, 400) == -1 && menu.Hit(300, 570) == -1 && menu.Hit(199, 320) == -1,
            "Menu rejects clicks outside the strip");
        var grid = new CardGrid(225, 110);
        check(grid.Cell(0) == (225, 110) && grid.Cell(9) == (720, 110) && grid.Cell(10) == (225, 197) &&
            grid.Cell(49) == (720, 458), "Native grid pitch and row-major ordering for 50 cards");
        check(grid.Hit(225, 110) == 0 && grid.Hit(769, 529) == 49 && grid.Hit(225, 197) == 10,
            "Grid hit testing at first, next-row and final cards");
        check(grid.Hit(275, 110) == -1 && grid.Hit(225, 182) == -1 && grid.Hit(224, 110) == -1 &&
            grid.Hit(775, 110) == -1 && grid.Hit(225, 545) == -1, "Grid gaps and outside clicks do not select cards");
        check(CardGrid.LastPage(0) == 0 && CardGrid.LastPage(50) == 0 && CardGrid.LastPage(51) == 1 &&
            CardGrid.LastPage(100) == 1 && CardGrid.LastPage(1114) == 22, "Pagination handles empty, exact and partial pages");
        var alpha = new Card(2, 900, "Alpha", "", 0);
        var beta = new Card(1, 100, "Beta", "", 0);
        var unnamed = new Card(0, 50, "", "", 0);
        byte[] ranks = [0, 0, 2, 0, 1, 0];
        var sorted = CardListResources.Sort([beta, unnamed, alpha], ranks);
        check(sorted.SequenceEqual([alpha, beta, unnamed]) && sorted[0].Id == 900,
            "Sort ranks are addressed by resource index, preserve IDs and put unnamed records last");
        reject(() => CardListResources.Sort([alpha], [0, 0]), "Reject card outside sort table");
        reject(() => CardListResources.Sort([], [0]), "Reject truncated sort word");
        var duplicateScene = Encoding.ASCII.GetBytes("digits.bmp 476576\ndigits.bmp 530576\n");
        check(SceneSprite.Find(duplicateScene, "digits.bmp", 1).X == 530, "Read second placement of a shared sprite");
    }
}
