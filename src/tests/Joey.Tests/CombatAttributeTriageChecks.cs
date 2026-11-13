using System.Text.Json.Nodes;
using Joey.Core;
using Joey.Lab;

internal static class CombatAttributeTriageChecks
{
    private const string PinnedExeSha256 = "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de";

    private static CaptureDocument Capture(bool hidden = false, int links = 0,
        bool corruptedEsi = false, bool changedDefender = false, bool wrongSchema = false,
        bool missingEsi = false)
    {
        string own = "18000F05A40600007E040000";
        string rival = hidden ? "00000000E8030000E8030000" :
            changedDefender ? "260212057007000084030000" : "260212056C07000084030000";
        int position = hidden ? 1 : 2;
        var rows = new List<JsonNode>
        {
            JsonNode.Parse("""{"kind":"ai_probe","function":"select_normal","phase":"enter","threadId":7,"callId":1,"side":0}""")!,
            JsonNode.Parse($$"""
            {"kind":"combat_context","schemaVersion":{{wrongSchema ? 2 : 3}},"session":1,"threadId":7,
             "combatCallId":2,"selectorCallId":1,"callId":2,"function":"combat_event",
             "phase":"enter","stateKind":"selector-window","argsRaw":[0,0,0],"seedHex":"00000000",
             "provenance":"F40-entry-snapshot","consumedPositionRaw":null,
             "consumedDescriptorSeedRaw":null,"consumedFlagsRaw":null,"readErrors":[],
             "attacker":{"side":0,"slot":0,"identityRaw":24,"internalCardIndex":24,
               "positionRaw":2,"defensePosition":false,"faceUp":true,
               "linkCountRaw":{{links}},"monsterFlagsRaw":0,"playerFlagsRaw":0},
             "defender":{"side":1,"slot":0,"identityRaw":550,"internalCardIndex":550,
               "positionRaw":{{position}},"defensePosition":{{hidden ? "true" : "false"}},
               "faceUp":{{hidden ? "false" : "true"}},
               "linkCountRaw":0,"monsterFlagsRaw":0,"playerFlagsRaw":0}}
            """)!,
            JsonNode.Parse($$"""
            {"kind":"combat_context","schemaVersion":{{wrongSchema ? 2 : 3}},"session":1,"threadId":7,
             "combatCallId":2,"selectorCallId":1,"callId":3,"function":"own_stats",
             "phase":"leave","argsRaw":[0,0,100],"caller":"0x5b0f97","outputHex":"{{own}}",
             "provenance":"wrapper-boundary-snapshot",
             "esiExpectedRaw":3396,"esiAtEnterRaw":3396,"esiAtLeaveRaw":{{corruptedEsi ? 141603343 : 3396}},
             "espAtEnterRaw":4096,"espAtLeaveRaw":4096,"esiPreserved":{{corruptedEsi ? "false" : "true"}},
             "positionBeforeRaw":2,"positionAfterRaw":2}
            """)!,
            JsonNode.Parse($$"""
            {"kind":"combat_context","schemaVersion":{{wrongSchema ? 2 : 3}},"session":1,"threadId":7,
             "combatCallId":2,"selectorCallId":1,"callId":4,"function":"opponent_stats",
             "phase":"leave","argsRaw":[1,0,104],"caller":"0x5b0fae","outputHex":"{{rival}}",
             "provenance":"wrapper-boundary-snapshot",
             "esiExpectedRaw":3396,"esiAtEnterRaw":3396,"esiAtLeaveRaw":3396,
             "espAtEnterRaw":4100,"espAtLeaveRaw":4100,"esiPreserved":true,
             "positionBeforeRaw":{{position}},"positionAfterRaw":{{position}}}
            """)!,
            JsonNode.Parse($$"""
            {"kind":"combat_context","schemaVersion":{{wrongSchema ? 2 : 3}},"session":1,"threadId":7,
             "combatCallId":2,"selectorCallId":1,"callId":2,"function":"combat_event",
             "phase":"leave","childCount":2,"childOverflow":false,"outputHex":"0808C800"}
            """)!,
            JsonNode.Parse("""{"kind":"ai_probe","function":"select_normal","phase":"leave","threadId":7,"callId":1,"side":0,"selectedHandIndex":-1}""")!
        };
        if (missingEsi) rows[2].AsObject().Remove("esiAtEnterRaw");
        var serialized = rows.Select((node, i) => { node["seq"] = i + 1; return node.ToJsonString(); });
        string header = $$"""{"kind":"metadata","session":1,"seen":6,"retained":6,"overwritten":0,"exeSha256":"{{PinnedExeSha256}}"}""";
        return CaptureReader.Read(new StringReader(header + "\n" + string.Join("\n", serialized)),
            "synthetic-v3", "synthetic-sha");
    }

