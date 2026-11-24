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
        var far = Sample(input with { PerspectiveSide = 1 });
        check(far[0].Destination.X > passes[0].Destination.X &&
              far[0].Destination.Y > passes[0].Destination.Y,
            "V08: identity geometry mirrors with perspective");
        check(Sample(input with { EffectiveType = 1, EffectiveAttribute = 1, FieldFlags = 0,
              Transition = default }).Length == 0, "V08: no status draws for unchanged ordinary card");
        var b = Sample(input with { Transition = NativeCardStatusTransition.Second() });
        check(b.Any(p => !p.IsDigit && p.ResourceIndex == 9) &&
              !b.Any(p => !p.IsDigit && p.ResourceIndex == 7), "V08: state 4 selects only B tail");
    }
}
