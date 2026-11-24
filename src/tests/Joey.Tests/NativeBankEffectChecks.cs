using Joey.LookAndFeel;

internal static class NativeBankEffectChecks
{
    public static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        int[] cosine=ActivationEffectPlayback.ApproximateCosineTable();
        NativeDiceTexture Tex(int key)=>key<6 ? new(160,40,4) :
            key==6 ? new(41,44,1) : new(32,35,1);
        for(int face=1;face<=6;face++)
        for(int side=0;side<2;side++)
        {
            var d=new NativeDiceTimeline(face,new(369,side==0?460:140));
            int n=0;
            while(d.Active)
            {
                int t=d.Counter;
                var before=d.Center;
                var preview=d.Sample(cosine,Tex)!.Value;
                check(d.Center==before,"V21: Sample is pure");
                var draw=d.Draw(cosine,Tex)!.Value;
                check(draw==preview && d.Center==draw.Center,"V21: Draw commits only center");
                int key=t>=448 ? face+6 : t<=256 ? 0 : face is 3 or 4 or 5 ? 2 : 4;
                check(draw.Die.Key==key && draw.Shadow.Key==(t>=448?6:key+1),
                    "V21: draw bank and shadow order");
                check(draw.SoundRequest==(t is 256 or 384 ? (uint?)0x1000001c:null),
                    "V21: sound is a Draw request at exact counters");
                if(t>=448)
                    check(draw.Center==new CardPoint(525,300) &&
                          draw.Die.Destination==new CardBounds(509,284,32,35) &&
                          draw.Shadow.Destination==new CardBounds(509,284,41,44) &&
                          draw.Die.Intensity==(t<=704?256:(768-t)*4),
                        "V21: final shared width-based anchor and fade");
                if(t==-256)
                    check(draw.ScaleFixed8==0 && draw.Die.Destination.Width==0,
                        "V21: starting scale is zero");
                if(t==384)
                    check(draw.Center==new CardPoint(525,side==0?364:236),
                        "V21: recurrence reaches intermediate target");
                d.AdvanceNativeUpdate();
                n++;
            }
            check(n==128 && d.Counter==768 && d.Draw(cosine,Tex) is null,
                "V21: 128 updates, inactive at 768");
        }
        var repeat=new NativeDiceTimeline(1,new(369,460));
        for(int i=0;i<33;i++) repeat.AdvanceNativeUpdate();
        var first=repeat.Draw(cosine,Tex)!.Value;
        var second=repeat.Draw(cosine,Tex)!.Value;
        check(first.Center!=second.Center && first.Counter==second.Counter,
            "V21: repeated Draw at one counter is not a no-op");
        reject(()=>new NativeDiceTimeline(0,new(525,300)),"V21: unsupported native index zero");
        reject(()=>new NativeDiceTimeline(7,new(525,300)),"V21: unsupported native index seven");
        reject(()=>new NativeDiceTexture(41,40,4).Pattern(0),"V21: invalid strip dimensions");

        foreach(int count in new[]{1,3})
        {
            var coin=new NativeCoinTimeline(count,5,new(369,460));
            int n=0;
            while(coin.Active)
            {
                for(int i=0;i<count;i++)
                {
                    var sample=coin.Sample(i,cosine);
                    check(sample.LocalCounter==coin.Counter-128*i &&
                          sample.ResultBit==((5&(1<<i))!=0) &&
                          sample.SoundRequest==(sample.LocalCounter==0?(uint?)0x1000001d:null),
                        "V20: stagger, supplied bits and sound");
                    if(sample.LocalCounter<0)check(sample.Center==coin.Origin,"V20: prelaunch origin");
                    if(sample.LocalCounter>=256)check(sample.Center==new CardPoint(525+(100-100*count)/2+100*i,300),
                        "V20: centered destinations 100 pixels apart");
                }
                coin.AdvanceNativeUpdate();
                n++;
            }
            check(n==(count==1?96:128),"V20: single/triple terminal counter");
        }

        var b=new NativeBreakTimeline();
        var c=new CardPoint(525,352);
        check(b.Particles(c,21,17,cosine).All(p=>p.Bounds==new CardBounds(515,344,21,17) &&
            p.Intensity==255),"V22: five initial passes at common center");
        for(int i=0;i<8;i++)b.AdvanceNativeUpdate();
        var particles=b.Particles(c,21,17,cosine);
        check(particles.Length==5 && particles[0].Bounds==new CardBounds(531,344,21,17) &&
              particles.All(p=>p.Intensity==127),"V22: radius t/8, intensity 255-t");
        check(b.DistortionIntensity(cosine)==256-(cosine[448]>>8),
            "V22: distinct cosine-driven distortion parameter");
        for(int i=0;i<8;i++)b.AdvanceNativeUpdate();
        check(!b.Active && b.Particles(c,21,17,cosine).Length==0,"V22: inactive at 256");
        for(int perspective=0;perspective<2;perspective++)
        for(int side=0;side<2;side++)
        for(int defense=0;defense<2;defense++)
        {
            ushort descriptor=(ushort)(0x8000|defense<<14|side);
            check(NativeBreakTimeline.CardRotationDegrees(descriptor,perspective)==
                  (defense+2*(perspective^side))*90,"V22: exact rotation helper selector");
        }
    }
}
