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
    }
}
