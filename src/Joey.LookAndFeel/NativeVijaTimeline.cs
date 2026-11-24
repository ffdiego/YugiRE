namespace Joey.LookAndFeel;

/// <summary>Logical resource dimensions, before virtual bank remap.
/// Parameter is the unmodified integer from vija.txt.</summary>
public readonly record struct NativeVijaTexture(int Width,int Height,int Parameter)
{
    public NativeVijaAtlas Atlas => NativeVijaAtlas.FromParameter(Parameter,Width,Height);
    public void Validate()
    {
        if(Width<=0||Height<=0)throw new InvalidDataException("Invalid Vija resource dimensions.");
    }
}

/// <summary>Virtual +48: direct texture pass, source and destination are
/// integer logical pixels. Intensity and scale are independent.</summary>
public readonly record struct NativeVijaPass(int Key,CardBounds Source,CardBounds Destination,int Intensity);

/// <summary>00458844..004589C8: construct the letter surface in native
/// order. The two key-4 copies use full texture width, not strip width.
/// Preserve source/destination and the raw +98 channel arguments.
/// Do not silently replace this native color/mask operation with alpha.</summary>
public readonly record struct NativeVijaScratch(int Width,int Height,
    int BackgroundKey,int FirstBackgroundX,int SecondBackgroundX,
    int ColorKey,CardBounds ColorSource,int MaskKey,CardBounds MaskSource);

public readonly record struct NativeVijaLetterPass(CardBounds Source,CardBounds Destination,
    int Intensity,bool DistortedRow);

public sealed record NativeVijaFrame(int Counter,CardPoint Center,NativeVijaPass Base,
    NativeVijaScratch? Scratch,NativeVijaLetterPass[] Letters,NativeVijaPass? Overlay,
    bool DrawFlagAfter);

/// <summary>004584C0. One Vija object, single or group mode. Update is
/// 004481F0 (no-op); a real Draw increments by four. The raw virtual +4
/// predicate is distinct from the flag at +4 written by Draw. The
/// owner must handle native disposal/dispatch; never use BlocksCompletion
/// alone after the object has been removed from its container.</summary>
public sealed class NativeVijaTimeline
{
    public const int Increment=4;
    public NativeVijaContract Contract { get; }
    public CardPoint Origin { get; }
    public int Counter { get; private set; }
    public bool DrawFlag { get; private set; }=true;
    public bool BlocksCompletion => Contract.BlocksCompletion(Counter);

    public NativeVijaTimeline(NativeVijaContract contract,CardPoint origin)
    {
        Contract=contract;Origin=origin;Counter=contract.InitialCounter;
    }

    // Resource IDs from 005DA410: five little-endian ushorts at stride 4.
    // The second ushort in each entry is zero in the captured table.
    public static IReadOnlyList<ushort> VictoryResourceIds { get; } =
        Array.AsReadOnly(new ushort[]{1528,1541,1542,1543,1544});

    public CardPoint CenterAt(int t,ReadOnlySpan<int> cosine)
    {
        NativeFusionMath.Validate(cosine);
        if(t<0||!Contract.GroupMode)return Origin;
        int p=Math.Clamp(t,64,256)-64;
        int x=(Origin.X*(192-p)+(Contract.LetterIndex+2)*100*p)/192;
        int y=(Origin.Y*(192-p)+300*p)/192+
            (unchecked(NativeFusionMath.C(cosine,t*8-128)<<3)>>16);
        return new(x,y);
    }

    // 0045863B..00458658. Raw virtual +48 intensity, not alpha 0..255.
    public int FadeAt(int t)=>t>Contract.PhaseLimit ?
        256+4*(Contract.PhaseLimit-t):256;

    public NativeVijaFrame? SampleNextDraw(ReadOnlySpan<int> cosine,
        Func<int,NativeVijaTexture> resource)
    {
        if(!DrawFlag)return null;
        NativeFusionMath.Validate(cosine);
        ArgumentNullException.ThrowIfNull(resource);
        int t=Counter+Increment;
        if(t<0)return null; // staggered object, no texture lookup
        var center=CenterAt(t,cosine);
        var a=resource(0).Atlas;
        int scale=Math.Min(256,4*t);
        int fade=FadeAt(t);
        int w=unchecked(a.FrameWidth*scale)>>8,h=unchecked(a.Height*scale)>>8;
        int top=center.Y-h/2-a.YOffset;
        int baseIndex=(t/8)%a.PatternCount;
        var basePass=new NativeVijaPass(0,
            new(baseIndex*a.FrameWidth,0,a.FrameWidth,a.Height),
            new(center.X-w/2,top,w,h),t<64?scale:fade);

        NativeVijaScratch? scratch=null;
        NativeVijaLetterPass[] letters=[];
        if(t>=64)
        {
            var mask=resource(3).Atlas;
            var color=resource(2);
            var background=resource(4);
            color.Validate();background.Validate();
            int colorPatterns=color.Parameter/1000;
            if(colorPatterns<=0||color.Width<mask.FrameWidth*colorPatterns||
                color.Height<mask.Height||background.Height<mask.Height||
                mask.PatternCount<5)
                throw new InvalidDataException("Vija scratch strips cannot supply the recovered rectangles.");
            int bgX=t%background.Width;
            scratch=new(mask.FrameWidth,mask.Height,4,bgX,bgX-background.Width,
                2,new((t/8%colorPatterns)*mask.FrameWidth,0,mask.FrameWidth,mask.Height),
                3,new(Contract.LetterIndex*mask.FrameWidth,0,mask.FrameWidth,mask.Height));
            int local=t-64;
            int y=top+mask.YOffset;
            if(local<128)
            {
                int scaledWidth=unchecked(mask.FrameWidth*local*2)>>8;
                int left=center.X-scaledWidth/2;
                int amplitude=16-local/8;
                letters=new NativeVijaLetterPass[mask.Height];
                for(int row=0;row<mask.Height;row++)
                {
                    int wave=unchecked(NativeFusionMath.C(cosine,8*local-128+16*row)*amplitude)>>16;
                    letters[row]=new(new(0,row,mask.FrameWidth,1),
                        new(left+wave,y+row,scaledWidth,1),256,true);
                }
            }
            else
                letters=[new(new(0,0,mask.FrameWidth,mask.Height),
                    new(center.X-mask.FrameWidth/2,y,mask.FrameWidth,mask.Height),fade,false)];
        }

        NativeVijaPass? overlay=null;
        if(t>=32)
        {
            var b=resource(1).Atlas;
            int step=(t-32)/4;
            int frame=step<8?step/4:((step&3) switch {0=>2,1=>3,2=>4,_=>3});
            if(frame>=b.PatternCount)throw new InvalidDataException("Vija overlay atlas lacks selected frame.");
            int drop=step<8?8-step:0;
            overlay=new(1,new(frame*b.FrameWidth,0,b.FrameWidth,b.Height),
                new(center.X-b.FrameWidth/2,top+b.YOffset+drop,b.FrameWidth,b.Height),fade);
        }
        return new(t,center,basePass,scratch,letters,overlay,t<Contract.BlockingThrough);
    }

    /// <summary>One invocation of the original Draw slot. Before zero,
    /// the counter still advances but no image is drawn. At the final
    /// boundary the frame is returned before clearing DrawFlag.</summary>
    public NativeVijaFrame? Draw(ReadOnlySpan<int> cosine,Func<int,NativeVijaTexture> resource)
    {
        if(!DrawFlag)return null;
        var frame=SampleNextDraw(cosine,resource);
        Counter+=Increment;
        if(frame is not null)DrawFlag=frame.DrawFlagAfter;
        return frame;
    }
}
