using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Numerics;
using System.Globalization;
using System.Text;
using System.Text.Json;

static partial class Game
{
    static string? altExReviewMode,altExCardPath;
    static NativeAlternativeActivationTimeline altReview=new();
    static NativeExclusionTimeline? exclusionReview;
    static NativeDisabledChainTarget exclusionTarget;
    static NativeExclusionStart exclusionStart;
    static NativeExclusionDraw? exclusionFrame;
    static string[] exclusionResources=[];
    static CardBounds[] exclusionSizes=[];
    static int altReviewCategory,altExPerspective;
    static uint? altReviewSound;
    static bool exclusionReviewPlaying;

    static void ConfigureAlternativeExclusionReview(string[] args,Archive archive)
    {
        string? alt=Option(args,"--review-alternative="),ex=Option(args,"--review-exclusion=");
        if(alt is null&&ex is null)return;
        if(alt is not null&&ex is not null)throw new InvalidDataException("Choose one review mode.");
        string[] fields=(alt??ex!).Split(',');
        if(fields.Length!=2||!ushort.TryParse(fields[0],out ushort id))
            throw new InvalidDataException("Expected card resource ID,category or hex descriptor.");
        if(alt is not null)
        {
            if(!int.TryParse(fields[1],out altReviewCategory)||altReviewCategory is <0 or >1)
                throw new InvalidDataException("Alternative review category must be 0 or 1.");
            var card=cards.FirstOrDefault(c=>c.Id==id)??throw new InvalidDataException("Unknown card.");
            altExCardPath=graphics.Artwork.For(card)??throw new InvalidDataException("Missing large artwork.");
            altExReviewMode="alternative";
        }
        else
        {
            if(!ushort.TryParse(fields[1],NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out ushort descriptor))
                throw new InvalidDataException("Expected hex field descriptor.");
            try {exclusionTarget=new(descriptor);}
            catch(ArgumentException exn){throw new InvalidDataException(exn.Message,exn);}
            if(Option(args,"--review-perspective=") is string perspective&&
                (!int.TryParse(perspective,out altExPerspective)||altExPerspective is <0 or >1))
                throw new InvalidDataException("Perspective must be 0 or 1.");
            if((descriptor&0x8000)==0)
            {
                if(id!=0)throw new InvalidDataException("Hidden review must use ID 0; never load its identity.");
                altExCardPath=DatGraphics.MiniCardBack;
                exclusionStart=NativeExclusionStart.FromConstructor(descriptor,0);
            }
            else
            {
                var card=cards.FirstOrDefault(c=>c.Id==id)??throw new InvalidDataException("Unknown card.");
                altExCardPath=graphics.Artwork.MiniFor(card)??throw new InvalidDataException("Missing mini artwork.");
                exclusionStart=NativeExclusionStart.FromConstructor(descriptor,(uint)card.Index);
            }
            exclusionResources=NativeExclusionResources.Parse(
                Encoding.Latin1.GetString(archive.Read(NativeExclusionStart.Manifest)),archive.Entries.Select(e=>e.Name));
            exclusionReview=new(exclusionStart);
            altExReviewMode="exclusion";
        }
    }

    static void InitializeAlternativeExclusionReview()
    {
        _=graphics.Texture(altExCardPath!);
        if(altExReviewMode=="alternative")
        {
            foreach(string resource in ActivationSpriteNames.All)_=graphics.Texture(resource);
        }
        else
        {
            exclusionSizes=exclusionResources.Select((name,key)=>{
                var t=graphics.Texture(name);
                var size=new CardBounds(0,0,t.Width,t.Height);
                if(size!=NativeExclusionResources.ExpectedSize(key))
                    throw new InvalidDataException("Unexpected exclusion resource geometry: "+name);
                return size;
            }).ToArray();
            AdvanceExclusionReviewDraw();
        }
    }

    static void AdvanceExclusionReviewDraw()
    {
        var texture=graphics.Texture(altExCardPath!);
        // Native 0043DC52 uses A54E5C bit 0; own side is ~bit &1.
        exclusionFrame=exclusionReview!.Draw(exclusionTarget.Center(altExPerspective),
            texture.Width,texture.Height,1-altExPerspective,effectCosine,k=>exclusionSizes[k]);
    }

    static NativeAlternativeActivationDraw? CurrentAlternativeReview()
    {
        var t=graphics.Texture(altExCardPath!);
        return altReview.Sample(t.Width,t.Height,altReviewCategory,effectCosine);
    }

