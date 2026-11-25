using Joey.LookAndFeel;

internal static class NativeCoinPassChecks
{
    public static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        int[] c=NativeCosineCapture.GenerateTable();
        NativeDiceTexture Texture(int key)=>key switch
        {
            0=>new(14*40,44,14),1=>new(4*30,20,4),2=>new(50,70,1),
            _=>throw new InvalidOperationException("Unknown key")
        };
        NativeCoinDraw At(int counter,uint bit=0,int highlight=0)
        {
            var coin=new NativeCoinTimeline(1,bit,new(525,300));
            while(coin.Counter<counter)coin.AdvanceNativeUpdate();
            return NativeCoinPassLayout.Sample(coin,0,highlight,c,Texture);
        }
        check(At(-128).Passes.Count==0,"V20: launch boundary invisible");
        var pre=At(-120);
        check(pre.Passes.Count==2 && pre.Passes[0].Parameter256==32 &&
            pre.Passes[0].Source==new CardBounds(0,0,30,20) &&
            pre.Passes[0].Destination==new CardBounds(511,286,30,20) &&
            pre.Passes[1].Source==new CardBounds(240,0,40,44),
            "V20: stationary shadow +1/width anchor and initial face");
        check(At(-64).Passes[0].Parameter256==256 &&
            At(0).Coin.SoundRequest==0x1000001d &&
            At(0).Passes[0].VirtualSlot==0x48,
            "V20: flat at local zero, sound independent of passes");
        var a=At(128);var b=At(128,1);
        check(a.Passes[1].Source.X==9*40&&b.Passes[1].Source.X==13*40&&
            a.Passes[0].Source.X==3*30&&b.Passes[0].Source.X==0,
            "V20: strip selector and shadow table use supplied bit");
        var coin=a.Coin;int w=40*coin.ScaleFixed8/256,h=44*coin.ScaleFixed8/256;
        check(a.Passes[0].VirtualSlot==0x4c&&a.Passes[1].Destination==
            new CardBounds(525-h/2,300-h/2-coin.Bounce*50/256,w,h),
            "V20: rotating coin uses scaled height for both anchors");
        var landing=At(256);
        check(landing.Passes.Count==3&&landing.Passes[1].Source.X==13*40&&
            At(256,1).Passes[1].Source.X==6*40&&
            landing.Passes[2].Destination==new CardBounds(500,265,50,70)&&
            landing.Passes[2].Parameter256==256,
            "V20: stationary outcome and halo entry");
        check(At(288).Passes[2].Destination==new CardBounds(494,257,62,87)&&
            At(288).Passes[2].Parameter256==128&&At(320).Passes.Count==2,
            "V20: halo size and parameter separate, ends at 320");
        check(At(320).ReflectionScratchRequired&&At(440).ReflectionScratchRequired&&
            !At(448).ReflectionScratchRequired&&At(448).SparkleBranchReached&&
            !At(320,1,0).ReflectionScratchRequired,
            "V20: highlight uses bit and global remaining window");
        check(At(576).Passes[0].Parameter256==256&&At(584).Passes[0].Parameter256==224&&
            At(632).Passes[1].Parameter256==32,
            "V20: global end fade");
        var triple=new NativeCoinTimeline(3,5,new(369,460));
        for(int i=0;i<17;i++)triple.AdvanceNativeUpdate();
        check(NativeCoinPassLayout.Sample(triple,0,0,c,Texture).Passes.Count==2&&
            NativeCoinPassLayout.Sample(triple,1,0,c,Texture).Passes.Count==2&&
            NativeCoinPassLayout.Sample(triple,2,0,c,Texture).Passes.Count==0,
            "V20: staggered visibility, independent result bits");
        reject(()=>NativeCoinPassLayout.Sample(triple,0,0,c,_=>new(40,44,1)),
            "V20: fail closed on unconfirmed strip divisor");
    }
}
