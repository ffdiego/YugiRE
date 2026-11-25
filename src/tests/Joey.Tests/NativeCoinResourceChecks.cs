using Joey.LookAndFeel;

internal static class NativeCoinResourceChecks
{
    internal static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        // Synthetic manifest syntax with the six resource names/divisors
        // observed in private v3; no original asset payload or comments.
        const string fixture="coin_rotate.bmp 14 // strip\r\ncoin_sha.yga 4\r\ncoin_ripple.yga\r\ncoin_mask.bmp\r\ncoin_ref.bmp\r\ncoin_kirari.yga 4\r\n";
        string[] names=["coin_rotate.bmp","coin_sha.yga","coin_ripple.yga","coin_mask.bmp",
            "coin_ref.bmp","coin_kirari.yga"];
        var paths=names.Select(n=>NativeCoinResources.Prefix+n).ToArray();
        var resources=NativeCoinResources.Parse(fixture,paths);
        check(resources.Length==6&&resources.Select(r=>r.Patterns).SequenceEqual(new[]{14,4,1,1,1,4}),
            "V20: coin resource order and DAT strip divisors");
        var textures=resources.Select(r=>NativeCoinResources.ValidateTexture(r,r.Width,r.Height)).ToArray();
        check(textures[0].PatternWidth==40&&textures[1].PatternWidth==42&&
            textures[2].Whole==new CardBounds(0,0,64,64)&&textures[5].PatternWidth==14,
            "V20: observed atlas dimensions");
        reject(()=>NativeCoinResources.Parse(fixture.Replace("14 //","13 //"),paths),
            "V20: reject unexpected divisor");
        reject(()=>NativeCoinResources.Parse(fixture,paths[..^1]),
            "V20: reject missing original resource");
        reject(()=>NativeCoinResources.Parse(fixture.Replace("coin_mask.bmp","coin_ref.bmp"),paths),
            "V20: reject out-of-order resource");
        reject(()=>NativeCoinResources.ValidateTexture(resources[0],559,40),
            "V20: reject unexpected texture dimension");
        check(NativeCoinResources.IsCoinFaceBitmap(paths[0])&&!NativeCoinResources.IsCoinFaceBitmap(paths[3]),
            "V20: diagnostic face key excludes mask");
        // Full 96/128-update timelines using dimensions from v3; synthetic
        // cosine deliberately not labelled a native capture.
        int[] cosine=Enumerable.Range(0,512).Select(i=>(int)(65536*Math.Cos(i*Math.PI/256))).ToArray();
        foreach(int count in new[]{1,3})
        foreach(uint bits in new[]{0u,(1u<<count)-1})
        {
            var timeline=new NativeCoinTimeline(count,bits,new CardPoint(400,100));
            int updates=0,sound=0,halo=0;
            bool bounded=true;
            while(timeline.Active)
            {
                for(int i=0;i<count;i++)
                {
                    var frame=NativeCoinPassLayout.Sample(timeline,i,0,cosine,k=>textures[k]);
                    if(frame.Coin.SoundRequest.HasValue)sound++;
                    foreach(var pass in frame.Passes)
                    {
                        var texture=textures[pass.Key];
                        bounded &= pass.Source.X>=0&&pass.Source.Y>=0&&
                            pass.Source.X+pass.Source.Width<=texture.Width&&
                            pass.Source.Y+pass.Source.Height<=texture.Height&&
                            pass.Parameter256 is >=0 and <=256;
                        if(pass.Key==2)halo++;
                    }
                }
                timeline.AdvanceNativeUpdate();updates++;
            }
            check(updates==(count==1?96:128)&&sound==count&&halo==8*count&&bounded,
                $"V20: full coin review sample sequence count={count} bits={bits}");
        }
    }
}
