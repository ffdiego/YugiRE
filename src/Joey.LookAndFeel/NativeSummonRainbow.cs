namespace Joey.LookAndFeel;

public readonly record struct NativeSummonStrip(CardBounds Source,CardBounds Destination);

/// <summary>0044FA10 second branch uses the summon bank (key zero).
/// Vertical scroll modulo effect texture height; horizontal centered crop
/// is explicit client policy until remaining ABI/assembly is recovered.</summary>
public static class NativeSummonRainbow
{
    public static NativeSummonStrip[] Strips(int scroll,int textureWidth,int textureHeight,
        int cardWidth,int cardHeight,CardPoint center)
    {
        if(scroll<0||cardWidth<=0||cardHeight<=0||textureWidth<cardWidth||textureHeight<cardHeight)
            throw new ArgumentOutOfRangeException(nameof(cardWidth));
        int offset=scroll%textureHeight, x=(textureWidth-cardWidth)/2;
        int first=Math.Min(cardHeight,textureHeight-offset);
        int left=center.X-cardWidth/2,top=center.Y-cardHeight/2;
        NativeSummonStrip a=new(new(x,offset,cardWidth,first),new(left,top,cardWidth,first));
        if(first==cardHeight)return [a];
        int second=cardHeight-first;
        return [a,new(new(x,0,cardWidth,second),new(left,top+first,cardWidth,second))];
    }
}
