using Joey.LookAndFeel;

internal static class NativeCoinExtraChecks
{
    public static void Run(Action<bool,string> check)
    {
        int[] cosine=NativeCosineCapture.GenerateTable();
        NativeDiceTexture Texture(int key)=>key switch
        {
            0=>new(560,40,14),1=>new(168,42,4),2=>new(64,64,1),
            3=>new(40,40,1),4=>new(69,40,1),5=>new(56,14,4),
            _=>throw new InvalidOperationException()
        };
        NativeCoinExtras At(int counter,int count=1,int index=0,uint bits=0,int highlight=0)
        {
            var timeline=new NativeCoinTimeline(count,bits,new(400,100));
            while(timeline.Counter<counter)timeline.AdvanceNativeUpdate();
            var primary=NativeCoinPassLayout.Sample(timeline,index,highlight,cosine,Texture);
            return NativeCoinExtraLayout.Sample(timeline,index,primary,Texture);
        }
        var first=At(320);
        check(first.Reflection is { } r && r.CopyOrigin==new CardPoint(-69,0) &&
            r.Scratch==new CardBounds(0,0,40,40) && r.Destination==new CardPoint(505,280)&&
            r.MaskSlot==0x94&&r.CompositeSlot==0xa0&&first.Sparkles.Count==0,
            "V20: reflection begins at remaining 320");
        var overlap=At(384);
        check(overlap.Reflection is { } r2 && r2.CopyOrigin==new CardPoint(-15,0) &&
            overlap.Sparkles.Count==1 && overlap.Sparkles[0].Source==new CardBounds(42,0,14,14)&&
            overlap.Sparkles[0].Destination==new CardBounds(498,273,14,14),
            "V20: reflection and first sparkle coexist");
        var later=At(440);
        check(later.Reflection is { } r3 && r3.CopyOrigin==new CardPoint(33,0)&&
            later.Sparkles.Count==4&&later.Sparkles.Select(p=>p.Source.X).SequenceEqual(new[]{42,14,0,28}),
            "V20: late reflection with four independently timed sparkles");
        var boundary=At(448);
        check(boundary.Reflection is null&&boundary.Sparkles.Count==4&&
            boundary.Sparkles[0].Source.X==28,
            "V20: reflection ends while sparkles continue");
        check(At(512).Sparkles.Count==0&&At(384,bits:1).Sparkles.Count==0&&
            At(384,bits:1).Reflection is null,
            "V20: sparkle endpoint and highlight gate");
        // At global counter 640, remaining 256 for three coins.
        // Second coin has already landed: target 525,300, index=1.
        var middle=At(640,3,1);
        check(middle.Sparkles.Count==1&&middle.Sparkles[0].Destination==new CardBounds(498,283,14,14),
            "V20: second coin phase offsets sparkle vertically");
        check(At(640,3,2).Sparkles[0].Destination==new CardBounds(598,293,14,14),
            "V20: third coin offset and global sparkle clock");
    }
}
