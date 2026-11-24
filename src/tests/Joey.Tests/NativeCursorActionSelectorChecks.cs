using Joey.LookAndFeel;

internal static class NativeCursorActionSelectorChecks
{
    public static void Run(Action<bool,string> check, Action<Action,string> reject)
    {
        // Deliberately non-native order: the real table is not yet extracted.
        int[] fixture = [6, 5, 2, 0, 12, 3, 4, 1, 7, 8, 9, 10, 11];
        var selector = new NativeCursorActionSelector(fixture);
        check(selector.First(0) is null && selector.Next(0,6) is null,
            "V10: no action for empty mask");
        ushort pair = (1 << 5) | (1 << 6);
        check(selector.First(pair)==6 && selector.Next(pair,6)==5 &&
            selector.Next(pair,5)==6,"V10: caller-provided priority and wrap");
        check(selector.First(1<<5)==5 && selector.Next(1<<5,5)==5,
            "V10: single legal action cannot toggle to another");
        check(selector.Next(1<<5,6)==5,"V10: defensive stale-selection reset");
        check(selector.First(0x8000) is null,"V10: unknown mask bits do not expose actions");
        for(int action=0;action<12;action++)
            check(NativeCursorActionSelector.ObjectRequest(action)==action+1,
                "V10: raw object request is not a texture index");
        check(NativeCursorActionSelector.ObjectRequest(null)==0 &&
            NativeCursorActionSelector.ObjectRequest(12)==0,
            "V10: reset and special action both write zero");
        reject(()=>new NativeCursorActionSelector(new int[13]),"V10: duplicate table codes rejected");
        reject(()=>new NativeCursorActionSelector(fixture[..12]),"V10: incomplete table rejected");
    }
}
