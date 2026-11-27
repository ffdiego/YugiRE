using Joey.Core;
using Joey.LookAndFeel;
using Raylib_cs;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

static partial class Game
{
    static string? breakReviewCardPath;
    static string breakReviewShinePath=NativeBreakResources.Shine;
    static NativeBreakFieldTarget breakReviewTarget;
    static int breakReviewPerspective;
    static NativeBreakTimeline breakReviewTimeline=new();

    static void ConfigureBreakFieldReview(string[] args,Archive archive)
    {
        if(Option(args,"--review-break=") is not string option)return;
        if(args.Contains("--duel-console") || Option(args,"--duel-script=") is not null ||
            args.Contains("--alpha-headless"))
            throw new InvalidDataException("Break review requires an isolated graphical session.");
        string[] fields=option.Split(',');
        if(fields.Length!=2 || !ushort.TryParse(fields[0],out ushort id) ||
            !ushort.TryParse(fields[1],NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out ushort descriptor))
            throw new InvalidDataException("Break review expects --review-break=ID,HEX; use ID 0 for hidden cards.");
        try { breakReviewTarget=new(descriptor); }
        catch(ArgumentException ex) { throw new InvalidDataException("Break review currently supports field region+slot 0..10 or monster region with snapshot slot FF.",ex); }
        if(Option(args,"--review-perspective=") is string perspective &&
            (!int.TryParse(perspective,out breakReviewPerspective) || breakReviewPerspective is <0 or >1))
            throw new InvalidDataException("Perspective must be 0 or 1.");
        if((descriptor&0x8000)==0)
        {
            if(id!=0)throw new InvalidDataException("Hidden review requires ID 0; identity is not loaded.");
            breakReviewCardPath=DatGraphics.MiniCardBack;
        }
        else
        {
            var card=cards.FirstOrDefault(c=>c.Id==id)??throw new InvalidDataException("Unknown card.");
            breakReviewCardPath=graphics.Artwork.MiniFor(card)??throw new InvalidDataException("Missing native miniature.");
        }
        breakReviewShinePath=NativeBreakResources.Parse(
            Encoding.Latin1.GetString(archive.Read(NativeBreakResources.Manifest)),archive.Entries.Select(e=>e.Name));
        breakReviewTimeline=new();
    }

    static void InitializeBreakFieldReview()
    {
        _=graphics.Texture(breakReviewCardPath!);
        var shine=graphics.Texture(breakReviewShinePath);
        if(shine.Width!=NativeBreakResources.ShineWidth || shine.Height!=NativeBreakResources.ShineHeight)
            throw new InvalidDataException("Unexpected break shine.bmp dimensions.");
    }

    static NativeBreakFieldFrame? CurrentBreakFieldReview()
    {
        var card=graphics.Texture(breakReviewCardPath!);
        // The native formula uses A54E5C bit0, opposite the existing
        // review convention in which perspective denotes the lower side.
        return NativeBreakFieldReview.Sample(breakReviewTimeline,breakReviewTarget.Center(breakReviewPerspective),
            breakReviewTarget.Descriptor,1-breakReviewPerspective,card.Width,card.Height,effectCosine);
    }

    static void DrawBreakFieldReview()
    {
        if(Raylib.IsKeyPressed(KeyboardKey.F8))breakReviewTimeline=new();
        else if(Raylib.IsKeyPressed(KeyboardKey.F7))breakReviewTimeline.AdvanceNativeUpdate();
        Raylib.DrawTexture(graphics.Texture(DatGraphics.DuelDetailBackground),0,0,Color.White);
        if(duelTheme.Board is Texture2D board)
            Raylib.DrawTexturePro(board,new Rectangle(0,0,board.Width,board.Height),
                ToRectangle(DuelSurfaceLayout.Board),Vector2.Zero,0,Color.White);
        if(CurrentBreakFieldReview() is not {} frame)return;
        DrawBreakFrame(frame,breakReviewCardPath!,breakReviewShinePath);
    }

    // Shared by review and live presentation. Packed +AC/+C are not yet
    // reproduced; retain this diagnostic GPU composition.
    static void DrawBreakFrame(NativeBreakFieldFrame frame,string cardPath,string shinePath)
    {
        var shine=graphics.Texture(shinePath);
        foreach(var particle in frame.Particles)
            // Diagnostic low-byte tint only. The native +AC argument
            // retains high bits and its actual blending is unresolved.
            Raylib.DrawTexture(shine,particle.Bounds.X,particle.Bounds.Y,
                new Color(255,255,255,particle.Intensity));
        var card=graphics.Texture(cardPath);
        foreach(var fragment in frame.Fragments)
        {
            var src=fragment.Source;
            var dst=fragment.Destination;
            int x=dst.X+(frame.RotationDegrees is 90 or 180?dst.Width:0);
            int y=dst.Y+(frame.RotationDegrees is 180 or 270?dst.Height:0);
            Raylib.DrawTexturePro(card,ToRectangle(src),new Rectangle(x,y,src.Width,src.Height),
                Vector2.Zero,frame.RotationDegrees,Color.White);
        }
    }

    static void CaptureBreakFieldReview(RenderTexture2D canvas)
    {
        string basename=SaveVisualReviewImage(canvas,"break");
        File.WriteAllText(basename+".json",JsonSerializer.Serialize(new
        {
            schemaVersion=1,routine="00450FD0",descriptor=breakReviewTarget.Descriptor,
            perspective=breakReviewPerspective,center=breakReviewTarget.Center(breakReviewPerspective),
            counter=breakReviewTimeline.Counter,active=breakReviewTimeline.Active,
            combatSnapshot=breakReviewTarget.IsCombatSnapshot,fieldZone=breakReviewTarget.FieldZone,
            blocksRelease=breakReviewTimeline.BlocksRelease,
            cardResource=breakReviewCardPath,shineResource=breakReviewShinePath,
            frame=CurrentBreakFieldReview(),cosineSource=effectCosineSource,
            compositor="diagnostic raylib rotation, green-key, low-byte tint; native +AC/+C not verified"
        },new JsonSerializerOptions{WriteIndented=true})+Environment.NewLine);
    }
}
