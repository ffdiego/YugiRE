using Joey.LookAndFeel;

internal static class NativeFusionResourceChecks
{
    public static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        // Synthetic names: indices, not filenames, select the resources.
        string[] names = ["z.yga","a.bmp","b.yga","orbit.bmp"];
        string[] paths = names.Select(n => NativeFusionResources.Prefix + n).ToArray();
        string manifest = "// fixture\n" + string.Join("\r\n",names.Select(n => n + " 000001"));
        check(NativeFusionResources.Parse(manifest,paths.Reverse()).SequenceEqual(paths),
            "V11: both fusion families share the ordered resource bank");
        reject(() => NativeFusionResources.Parse(manifest,paths.Skip(1)), "V11: missing attached resource");
        reject(() => NativeFusionResources.Parse("a.bmp\nb.yga",paths), "V11: missing general resource");
        reject(() => NativeFusionResources.Parse(manifest + "\nunknown",paths), "V11: do not skip unknown records");
        int[] cosine = Enumerable.Repeat(65536,512).ToArray();
        var attached = new NativeAttachedFusionTimeline(0);
        for (int i=0;i<16;i++) attached.AdvanceNativeUpdate(false);
        check(attached.Counter == 128 && attached.Angle == 0, "V11: update does not mutate angle");
        var a = attached.Draw(true,cosine);
        attached.AdvanceNativeUpdate(false);
        var b = attached.Draw(true,cosine);
        check(a?.AngleIndex == -4 && b?.AngleIndex == -8 && attached.Counter == 128,
            "V11: separate draw mutation while waiting");
        int angle = attached.Angle;
        check(attached.Draw(false,cosine) is null && attached.Angle == angle,
            "V11: inactive associated card suppresses draw mutation");
        attached.AdvanceNativeUpdate(true);
        check(attached.Counter == 136, "V11: explicit release advances update");
    }
}
