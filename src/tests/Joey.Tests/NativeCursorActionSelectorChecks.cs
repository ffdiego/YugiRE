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

        // Exercise the legal presentation adapter independently of the raw
        // native action numbers, which still require the original dispatcher.
        foreach (bool own in new[]{false,true})
        foreach (bool allowed in new[]{false,true})
        foreach (bool hidden in new[]{false,true})
        foreach (bool defense in new[]{false,true})
        foreach (bool target in new[]{false,true})
        {
            NativeCursorAction? expected = !own || !allowed || target ? null :
                hidden ? NativeCursorAction.Flip :
                defense ? NativeCursorAction.SetAttack : NativeCursorAction.SetDefense;
            var selected = NativeDuelCursor.FieldMonsterPositionAction(own,allowed,hidden,defense,target);
            check(selected==expected &&
                  NativeDuelCursor.ActionForSprite(NativeDuelCursor.CursorSprite(selected))==expected,
                "V10: legal position cloud and diagnostic agree; one pointer only");
        }
        // Original English manifest order and raw fields (DAT metadata).
        string[] names = ["cursor_pow.yga","cursor00.yga","cursor01.yga",
            "cursor_e_summon.yga","cursor_e_flip.yga","cursor_e_atk.yga","cursor_e_act.yga",
            "cursor_e_set.yga","cursor_e_setdef.yga","cursor_e_setatk.yga","cursor_e_draw.yga",
            "cursor_e_surrender.yga","cursor_e_fusion.yga","cursor_ssummon_a.yga",
            "cursor_ssummon_d.yga","cursor_e_over.yga"];
        string[] paths = names.Select(n=>@"cursor\"+n).ToArray();
        string manifest = string.Join("\n",names.Select((n,i)=>n.Replace("cursor_e_","cursor_?_")+
            (i==0 ? "" : i==11 ? "\t043015" : i is 13 or 14 ? "\t007030" : "\t007014")+
            " // fixture"));
        var bank=NativeCursorManifest.ParseEnglish(manifest,paths);
        check(NativeCursorManifest.ResourceIndex(null) == 1 &&
              NativeCursorManifest.ResourceIndex(12, alternatePointer: true) == 2 &&
              Enumerable.Range(0,12).All(code => NativeCursorManifest.ResourceIndex(code) == code + 3),
            "V10: assembly-backed request-to-manifest index");
        check(bank[NativeCursorManifest.ResourceIndex(5)].Path == NativeDuelCursor.ActionSprite(NativeCursorAction.SetDefense) &&
              bank[NativeCursorManifest.ResourceIndex(6)].Path == NativeDuelCursor.ActionSprite(NativeCursorAction.SetAttack) &&
              bank[NativeCursorManifest.ResourceIndex(1)].Path == NativeDuelCursor.ActionSprite(NativeCursorAction.Flip),
            "V10: manifest confirms position and Flip action code mapping");
        check(bank[8].Hotspot == new CardPoint(7,14) && bank[11].Hotspot == new CardPoint(43,15) &&
              bank[13].Hotspot == new CardPoint(7,30) &&
              bank[8].OriginAt(new(375,450), 1.5f) == new System.Numerics.Vector2(364.5f,429),
            "V10: native hotspots are subtracted at the client's display scale");
        var recovered = new NativeCursorActionSelector();
        check(recovered.First(0x1fff) == 2 && recovered.Next(0x1fff, 2) == 10 &&
              recovered.Next(0x1fff, 9) == 2, "V10: recovered priority table and wrap");
        check(bank.Length==16 && bank[0].RawParameter is null &&
              bank[5].Path==NativeDuelCursor.ActionSprite(NativeCursorAction.Attack) &&
              bank[8].Path==NativeDuelCursor.ActionSprite(NativeCursorAction.SetDefense) &&
              bank[9].Path==NativeDuelCursor.ActionSprite(NativeCursorAction.SetAttack) &&
              bank[11].RawParameter=="043015" && bank[13].RawParameter=="007030",
            "V09/V10: manifest order, raw parameters, and real resources");
        for(int i=0;i<16;i++)
        {
            int index=i, size=i==0?63:i is 13 or 14?80:64;
            NativeCursorManifest.ValidateDimensions(index,size,size);
            reject(()=>NativeCursorManifest.ValidateDimensions(index,size+1,size),
                "V09/V10: incorrect original cursor dimensions rejected");
        }
        reject(()=>NativeCursorManifest.ParseEnglish(manifest.Replace("007014","7014"),paths),
            "V09/V10: preserve raw leading zeros rather than interpreting the field");
        reject(()=>NativeCursorManifest.ParseEnglish(manifest,paths.Skip(1)),
            "V09/V10: missing cursor resource rejected");
        reject(()=>NativeCursorManifest.ParseEnglish(manifest.Replace("cursor_?_atk","cursor_?_act"),paths),
            "V09/V10: wrong cursor bank order rejected");
    }
}
