using System.Text.Json.Nodes;
using Joey.Lab;

internal static class LabCombatContextChecks
{
    public static void Run(Action<bool, string> check)
    {
        const string selector = "{\"kind\":\"ai_probe\",\"function\":\"select_normal\",\"phase\":\"enter\",\"threadId\":7,\"callId\":1,\"side\":0}";
        const string entry = "{\"kind\":\"combat_context\",\"schemaVersion\":1,\"session\":1,\"threadId\":7,\"combatCallId\":2,\"selectorCallId\":1,\"callId\":2,\"function\":\"combat_event\",\"phase\":\"enter\",\"stateKind\":\"selector-window\",\"argsRaw\":[0,0,0],\"seedHex\":\"00000000\",\"attacker\":{\"side\":0,\"slot\":0,\"identityRaw\":24,\"internalCardIndex\":24,\"positionRaw\":2,\"defensePosition\":false,\"faceUp\":true,\"monsterFlagsRaw\":0,\"playerFlagsRaw\":0,\"future\":99},\"defender\":{\"side\":1,\"slot\":0,\"identityRaw\":550,\"internalCardIndex\":550,\"positionRaw\":0,\"defensePosition\":false,\"faceUp\":false,\"monsterFlagsRaw\":0,\"playerFlagsRaw\":0}}";
        const string own = "{\"kind\":\"combat_context\",\"schemaVersion\":1,\"session\":1,\"threadId\":7,\"combatCallId\":2,\"selectorCallId\":1,\"callId\":3,\"function\":\"own_stats\",\"phase\":\"leave\",\"argsRaw\":[0,0,123],\"outputHex\":\"18000F05A40600007E040000\"}";
        const string rival = "{\"kind\":\"combat_context\",\"schemaVersion\":1,\"session\":1,\"threadId\":7,\"combatCallId\":2,\"selectorCallId\":1,\"callId\":4,\"function\":\"opponent_stats\",\"phase\":\"leave\",\"outputHex\":\"00000000E8030000E8030000\"}";
        const string leave = "{\"kind\":\"combat_context\",\"schemaVersion\":1,\"session\":1,\"threadId\":7,\"combatCallId\":2,\"selectorCallId\":1,\"callId\":2,\"function\":\"combat_event\",\"phase\":\"leave\",\"outputHex\":\"000438FF\",\"childCount\":2,\"childOverflow\":false}";
        const string end = "{\"kind\":\"ai_probe\",\"function\":\"select_normal\",\"phase\":\"leave\",\"threadId\":7,\"callId\":1,\"side\":0,\"selectedHandIndex\":-1}";
        CombatContextReport Read(params string[] lines)
        {
            var events = lines.Select((line, i) => { var n = JsonNode.Parse(line)!; n["seq"] = i + 1; return n.ToJsonString(); }).ToArray();
            string header = $"{{\"kind\":\"metadata\",\"session\":1,\"seen\":{events.Length},\"retained\":{events.Length},\"overwritten\":0}}";
            var capture = CaptureReader.Read(new StringReader(header + "\n" + string.Join('\n', events)), "synthetic", "synthetic-hash");
            return CombatContextReader.Read(capture);
        }
        var result = Read(selector, entry, own, rival, leave, end);
        var context = result.Contexts.Single();
        check(result.Issues.Count == 0 && context.Status == "Incomplete" && context.Provenance.CombatCallId == 2 && context.Services.Count == 2,
            "Combat contexts correlate selector, F40 and services by thread and call ID");
        check(context.Attacker?.ResourceId is null && context.Attacker?.LifePoints is null && context.Attacker?.Raw.GetProperty("future").GetInt32() == 99 &&
            context.Defender?.FaceUp == false && context.Services[1].StatsOutput?.Attack == 1000 && context.Services[1].StatsOutput?.HeaderIndex == 0 &&
            context.Defender.InternalCardIndex == 550 && context.Provenance.ExeSha256 is null,
            "Combat contexts retain missing values, unknown fields and hidden observed estimate without inventing real stats");
        check(context.MissingOrInvalid.Contains("player flags at consumption not observed") && context.MissingOrInvalid.Contains("capture executable attestation absent"),
            "Combat contexts do not equate entry flags or capture hash with attestation");
        result = Read(selector, entry, own, rival, end);
        check(result.Contexts.Single().MissingOrInvalid.Contains("missing combat leave"), "Combat contexts keep unclosed calls incomplete");
        result = Read(selector, entry, own.Replace("\"threadId\":7", "\"threadId\":8"), rival, leave, end);
        check(result.UncorrelatedEvents.Count == 1 && result.Issues.Count > 0 && result.Contexts.Single().MissingOrInvalid.Contains("child count missing or mismatched"),
            "Combat contexts do not pair children from another thread");
        result = Read(selector, entry.Replace("\"schemaVersion\":1", "\"schemaVersion\":2"), own, rival, leave, end);
        check(result.Contexts.Single().Status == "Unsupported" && result.Contexts.Single().EnterRaw is not null,
            "Combat contexts preserve unsupported schema raw input");
        result = Read(selector, entry.Replace("\"faceUp\":true", "\"faceUp\":false"), own, rival, leave, end);
        check(result.Contexts.Single().MissingOrInvalid.Contains("attacker snapshot inconsistent"), "Combat contexts detect conflicting raw posture and decoded flag");
        result = Read(selector, entry, own.Replace("\"outputHex\":", "\"readError\":\"failed\",\"outputHex\":"), rival, leave, end);
        check(result.Contexts.Single().MissingOrInvalid.Any(x => x.Contains("read error")), "Combat contexts retain read failures as incomplete");
    }
}
