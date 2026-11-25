using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Numerics;
using System.Text;
using System.Text.Json;

static partial class Game
{
    static string? layerFontReviewMode;
    static string[] layerFontReviewResources = [];
    static NativeLayerFontA layerFontA = new();
    static NativeLayerFontB layerFontB = new();
    static bool layerFontWantsDisplay = true;
    static CardBounds? layerFontReviewBounds;
    static int? layerFontReviewIntensity;

    static void ConfigureLayerFontReview(string[] args, Archive archive)
    {
        if (Option(args,"--review-layerfont=") is not string mode) return;
        if (mode is not ("a" or "b"))
            throw new InvalidDataException("Layerfont review expects a or b.");
        layerFontReviewResources = NativeLayerFontResources.Parse(
            Encoding.Latin1.GetString(archive.Read(NativeLayerFontResources.Manifest)),
            archive.Entries.Select(e=>e.Name));
        layerFontReviewMode = mode;
    }

    static void DrawLayerFontReview()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.F8))
        {
            layerFontA = new(); layerFontB = new();
            layerFontWantsDisplay = true;
        }
        else
        {
            if (Raylib.IsKeyPressed(KeyboardKey.F5)) layerFontWantsDisplay = !layerFontWantsDisplay;
            if (Raylib.IsKeyPressed(KeyboardKey.F7))
            {
                if (layerFontReviewMode == "a") layerFontA.AdvanceNativeDraw();
                else layerFontB.AdvanceNativeDraw(layerFontWantsDisplay);
            }
        }
        Raylib.DrawTexture(graphics.Texture(DatGraphics.DuelDetailBackground),0,0,Color.White);
        if (duelTheme.Board is Texture2D board)
            Raylib.DrawTexturePro(board,new Rectangle(0,0,board.Width,board.Height),
                ToRectangle(DuelSurfaceLayout.Board),Vector2.Zero,0,Color.White);
        bool first = layerFontReviewMode == "a";
        var texture = graphics.Texture(layerFontReviewResources[first ? 0 : 1]);
        // Geometry comes from the recovered Draw expressions; B's width/height
        // register mapping still needs assembly confirmation. This diagnostic
        // does not establish the native compositor or the live duel gate.
        layerFontReviewBounds = first ? NativeLayerFontA.Bounds(texture.Width,texture.Height) :
            layerFontB.Top(texture.Height,effectCosine) is int y ?
                new CardBounds(525-texture.Width/2,y,texture.Width,texture.Height) : null;
        layerFontReviewIntensity = first ? layerFontA.Intensity(effectCosine) : null;
        if (layerFontReviewBounds is { } bounds)
            Raylib.DrawTexture(texture,bounds.X,bounds.Y,
                new Color(255,255,255,Math.Clamp(layerFontReviewIntensity ?? 255,0,255)));
    }

    static void CaptureLayerFontReview(RenderTexture2D canvas)
    {
        string basename = SaveVisualReviewImage(canvas,"layerfont");
        File.WriteAllText(basename+".json",JsonSerializer.Serialize(new {
            schemaVersion=1, source="V27/V28 isolated draw review",
            routine=layerFontReviewMode=="a" ? "00455FF0" : "00456360",
            resource=layerFontReviewResources[layerFontReviewMode=="a"?0:1],
            counter=layerFontA.Counter,value=layerFontB.Value,wantsDisplay=layerFontWantsDisplay,
            bounds=layerFontReviewBounds,intensity=layerFontReviewIntensity,
            cosineSource=effectCosineSource,
            compositor="provisional raylib texture/tint; native vtable blit and B registers pending"
        },new JsonSerializerOptions{WriteIndented=true})+Environment.NewLine);
    }
}
