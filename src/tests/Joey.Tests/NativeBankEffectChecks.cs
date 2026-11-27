using Joey.LookAndFeel;

internal static class NativeBankEffectChecks
{
    public static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        int[] cosine=NativeCosineCapture.GenerateTable();
        check(NativeEffectCommandRouting.CoinArguments(0x80e0,0,0,7)==(1,1u,1,(ushort)0x800f) &&
              NativeEffectCommandRouting.CoinArguments(0xe0,4,1,2)==(1,0u,0,(ushort)0x8004) &&
              NativeEffectCommandRouting.CoinArguments(0xe1,1,2,6)==(3,5u,0,(ushort)0x800c),
              "V20: E0/E1 decode raw constructor count, low bits, highlight and region D");
        foreach(ushort op in new ushort[]{0xe2,0xe3,0xe4})
            check(NativeEffectCommandRouting.DiceArguments((ushort)(op|0x8000),6,9,1)==(6,(ushort)0x8013),
                "V21: raw dice index B and region C");
        check(NativeDiceResources.ValidateTexture(0,new("dice_random.bmp",6),270,46).PatternWidth==45 &&
              NativeDiceResources.ValidateTexture(3,new("dice_shadow2.yga",4),152,43).PatternWidth==38,
            "V21: original DAT strip dimensions");
        reject(()=>NativeDiceResources.ValidateTexture(3,new("dice_shadow2.yga",4),151,43),
            "V21: reject wrong DAT geometry");
        NativeDiceTexture Tex(int key)=>key<6 ? new(160,40,4) :
            key==6 ? new(41,44,1) : new(32,35,1);
        for(int face=1;face<=6;face++)
        for(int side=0;side<2;side++)
        {
            var d=new NativeDiceTimeline(face,new(369,side==0?460:140));
            int n=0;
            while(d.Active)
            {
                check(d.BlocksRelease,"V21: active blocks release");
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
            check(n==128 && !d.BlocksRelease && d.Counter==768 && d.Draw(cosine,Tex) is null,
                "V21: 128 updates, inactive at 768");
        }
        var repeat=new NativeDiceTimeline(1,new(369,460));
        for(int i=0;i<33;i++) repeat.AdvanceNativeUpdate();
        var first=repeat.Draw(cosine,Tex)!.Value;
        var second=repeat.Draw(cosine,Tex)!.Value;
        check(first.Center!=second.Center && first.Counter==second.Counter,
            "V21: repeated Draw at one counter is not a no-op");
        foreach(int invalid in new[]{0,7})
        {
            bool rejected=false;
            try { _=new NativeDiceTimeline(invalid,new(525,300)); }
            catch(ArgumentOutOfRangeException) { rejected=true; }
            check(rejected,"V21: unsupported native index");
        }
        reject(()=>new NativeDiceTexture(41,40,4).Pattern(0),"V21: invalid strip dimensions");
        // Synthetic manifest adapter test; the real file is private.
        string[] entries=["dice_random.bmp","dice_shadow.yga",
            "dice_ani_2453.bmp","dice_shadow2.yga",
            "dice_ani_5621.bmp","dice_shadow2.yga","dice_dci_sha.yga",
            "dice_decide1.bmp","dice_decide2.bmp","dice_decide3.bmp",
            "dice_decide4.bmp","dice_decide5.bmp","dice_decide6.bmp"];
        string[] names=entries.Select(x=>NativeDiceResources.Prefix+x).ToArray();
        string manifest=string.Join("\n",entries.Select((x,i)=>i<6?x+" 4":x));
        var bank=NativeDiceResources.Parse(manifest,names);
        check(bank.Length==13 && bank[3].Path==bank[5].Path &&
              bank[1].Parameter==4 && bank[7].Parameter is null,
              "V21: manifest adapter preserves ordered duplicates and metadata");
        reject(()=>NativeDiceResources.Parse(manifest,names[..^1]),"V21: missing face");
        reject(()=>NativeDiceResources.Parse(manifest.Replace("dice_random.bmp 4","dice_random.bmp 0"),names),
            "V21: zero strip divisor");
        reject(()=>NativeDiceResources.Parse(manifest.Replace("dice_decide1.bmp","dice_decide2.bmp"),names),
            "V21: reject unconfirmed final key ordering");

        foreach(int count in new[]{1,3})
        {
            var coin=new NativeCoinTimeline(count,5,new(369,460));
            int n=0;
            while(coin.Active)
            {
                check(coin.BlocksRelease,"V20: active blocks release");
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
            check(n==(count==1?96:128) && !coin.BlocksRelease,"V20: single/triple terminal counter");
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
