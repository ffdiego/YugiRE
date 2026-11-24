using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Numerics;
using System.Text;
using System.Text.Json;

static partial class Game
{
    static NativeDiceTimeline? diceReview;
    static int diceReviewIndex;
    static CardPoint diceReviewOrigin;
    static NativeDiceResource[] diceReviewResources=[];
    static NativeDiceTexture[] diceReviewTextures=[];
    static NativeDiceDraw? diceReviewFrame;

    static void ConfigureDiceReview(string[] args,Archive archive)
    {
        if (Option(args,"--review-dice=") is not string option) return;
        string[] fields=option.Split(',');
        if(fields.Length!=3 || !int.TryParse(fields[0],out int index) || index is <1 or >6 ||
            !int.TryParse(fields[1],out int x) || x is <0 or >=800 ||
            !int.TryParse(fields[2],out int y) || y is <0 or >=600)
            throw new InvalidDataException("Dice review expects --review-dice=INDEX,X,Y.");
        if(!effectCosineFromNativeCapture)
            throw new InvalidDataException("Dice review requires --native-cosine=<capture.json>.");
        diceReviewResources=NativeDiceResources.Parse(
            Encoding.Latin1.GetString(archive.Read(NativeDiceResources.Manifest)),
            archive.Entries.Select(e=>e.Name));
        diceReviewIndex=index;
        diceReviewOrigin=new(x,y);
        diceReview=new(index,diceReviewOrigin);
    }

    static NativeDiceTexture DiceTexture(int key)=>diceReviewTextures[key];

    static void InitializeDiceReview()
    {
        diceReviewTextures=diceReviewResources.Select((record,key)=>
        {
            var texture=graphics.Texture(record.Path);
            int patterns=key<6 ? record.Parameter!.Value : 1;
            var info=new NativeDiceTexture(texture.Width,texture.Height,patterns);
            if(key<6)_=info.PatternWidth;
            return info;
        }).ToArray();
        diceReviewFrame=diceReview!.Draw(effectCosine,DiceTexture);
    }

    static void DrawDiceReview()
    {
        if(Raylib.IsKeyPressed(KeyboardKey.F8))
        {
            diceReview=new(diceReviewIndex,diceReviewOrigin);
            diceReviewFrame=diceReview.Draw(effectCosine,DiceTexture);
        }
        else if(Raylib.IsKeyPressed(KeyboardKey.F7))
        {
            diceReview!.AdvanceNativeUpdate();
            diceReviewFrame=diceReview.Draw(effectCosine,DiceTexture);
        }
        else if(Raylib.IsKeyPressed(KeyboardKey.F9))
            diceReviewFrame=diceReview!.Draw(effectCosine,DiceTexture);
        Raylib.DrawTexture(graphics.Texture(DatGraphics.DuelDetailBackground),0,0,Color.White);
        if(duelTheme.Board is Texture2D board)
            Raylib.DrawTexturePro(board,new Rectangle(0,0,board.Width,board.Height),
                ToRectangle(DuelSurfaceLayout.Board),Vector2.Zero,0,Color.White);
        if(diceReviewFrame is { } frame)
        {
            DrawDicePass(frame.Shadow);
            DrawDicePass(frame.Die);
        }
    }

    // Diagnostic compositor: same native pass order and rectangles, but
    // Raylib blend/keying has not been compared with the original blit.
    static void DrawDicePass(NativeDicePass pass)
    {
        if(pass.Destination.Width<=0 || pass.Destination.Height<=0)return;
        Raylib.DrawTexturePro(graphics.Texture(diceReviewResources[pass.Key].Path),
            ToRectangle(pass.Source),ToRectangle(pass.Destination),Vector2.Zero,0,
            new Color(255,255,255,Math.Clamp(pass.Intensity,0,255)));
    }

    static void CaptureDiceReview(RenderTexture2D canvas)
    {
        string basename=SaveVisualReviewImage(canvas,"dice");
        File.WriteAllText(basename+".json",JsonSerializer.Serialize(new {
            schemaVersion=1,routine="00456B90",index=diceReviewIndex,origin=diceReviewOrigin,
            counter=diceReview!.Counter,center=diceReview.Center,frame=diceReviewFrame,
            resources=diceReviewResources,cosineSource="native-runtime-capture",
            compositor="diagnostic manifest-order mapping; raylib blend/key not verified",
        },new JsonSerializerOptions{WriteIndented=true})+Environment.NewLine);
    }
}
