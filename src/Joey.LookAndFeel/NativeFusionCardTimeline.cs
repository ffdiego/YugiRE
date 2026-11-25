namespace Joey.LookAndFeel;

public readonly record struct NativeFusionMaterialPass(int MaterialIndex,int AngleIndex,
    int Radius,CardPoint Center,CardBounds Destination);

public readonly record struct NativeFusionCardFrame(int Counter,int OrbitAngle,
    IReadOnlyList<NativeFusionMaterialPass> Materials,CardBounds? Result,
    bool ResultOverlayPending,bool DeactivateAfterDraw);

/// <summary>Separate material orbit angle at object+1C. Early 00452938..
/// 00452B91 has assembly; contraction and result (00452BE8..00452ED7)
/// are decompilation-derived candidates until the remaining window is
/// available. Count is restricted to the two/three inputs of 004520E0.
/// Caller supplies mini-bank dimensions and material/result identities.
/// No fusion rule, scheduler, or unresolved +AC overlay here.</summary>
public sealed class NativeFusionCardTimeline
{
    public const int EndCounter=640;
    public int OrbitAngle { get; private set; }
    public int MaterialCount { get; }
    public NativeFusionCardTimeline(int materialCount)
    {
        if(materialCount is not (2 or 3))
            throw new ArgumentOutOfRangeException(nameof(materialCount));
        MaterialCount=materialCount;
    }

    public NativeFusionCardFrame? Draw(int counter,int width,int height,ReadOnlySpan<int> cosine)
    {
        NativeFusionMath.Validate(cosine);
        if(counter<0)throw new ArgumentOutOfRangeException(nameof(counter));
        if(width is <1 or >4096||height is <1 or >4096)
            throw new ArgumentOutOfRangeException(nameof(width));
        if(counter>=EndCounter)
            return new(counter,OrbitAngle,[],CenteredResult(width,height),false,true);
        if(counter>=384)
            return new(counter,OrbitAngle,[],CenteredResult(width,height),counter<512,false);

        int w=width,h=height,radius=256;
        if(counter<32)
        {
            OrbitAngle=-128;
            w=width*counter/32;
            h=2*height-height*counter/32;
        }
        else if(counter>=128)
        {
            // Decompilation from 00452BE8: separate Draw mutation, not Update.
            int c=NativeFusionMath.C(cosine,(counter-128)/2);
            OrbitAngle=unchecked(OrbitAngle-1-(((65536-c)*64)>>16));
            radius=c>>8;
        }
        var materials=new NativeFusionMaterialPass[MaterialCount];
        for(int i=0;i<materials.Length;i++)
        {
            int angle=OrbitAngle+512*i/MaterialCount+(MaterialCount==2?128:0);
            int dx=unchecked(NativeFusionMath.C(cosine,angle)*radius)>>16;
            int dy=unchecked(NativeFusionMath.C(cosine,angle-128)*radius)>>16;
            var center=new CardPoint(NativeGeneralFusionEffect.Center.X+dx,
                NativeGeneralFusionEffect.Center.Y+dy);
            materials[i]=new(i,angle,radius,center,new(center.X-w/2,center.Y-h/2,w,h));
        }
        return new(counter,OrbitAngle,materials,null,false,false);
    }

    private static CardBounds CenteredResult(int width,int height)=>
        new(NativeGeneralFusionEffect.Center.X-width/2,
            NativeGeneralFusionEffect.Center.Y-height/2,width,height);
}
