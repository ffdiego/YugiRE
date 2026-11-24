using Joey.LookAndFeel;

internal static class NativeSelectionManifestChecks
{
    public static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        // Resource metadata from native-selection-dat-contract; no image
        // pixels or full manifest included. Fixture uses the documented schema.
        string[] names = [@"j\duel\list\list_select0.bmp",
            @"j\duel\list\list_select1.bmp", @"j\duel\list\select_scroll.txt"];
        string manifest = "// fixture\nlist_select0.bmp 000095\nlist_select1.bmp 000083\nselect_scroll.txt 150\n";
        var parsed = NativeSelectionManifest.Parse(manifest, names.Reverse());
        check(parsed.Background(5).Resource == names[0] && parsed.Background(6).Resource == names[1] &&
              parsed.Background(5).SlotYOffset == 95 && parsed.Background(6).SlotYOffset == 83 &&
              parsed.ScrollAuxiliary == 150, "V11: preserve parent entry order and select scroll variant");
        check(NativeSelectionManifest.SideVariant(0,0) == 1 &&
              NativeSelectionManifest.SideVariant(1,1) == 1 &&
              NativeSelectionManifest.SideVariant(0,1) == 2 &&
              NativeSelectionManifest.SideVariant(1,0) == 2, "V11: constructor suffix by perspective");
        check(NativeSelectionManifest.BackgroundBounds(451,191) == new CardBounds(300,205,451,191),
            "V11: integer centered stable background");
        check(NativeSelectionManifest.SlotBounds(0,191,95,50,72) == new CardBounds(372,264,50,72) &&
              NativeSelectionManifest.SlotBounds(4,191,83,50,72) == new CardBounds(628,252,50,72),
            "V11: DAT slot offset, exact 64px spacing and mini bounds");
        reject(() => NativeSelectionManifest.Parse(manifest,names.Skip(1)), "V11: missing background");
        reject(() => NativeSelectionManifest.Parse(manifest.Replace("000083","invalid"),names), "V11: unknown offset");
        reject(() => NativeSelectionManifest.Parse(manifest.Replace("select_scroll.txt","../select_scroll.txt"),names), "V11: unsafe record");
        var list = new NativeSelectionWindow(8);
        check(!list.FocusVisibleSlot(2), "V11: no input during entrance");
        list.AdvanceOpening(8);
        check(list.FocusVisibleSlot(4) && list.MoveNext() &&
            list.ScrollOffset == 1 && list.SelectedCardIndex == 5 &&
            list.CardIndexAtSlot(0) == 1 && list.CardIndexAtSlot(4) == 5 &&
            list.FocusVisibleSlot(1) && list.SelectedCardIndex == 2,
            "V11: visible click maps to absolute index after scroll");
        check(!list.FocusVisibleSlot(5), "V11: no sixth slot");
        var empty = new NativeSelectionWindow(0);
        empty.AdvanceOpening(8);
        check(!empty.FocusVisibleSlot(0) && empty.SelectedCardIndex == -1, "V11: empty list");
    }
}
