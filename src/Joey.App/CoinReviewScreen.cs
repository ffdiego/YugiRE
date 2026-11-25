using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Numerics;
using System.Text;
using System.Text.Json;

static partial class Game
{
    static NativeCoinTimeline? coinReview;
    static int coinReviewCount,coinReviewHighlight;
    static uint coinReviewBits;
    static CardPoint coinReviewOrigin;
    static NativeCoinResource[] coinReviewResources=[];
    static NativeDiceTexture[] coinReviewTextures=[];
    static NativeCoinDraw[] coinReviewFrame=[];
    static NativeCoinExtras[] coinReviewExtras=[];

    static void ConfigureCoinReview(string[] args,Archive archive)
    {
        if(Option(args,"--review-coin=") is not string option)return;
        string[] fields=option.Split(',');
        if(fields.Length!=5 || !int.TryParse(fields[0],out int count) || count is not (1 or 3) ||
            !uint.TryParse(fields[1],out uint bits) || bits>=(1u<<count) ||
            !int.TryParse(fields[2],out int x) || x is <0 or >=800 ||
            !int.TryParse(fields[3],out int y) || y is <0 or >=600 ||
            !int.TryParse(fields[4],out int highlight) || highlight is not (0 or 1))
            throw new InvalidDataException("Coin review expects --review-coin=COUNT,BITS,X,Y,HIGHLIGHT (count 1 or 3).");
        coinReviewResources=NativeCoinResources.Parse(
            Encoding.Latin1.GetString(archive.Read(NativeCoinResources.Manifest)),
            archive.Entries.Select(e=>e.Name));
        coinReviewCount=count;coinReviewBits=bits;coinReviewOrigin=new(x,y);
        coinReviewHighlight=highlight;
        coinReview=new(count,bits,coinReviewOrigin);
    }

    static void InitializeCoinReview()
    {
        coinReviewTextures=coinReviewResources.Select(record=>
        {
            var texture=graphics.Texture(record.Path);
            return NativeCoinResources.ValidateTexture(record,texture.Width,texture.Height);
        }).ToArray();
        SampleCoinReview();
    }

    static void SampleCoinReview()
    {
        var timeline=coinReview!;
        coinReviewFrame=timeline.Active
            ? Enumerable.Range(0,timeline.Count).Select(i=>
                NativeCoinPassLayout.Sample(timeline,i,coinReviewHighlight,effectCosine,
                    key=>coinReviewTextures[key])).ToArray()
            : [];
        coinReviewExtras=coinReviewFrame.Select((frame,index)=>
            NativeCoinExtraLayout.Sample(timeline,index,frame,key=>coinReviewTextures[key])).ToArray();
    }

    static void DrawCoinReview()
    {
        if(Raylib.IsKeyPressed(KeyboardKey.F8))
        {
            coinReview=new(coinReviewCount,coinReviewBits,coinReviewOrigin);
            SampleCoinReview();
        }
        else if(Raylib.IsKeyPressed(KeyboardKey.F7))
        {
            coinReview!.AdvanceNativeUpdate();
            SampleCoinReview();
        }
        Raylib.DrawTexture(graphics.Texture(DatGraphics.DuelDetailBackground),0,0,Color.White);
        if(duelTheme.Board is Texture2D board)
            Raylib.DrawTexturePro(board,new Rectangle(0,0,board.Width,board.Height),
                ToRectangle(DuelSurfaceLayout.Board),Vector2.Zero,0,Color.White);
        // Per coin: primary passes, reflection (omitted until +94/+A0
        // resolved), then gated sparkles, preserving native order.
        for(int index=0;index<coinReviewFrame.Length;index++)
        foreach(var pass in coinReviewFrame[index].Passes.Concat(coinReviewExtras[index].Sparkles))
        {
            if(pass.Destination.Width<=0||pass.Destination.Height<=0)continue;
            Raylib.DrawTexturePro(graphics.Texture(coinReviewResources[pass.Key].Path),
                ToRectangle(pass.Source),ToRectangle(pass.Destination),Vector2.Zero,0,
                new Color(255,255,255,Math.Clamp(pass.Parameter256,0,255)));
        }
    }

    static void CaptureCoinReview(RenderTexture2D canvas)
    {
        string basename=SaveVisualReviewImage(canvas,"coin");
        File.WriteAllText(basename+".json",JsonSerializer.Serialize(new
        {
            schemaVersion=2,routine="00457520",count=coinReviewCount,
            resultBits=coinReviewBits,origin=coinReviewOrigin,highlightBit=coinReviewHighlight,
            counter=coinReview!.Counter,active=coinReview.Active,frame=coinReviewFrame,extras=coinReviewExtras,
            resources=coinReviewResources,cosineSource=effectCosineSource,
            compositor="diagnostic raylib tint/key; bank remap unverified; scratch reflection plan only, sparkles rendered diagnostically",
            audio="request only, not played"
        },new JsonSerializerOptions{WriteIndented=true})+Environment.NewLine);
    }
}
