using Joey.LookAndFeel;

internal static class NativeCardStatusCompositionChecks
{
    public static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        var names = new[] { NativeCardSelectionFrame.Small, NativeCardSelectionFrame.Large,
            NativeCardSelectionFrame.SmallAlternate, NativeCardSelectionFrame.LargeAlternate }
            .Concat(Enumerable.Range(4, 10).Select(i => NativeCardStatusResources.Prefix + $"test{i}.bmp")).ToArray();
        int[] columns = [0,0,0,0,6,20,6,8,4,8,0,0,0,0];
        string manifest = string.Join("\n", names.Select((path,i) =>
            path.Split('\\')[^1] + " " + columns[i]));
        var bank = NativeCardStatusResources.Parse(manifest, names.Reverse());
        check(bank.Select(entry => entry.Path).SequenceEqual(names) &&
              bank.Skip(4).Take(6).Select(entry => entry.Columns).SequenceEqual(columns.Skip(4).Take(6)),
            "V08: ordered bank with real input divisors");
        reject(() => NativeCardStatusResources.Parse(manifest.Replace("test6.bmp 6", "test6.bmp 0"), names),
            "V08: reject absent divisor rather than infer");
        reject(() => NativeCardStatusResources.Parse(manifest.Replace("test6.bmp 6", "test6.bmp 6 2"), names),
            "V08: reject ambiguous divisor");
        reject(() => NativeCardStatusResources.Parse(manifest, names.Skip(1)), "V08: missing resource");

        // Synthetic atlas dimensions, not measurements of original resources.
        NativeStatusAtlas Atlas(int i) => new(columns[i] * 9, 11, columns[i]);
        var input = new NativeCardStatusInput(0x8000, 0, new(525,352), 50,72,true,true,
            1,2,1,3,(ushort)((12<<2)|(3<<6)),NativeCardStatusTransition.First(),0);
        NativeCardStatusPass[] Sample(NativeCardStatusInput data) =>
            NativeCardStatusComposition.Compose(data, Atlas,70,10);
        var passes = Sample(input);
        check(passes.Select(p => p.IsDigit ? -1 : p.ResourceIndex)
              .SequenceEqual(new[] {5,4,8,-1,6,-1,-1,7}),
            "V08: identity then B then A; main, units/tens, transition");
        check(passes[0].Source.X == 9 && passes[1].Source.X == 18 &&
              passes[5].Source.X == 14 && passes[6].Source.X == 7,
            "V08: type/attribute and units-first digits");
        check(Sample(input with { Descriptor = 0 }).Length == 0 &&
              Sample(input with { Settled = false }).Length == 0 &&
              Sample(input with { Active = false }).Length == 0,
            "V08: parent gate hides all passes for face-down, moving or inactive");
        check(!Sample(input with { Descriptor = 0x800a }).Any(p => !p.IsDigit && p.ResourceIndex == 8),
            "V08: separate B region gate");
        // Parent returns before any status pass when region + slot > 10.
        var lastEligible = Sample(input with { Descriptor = 0x814a });
        check(lastEligible.Select(p => p.IsDigit ? -1 : p.ResourceIndex)
              .SequenceEqual(new[]{6,-1,-1,7}) &&
              Sample(input with { Descriptor = 0x818a }).Length == 0 &&
              Sample(input with { Descriptor = 0x82c0 }).Length == 0,
            "V08: region+slot limit and ordinary identity gate are independent");
        var far = Sample(input with { PerspectiveSide = 1 });
        check(far[0].Destination.X > passes[0].Destination.X &&
              far[0].Destination.Y > passes[0].Destination.Y,
            "V08: identity geometry mirrors with perspective");
        check(Sample(input with { EffectiveType = 1, EffectiveAttribute = 1, FieldFlags = 0,
              Transition = default }).Length == 0, "V08: no status draws for unchanged ordinary card");
        var b = Sample(input with { Transition = NativeCardStatusTransition.Second() });
        check(b.Any(p => !p.IsDigit && p.ResourceIndex == 9) &&
              !b.Any(p => !p.IsDigit && p.ResourceIndex == 7), "V08: state 4 selects only B tail");

        // Confirmed metadata only from original card.txt and image inventory.
        string[] actualNames = names.Take(4).Concat(new[]{
            "icon_zokusei.bmp","icon_syuzoku.bmp","pas_eff_suna.bmp","pas_eff.bmp",
            "pas_eff_skul.bmp","pas_eff2.bmp","yaji_ue1.bmp","yaji_ue2.bmp",
            "yaji_sita1.bmp","yaji_sita2.bmp"}.Select(n=>NativeCardStatusResources.Prefix+n)).ToArray();
        int[] actualColumns=[0,0,0,0,10,20,6,8,4,8,0,0,0,0];
        string actualManifest=string.Join("\n",actualNames.Select((n,i)=>
            n.Split('\\')[^1]+(actualColumns[i]>0 ? "\t"+actualColumns[i] : "")+" // fixture"));
        var actualBank=NativeCardStatusResources.Parse(actualManifest,actualNames);
        NativeCardStatusResources.ValidateEvidenceBank(actualBank);
        NativeStatusAtlas[] actualAtlases=[
            new(160,16,10),new(320,16,20),new(78,13,6),new(192,24,8),
            new(44,12,4),new(192,24,8),new(12,11,0),new(12,11,0),
            new(12,11,0),new(12,11,0)];
        for(int i=4;i<14;i++)
        {
            int index=i;
            var atlas=actualAtlases[i-4];
            NativeCardStatusResources.ValidateEvidenceAtlas(index,atlas);
            reject(()=>NativeCardStatusResources.ValidateEvidenceAtlas(index,atlas with { Width=atlas.Width+1 }),
                "V08: wrong native dimensions fail preflight");
        }
        var wrongBank=actualBank.ToArray();
        (wrongBank[6],wrongBank[8])=(wrongBank[8],wrongBank[6]);
        reject(()=>NativeCardStatusResources.ValidateEvidenceBank(wrongBank),"V08: wrong actual bank order");
        wrongBank=actualBank.ToArray();
        wrongBank[4]=wrongBank[4] with { Columns=6 };
        reject(()=>NativeCardStatusResources.ValidateEvidenceBank(wrongBank),"V08: real attribute atlas has ten columns, not six");
        reject(()=>NativeCardStatusResources.ValidateEvidenceBank(actualBank[..^1]),"V08: missing arrow variant");
        var realPasses=NativeCardStatusComposition.Compose(input,i=>actualAtlases[i-4],70,10);
        check(realPasses[0]==new NativeCardStatusPass(false,5,new(16,0,16,16),new(492,308)) &&
            realPasses[1]==new NativeCardStatusPass(false,4,new(32,0,16,16),new(542,308)),
            "V08: type and attribute positions at original atlas dimensions");
        check(realPasses[2]==new NativeCardStatusPass(false,8,new(0,0,11,12),new(528,377)) &&
            realPasses[3].Destination==new CardPoint(540,378),
            "V08: original skull frame and right-anchored digit");
        check(realPasses[4]==new NativeCardStatusPass(false,6,new(0,0,13,13),new(500,376)) &&
            realPasses[5].Destination==new CardPoint(519,378) &&
            realPasses[6].Destination==new CardPoint(512,378) &&
            realPasses[7]==new NativeCardStatusPass(false,7,new(0,0,24,24),new(507,371)),
            "V08: original hourglass, centered digits and transition frame");
    }
}
