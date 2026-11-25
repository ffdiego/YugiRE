namespace Joey.LookAndFeel;

/// <summary>Client sampling policy: one Draw per Update, including sample
/// zero and the candidate terminal sample 640. No Core or rendering calls.</summary>
public sealed class NativeFusionPlayback
{
    public const int LastSample=NativeFusionCardTimeline.EndCounter/NativeGeneralFusionTimeline.UpdateIncrement;
    public const int Updates=LastSample+1;
    readonly NativeGeneralFusionTimeline effect=new(0);
    readonly NativeFusionCardTimeline cards;
    public int Sample { get; private set; }=-1;
    public NativeGeneralFusionEffectFrame? Effect { get; private set; }
    public NativeFusionCardFrame? Cards { get; private set; }
    public NativeFusionPlayback(int materialCount)=>cards=new(materialCount);

    public void AdvanceTo(int sample,ReadOnlySpan<int> cosine,Func<int,CardBounds> resource,
        Action<uint>? sound=null)
    {
        if(sample<Sample||sample>LastSample||sample<0)
            throw new ArgumentOutOfRangeException(nameof(sample));
        while(Sample<sample)
        {
            if(Sample>=0)effect.AdvanceNativeUpdate();
            Sample++;
            Effect=NativeGeneralFusionEffect.Draw(effect,cosine,resource);
            Cards=cards.Draw(effect.Counter,50,72,cosine);
            if(Effect?.SoundRequest is uint request)sound?.Invoke(request);
        }
    }
}
