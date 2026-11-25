namespace Joey.LookAndFeel;

public readonly record struct NativeFusionParticle(int Key,int Group,int Age,
    int AngleIndex,int Radius,CardPoint Center,CardBounds Destination);

public readonly record struct NativeGeneralFusionEffectFrame(int Counter,
    NativeFusionSprite Primary,NativeFusionSprite? Additional,
    IReadOnlyList<NativeFusionParticle> Particles,uint? SoundRequest);

/// <summary>004524F0 assembly up to 00452938: effect bank only.
/// Material miniatures, result card and scratch/background composition
/// are deliberately separate. Center and selector from 005DB278/005DB280.</summary>
public static class NativeGeneralFusionEffect
{
    public static CardPoint Center => new(400,300);
    public const int EffectEndCounter=448;
    private static readonly int[] Keys=[3,4,5,4,3,0,6,7,8,7,6,0];

    public static NativeFusionParticle[] Particles(int counter,int drawAngle,
        ReadOnlySpan<int> cosine,Func<int,CardBounds> resource)
    {
        NativeFusionMath.Validate(cosine);
        ArgumentNullException.ThrowIfNull(resource);
        if(counter<0)throw new ArgumentOutOfRangeException(nameof(counter));
        if(counter>=EffectEndCounter)return [];
        List<NativeFusionParticle> result=[];
        int group=Math.Min(counter&~31,256);
        int age=counter-group;
        // 00452922..00452932: age += 32 (NOT the +8 shown
        // in one decompilation). Four angular samples per group.
        for(;group>0;group-=32,age+=32)
        {
            if(age>=128)continue;
            int selector=counter/4-group+counter;
            int radius=NativeFusionMath.C(cosine,age)>>7;
            for(int j=0;j<4;j++,selector++)
            {
                int key=Keys[selector%12];
                if(key==0)continue;
                int angle=j*128+2*group-counter+drawAngle;
                int x=Center.X+(unchecked(NativeFusionMath.C(cosine,angle)*radius)>>16);
                int y=Center.Y+(unchecked(NativeFusionMath.C(cosine,angle-128)*radius)>>16);
                var rect=resource(key);
                if(rect.Width<=0||rect.Height<=0)
                    throw new InvalidDataException("Invalid fusion particle dimensions.");
                result.Add(new(key,group,age,angle,radius,new(x,y),
                    new(x-rect.Width/2,y-rect.Height/2,rect.Width,rect.Height)));
            }
        }
        return result.ToArray();
    }

    /// <summary>One actual Draw of the recovered effects layer. Angle
    /// advances once in the timeline; particles reuse that mutated angle.
    /// A review may stop at 448; the native object continues other passes.</summary>
    public static NativeGeneralFusionEffectFrame? Draw(NativeGeneralFusionTimeline timeline,
        ReadOnlySpan<int> cosine,Func<int,CardBounds> resource)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        if(timeline.Counter>=EffectEndCounter)return null;
        var frame=timeline.Draw(cosine);
        NativeFusionSprite? additional=frame.AdditionalScale is int scale &&
            frame.AdditionalIntensity is int intensity
            ? new(2,scale,intensity,timeline.Angle):null;
        return new(timeline.Counter,new(frame.PrimaryResourceIndex,frame.PrimaryScale,
            frame.PrimaryIntensity,timeline.Angle),additional,
            Particles(timeline.Counter,timeline.Angle,cosine,resource),
            timeline.Counter switch {4=>0x10000020u,384=>0x10000021u,_=>(uint?)null});
    }
}
