namespace Joey.LookAndFeel;

/// <summary>Ordinary phase banner branch of 00480C60 (types 0..5).
/// The second-bank selector is the raw SETZ result at 00480C94,
/// not a claim about which absolute player the native executable calls local.</summary>
public enum NativePhaseBannerRoute { Intensity, Base, UnresolvedB0 }

/// <summary>Pass center is in logical pixels. Operand is raw intensity
/// for +10, or the confirmed low 16 bits of the +B0 argument.
/// Do not reinterpret the latter as opacity or a signed 32-bit value.</summary>
public readonly record struct NativePhaseBannerPass(
    int LookupKey, CardPoint Center, NativePhaseBannerRoute Route, int Operand);

public static class NativePhaseBannerDraw
{
    public const int EndCounter=256;
    public const int OverlayVirtualOffset=0xB0;

    public static NativePhaseBannerPass[] AtCounter(int phase,bool secondBank,
        int counter,ReadOnlySpan<int> cosine)
    {
        if(phase is <0 or >5)throw new ArgumentOutOfRangeException(nameof(phase));
        ArgumentOutOfRangeException.ThrowIfNegative(counter);
        if(cosine.Length!=512)throw new ArgumentException("Expected 512 cosine samples.",nameof(cosine));
        if(counter>=EndCounter)return [];
        int key=phase+(secondBank?6:0);
        CardPoint center=new(525,300);
        if(counter<64)
        {
            int offset=cosine[counter*2]>>10;
            return [
                new(key,new(center.X-offset,center.Y),NativePhaseBannerRoute.Intensity,3*counter),
                new(key,new(center.X+offset,center.Y),NativePhaseBannerRoute.Intensity,3*counter)];
        }
        if(counter<=128)
        {
            int frame=(counter-64)/8;
            if(phase==3&&frame<7)
                return [new(12+(secondBank?7:0)+frame,center,NativePhaseBannerRoute.Base,0)];
            // MOV AL,BL; MOV DL,FE; IMUL DL writes AX only.
            // 00480E83 pushes EAX, whose upper bits and callee ABI
            // are not established. Preserve only the known low word.
            int lowWord=unchecked((ushort)((sbyte)(byte)counter * -2));
            return [new(key,center,NativePhaseBannerRoute.Base,0),
                new(key,center,NativePhaseBannerRoute.UnresolvedB0,lowWord)];
        }
        return [new(key,center,NativePhaseBannerRoute.Intensity,cosine[counter-128]>>8)];
    }
}