    private static IReadOnlyList<Card> CatalogCards() =>
    [
        new(24,0x004d,"Axe Raider","", (15u<<20) | (5u<<29) | (170u<<9) | 115u),
        new(550,0x0213,"Gemini Elf","", (18u<<20) | (5u<<29) | (190u<<9) | 90u)
    ];

    public static void Run(Action<bool, string> check)
    {
        var capture = Capture();
        var withoutDat = CombatAttributeTriage.Analyze(capture);
        check(withoutDat.TotalFrames == 1 && withoutDat.NeutralCandidates == 1 &&
            withoutDat.ConditionalMatches == 0 && withoutDat.ValidatedIndependentContexts == 0 &&
            withoutDat.Cases[0].Attacker.Status == "CatalogUnavailable" &&
            withoutDat.Cases[0].Defender.Status == "CatalogUnavailable",
            "V3 triage preserves candidate without inferring resources or copying native stats");

        var withDat = CombatAttributeTriage.Analyze(capture,CatalogCards());
        check(withDat.PinnedCatalogProvided && withDat.ConditionalMatches == 2 &&
            withDat.ConditionalMismatches == 0 &&
            withDat.Cases[0].Attacker.ConditionalCoreHex == "18000F05A40600007E040000" &&
            withDat.Cases[0].Defender.ConditionalCoreHex == "260212056C07000084030000" &&
            withDat.ValidatedIndependentContexts == 0,
            "Pinned catalog uses Core plain-card evaluator with explicitly hypothetical empty manifest");

        var mismatch = CombatAttributeTriage.Analyze(Capture(changedDefender:true),CatalogCards());
        check(mismatch.ConditionalMatches == 1 && mismatch.ConditionalMismatches == 1 &&
            mismatch.Cases[0].Defender.Status == "ConditionalMismatch",
            "Native output is compared AFTER calculation, never used to select modifiers");

        var hidden = CombatAttributeTriage.Analyze(Capture(hidden:true),CatalogCards());
        check(hidden.HiddenDefendersInNeutral == 1 && hidden.NeutralVisibleDefenders == 0 &&
            hidden.ConditionalMatches == 1 && hidden.Cases[0].Defender.Status == "HiddenOpponentEstimate" &&
            hidden.Cases[0].Defender.ConditionalCoreHex is null,
            "Face-down opponent heuristic never masquerades as independent base attributes");

        var linked = CombatAttributeTriage.Analyze(Capture(links:1),CatalogCards());
        check(linked.NeutralCandidates == 0 && linked.ConditionalMatches == 0 &&
            linked.Cases[0].Attacker.Status == "NonNeutralLocalSnapshot",
            "Any local links or flags block even conditional plain-stat comparison");

        var unsupportedCards = new List<Card> { CatalogCards()[1], new(24,0x123,"Unknown intrinsic policy","",
            (15u<<20) | (5u<<29) | (170u<<9) | 115u) };
        var unsupported = CombatAttributeTriage.Analyze(Capture(),unsupportedCards);
        check(unsupported.Cases[0].Attacker.Status == "UnsupportedIntrinsicPolicy" &&
            unsupported.ConditionalMatches == 1,
            "Unimplemented intrinsic policy is explicit Unsupported, not presumed vanilla");

        bool Rejected(Func<CombatAttributeTriageReport> run)
        {
            try { run(); return false; }
            catch (InvalidDataException) { return true; }
        }
        check(Rejected(() => CombatAttributeTriage.Analyze(Capture(corruptedEsi:true),CatalogCards())) &&
            Rejected(() => CombatAttributeTriage.Analyze(Capture(wrongSchema:true),CatalogCards())) &&
            Rejected(() => CombatAttributeTriage.Analyze(Capture(missingEsi:true),CatalogCards())),
            "V3 triage rejects corrupt or absent wrapper ESI and unsupported schema instead of reporting matches");
    }
}