    static void DrawAlternativeExclusionReview()
    {
        bool alternative=altExReviewMode=="alternative";
        if(Raylib.IsKeyPressed(KeyboardKey.F8))
        {
            altReview=new();altReviewSound=null;exclusionReviewPlaying=false;
            if(!alternative){exclusionReview=new(exclusionStart);AdvanceExclusionReviewDraw();}
        }
        else if(!alternative&&Raylib.IsKeyPressed(KeyboardKey.F5))
        {
            exclusionReviewPlaying=!exclusionReviewPlaying && exclusionReview!.Active;
        }
        else if(Raylib.IsKeyPressed(KeyboardKey.F7))
        {
            if(alternative)altReviewSound=altReview.AdvanceNativeUpdate();
            else { exclusionReviewPlaying=false;AdvanceExclusionReviewDraw(); }
        }
        else if(!alternative&&Raylib.IsKeyPressed(KeyboardKey.F9))
        {
            exclusionReviewPlaying=false;AdvanceExclusionReviewDraw();
        }
        else if(!alternative&&exclusionReviewPlaying)
        {
            // One native Draw sample per review draw. No Update clock,
            // no query advancing the timeline, no scheduler dependency.
            AdvanceExclusionReviewDraw();
            if(!exclusionReview!.Active) exclusionReviewPlaying=false;
        }
        Raylib.DrawTexture(graphics.Texture(DatGraphics.DuelDetailBackground),0,0,Color.White);
        if(duelTheme.Board is Texture2D board)
            Raylib.DrawTexturePro(board,new Rectangle(0,0,board.Width,board.Height),
                ToRectangle(DuelSurfaceLayout.Board),Vector2.Zero,0,Color.White);
        if(alternative&&CurrentAlternativeReview() is {} frame)
        {
            DrawAltExTexture(altExCardPath!,frame.CardBounds,frame.CardIntensity,0);
            if(frame.Glow is {} glow)
            {
                DrawAltGlow(glow.Lower);
                var b=frame.ScratchBounds;
                // Same diagnostic compositor as existing activation renderer.
                Raylib.DrawRectangle(b.X,b.Y,b.Width,b.Height,new Color(0,0,0,Math.Clamp(glow.Scratch.Opacity,0,255)));
                DrawAltGlow(glow.Upper);
            }
        }
        else if(!alternative&&exclusionFrame is {} exFrame)
        {
            foreach(var pass in exFrame.Passes)
                DrawAltExTexture(pass.ResourceKey is int k?exclusionResources[k]:altExCardPath!,
                    pass.Bounds,pass.Intensity,pass.RotationDegrees);
        }
    }

    static void DrawAltGlow(NativeActivationPass pass)
    {
        if(pass.ScaleFixed16<=0||pass.Opacity<=0)return;
        DrawEffectStamp(ActivationSpriteNames.At(pass.ResourceIndex),NativeAlternativeActivationTimeline.Center,
            pass.RotationIndex,pass.ScaleFixed16,pass.Opacity);
    }

    static void DrawAltExTexture(string name,CardBounds b,int intensity,int rotation)
    {
        if(b.Width<=0||b.Height<=0||intensity<=0)return;
        var t=graphics.Texture(name);
        Raylib.DrawTexturePro(t,new Rectangle(0,0,t.Width,t.Height),
            new Rectangle(b.X+b.Width/2,b.Y+b.Height/2,b.Width,b.Height),
            new Vector2(b.Width/2,b.Height/2),rotation,new Color(255,255,255,Math.Clamp(intensity,0,255)));
    }

    static void CaptureAlternativeExclusionReview(RenderTexture2D canvas)
    {
        string basename=SaveVisualReviewImage(canvas,altExReviewMode!);
        bool alternative=altExReviewMode=="alternative";
        File.WriteAllText(basename+".json",JsonSerializer.Serialize(new{
            schemaVersion=1,routine=alternative?"00450AD0":"0043DBE0",
            resource=altExCardPath,descriptor=alternative?(ushort?)null:exclusionStart.Descriptor,
            alternativeFrame=alternative?CurrentAlternativeReview():null,
            exclusionFrame=alternative?null:exclusionFrame,
            counter=alternative?altReview.Counter:exclusionReview!.Counter,
            blocksRelease=alternative?(bool?)null:exclusionReview!.BlocksRelease,
            active=alternative?(bool?)null:exclusionReview!.Active,
            soundRequest=alternative?altReviewSound:NativeExclusionTimeline.ConstructorSound,
            resources=exclusionResources,cosineSource=effectCosineSource,
            compositor="diagnostic raylib tint/geometry; native raster/remap not verified"
        },new JsonSerializerOptions{WriteIndented=true})+Environment.NewLine);
    }
}
