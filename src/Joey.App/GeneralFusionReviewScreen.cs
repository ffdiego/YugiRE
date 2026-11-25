using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Numerics;
using System.Text.Json;

static partial class Game
{
    static NativeGeneralFusionTimeline? generalFusionReview;
    static string[] generalFusionResources=[];
    static CardBounds[] generalFusionSizes=[];
    static NativeGeneralFusionEffectFrame? generalFusionFrame;
    static NativeFusionCardTimeline? generalFusionCards;
    static NativeFusionCardFrame? generalFusionCardFrame;
    static Card[] generalFusionReviewCards=[];
    static string[] generalFusionMiniResources=[];

    static void ConfigureGeneralFusionReview(string[] args,Archive archive)
    {
        string? cardsOption=Option(args,"--review-general-fusion-cards=");
        if(!args.Contains("--review-general-fusion"))
        {
            if(cardsOption is not null)throw new InvalidDataException("Use --review-general-fusion with --review-general-fusion-cards=.");
            return;
        }
        generalFusionResources = DatGraphics.ReadGeneralFusionBank(archive);
        generalFusionReview=new(0);
        if(cardsOption is not null)
        {
            string[] fields=cardsOption.Split(',');
            if(fields.Length is not (3 or 4))
                throw new InvalidDataException("Expected two/three material IDs followed by result ID.");
            generalFusionReviewCards=fields.Select(s=>
            {
                if(!int.TryParse(s,System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,out int id))
                    throw new InvalidDataException("Expected decimal card resource ID.");
                return cards.FirstOrDefault(c=>c.Id==id&&c.IsMonster)??
                    throw new InvalidDataException("Unknown monster resource ID: "+s);
            }).ToArray();
            generalFusionMiniResources=generalFusionReviewCards.Select(card=>
                graphics.Artwork.MiniFor(card)??throw new InvalidDataException("Missing original miniature.")).ToArray();
            generalFusionCards=new(generalFusionReviewCards.Length-1);
        }
    }

    static void InitializeGeneralFusionReview()
    {
        generalFusionSizes=generalFusionResources.Select((path,index)=>
        {
            var texture=graphics.Texture(path);
            return NativeFusionResources.ValidateObservedSize(index,texture.Width,texture.Height);
        }).ToArray();
        foreach(string path in generalFusionMiniResources)
        {
            var texture=graphics.Texture(path);
            if(texture.Width!=50||texture.Height!=72)
                throw new InvalidDataException("Unexpected native fusion miniature dimensions.");
        }
        SampleGeneralFusionReview();
    }

    static int GeneralFusionReviewEnd => generalFusionCards is null
        ? NativeGeneralFusionEffect.EffectEndCounter : NativeFusionCardTimeline.EndCounter;

    static void SampleGeneralFusionReview()
    {
        var timeline=generalFusionReview!;
        generalFusionFrame=NativeGeneralFusionEffect.Draw(timeline,effectCosine,
            key=>generalFusionSizes[key]);
        // The material path has a different accumulator. Sample once per
        // deliberate Draw, including repeated F9 at the same counter.
        generalFusionCardFrame=generalFusionCards?.Draw(timeline.Counter,50,72,effectCosine);
    }

    static void DrawGeneralFusionReview()
    {
        if(Raylib.IsKeyPressed(KeyboardKey.F8))
        {
            generalFusionReview=new(0);
            if(generalFusionCards is not null)generalFusionCards=new(generalFusionCards.MaterialCount);
            SampleGeneralFusionReview();
        }
        else if(Raylib.IsKeyPressed(KeyboardKey.F7))
        {
            if(generalFusionReview!.Counter<GeneralFusionReviewEnd)
            {
                generalFusionReview.AdvanceNativeUpdate();
                SampleGeneralFusionReview();
            }
        }
        else if(Raylib.IsKeyPressed(KeyboardKey.F9)&&generalFusionReview!.Counter<GeneralFusionReviewEnd)SampleGeneralFusionReview();

        // Isolated review over the existing board. Scratch and result
        // overlay remain omitted; cards are explicit opt-in snapshots.
        Raylib.DrawTexture(graphics.Texture(DatGraphics.DuelDetailBackground),0,0,Color.White);
        if(duelTheme.Board is Texture2D board)
            Raylib.DrawTexturePro(board,new Rectangle(0,0,board.Width,board.Height),
                ToRectangle(DuelSurfaceLayout.Board),Vector2.Zero,0,Color.White);
        if(generalFusionFrame is { } frame)
        {
            DrawFusionReviewPass(NativeGeneralFusionEffect.Center,frame.Primary,generalFusionResources);
            if(frame.Additional is { } extra)
                DrawFusionReviewPass(NativeGeneralFusionEffect.Center,extra,generalFusionResources);
            foreach(var particle in frame.Particles)
            {
                var texture=graphics.Texture(generalFusionResources[particle.Key]);
                Raylib.DrawTexturePro(texture,new Rectangle(0,0,texture.Width,texture.Height),
                    ToRectangle(particle.Destination),Vector2.Zero,0,Color.White);
            }
        }
        if(generalFusionCardFrame is { } cardFrame)
        {
            foreach(var material in cardFrame.Materials)
                DrawGeneralFusionMini(material.MaterialIndex,material.Destination);
            if(cardFrame.Result is { } result)
                DrawGeneralFusionMini(generalFusionMiniResources.Length-1,result);
        }
    }

    static void DrawGeneralFusionMini(int index,CardBounds destination)
    {
        if(destination.Width<=0||destination.Height<=0)return;
        var texture=graphics.Texture(generalFusionMiniResources[index]);
        Raylib.DrawTexturePro(texture,new Rectangle(0,0,texture.Width,texture.Height),
            ToRectangle(destination),Vector2.Zero,0,Color.White);
    }

    static void CaptureGeneralFusionReview(RenderTexture2D canvas)
    {
        string basename=SaveVisualReviewImage(canvas,"general-fusion-effects");
        File.WriteAllText(basename+".json",JsonSerializer.Serialize(new
        {
            schemaVersion=2,routine="004524F0",center=NativeGeneralFusionEffect.Center,
            counter=generalFusionReview!.Counter,angle=generalFusionReview.Angle,
            effectLayerActive=generalFusionFrame.HasValue,frame=generalFusionFrame,
            resources=generalFusionResources,sizes=generalFusionSizes,cosineSource=effectCosineSource,
            cardResourceIds=generalFusionReviewCards.Select(c=>c.Id).ToArray(),
            miniResources=generalFusionMiniResources,cardFrame=generalFusionCardFrame,
            cardEvidence="entry/steady assembly (0..127); contraction/result/deactivation decompilation candidate",
            compositor="diagnostic raylib; native scratch and result overlay +AC omitted",
            audio="requests only, not played"
        },new JsonSerializerOptions{WriteIndented=true})+Environment.NewLine);
    }
}
