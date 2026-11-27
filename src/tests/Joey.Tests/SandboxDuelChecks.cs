using System.Text.Json;
using Joey.Core;
using Joey.Lab;

internal static class SandboxDuelChecks
{
    public static void Run(Action<bool, string> check)
    {
        static Card Monster(int index, ushort id, int attackUnits) => new(
            index, id, "Sandbox candidate " + index, "",
            (1u << 20) | (4u << 25) | ((uint)attackUnits << 9) | 80u);
        ushort[] normalIds = [0, 1, 2, 3, 5, 6, 9, 12, 13, 16, 17, 18, 19, 21, 23, 25, 26, 27, 31, 32, 33, 34, 37, 38, 40, 41, 44, 49, 50, 51, 52, 54, 58, 59, 61, 63, 64, 65, 67, 70];
        var catalog = Enumerable.Range(0, 40)
            .Select(i => Monster(i + 1, normalIds[i], 100 + i)).ToList();
        var spark = new Card(755, 342, "Sparks", "", 21u << 20);
        // Category 1 contains ordinary-summonable effect monsters: those must not
        // enter the plain-stat-only candidate pool or its execution whitelist.
        var effect = Monster(800, 801, 140) with { Properties =
            (1u << 20) | (4u << 25) | (1u << 18) | (140u << 9) | 80u };
        check(NormalSummonRules.Category(effect) == 1 &&
              !NormalSummonRules.RequiresSpecialSummon(effect) &&
              !SandboxPoolCandidates.Suggest(catalog.Append(effect), 100)
                  .Any(x => x.Index == effect.Index),
            "Pool candidates exclude ordinary-summonable native effect category 1");

        var candidates = SandboxPoolCandidates.Suggest(catalog.Append(spark), 100);
        check(candidates.Count == 40 && candidates.All(x => x.IsMonster) &&
              candidates.All(x => x.Level <= 4),
            "Pool candidate discovery lists basic summon shapes but never grants native parity");
        catalog.Add(spark);
        List<SandboxCardEntry> reviewed = catalog.Select(card => new SandboxCardEntry(
            card.Index, card.Id,
            card.IsMonster ? "BaseMonsterSandbox" : "DirectDamageSandbox",
            "Declared sandbox-only policy, no native effect claim.")).ToList();
        var deckIndices = catalog.Take(39).Select(c => c.Index).Append(755).ToList();
        var manifest = new SandboxPoolManifest(1, "SandboxPartialOnly", reviewed,
            deckIndices, deckIndices);
        var (deck0, deck1, poolCount) = SandboxDuelRunner.ResolveManifest(catalog, manifest);
        check(deck0.Count == 40 && deck1.Count == 40 && poolCount == 41,
            "Sandbox manifest resolves only explicitly reviewed identities");

        var trace1 = new StringWriter();
        var trace2 = new StringWriter();
        var a = SandboxDuelRunner.Simulate(deck0, deck1, 2, 123, 160, trace1,
            "OperatorDeclaredSandboxPool", poolCount);
        var b = SandboxDuelRunner.Simulate(deck0, deck1, 2, 123, 160, trace2,
            "OperatorDeclaredSandboxPool", poolCount);
        check(a.SequenceEqual(b) && trace1.ToString() == trace2.ToString() &&
              a.Count == 2 && a.All(x => x.Actions > 0 && x.FinalTurn <= 161 && x.Status != "Unsupported"),
            "Headless sandbox produces deterministic bounded duels and JSONL");

        var entries = trace1.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(text => JsonDocument.Parse(text)).ToArray();
        try
        {
            check(entries[0].RootElement.GetProperty("kind").GetString() == "sandbox_run" &&
                entries[^1].RootElement.GetProperty("kind").GetString() == "sandbox_complete" &&
                entries[^1].RootElement.GetProperty("complete").GetBoolean() &&
                !entries[^1].RootElement.GetProperty("nativeParity").GetBoolean() &&
                entries.Count(doc => doc.RootElement.GetProperty("kind").GetString() == "sandbox_duel_end") == 2,
                "Sandbox emits scoped start, actions, duel results and completion marker");
            var actions = entries.Where(doc =>
                doc.RootElement.GetProperty("kind").GetString() == "sandbox_action").ToArray();
            check(actions.Length > 0 && actions.Any(doc =>
                doc.RootElement.GetProperty("action").GetString() == "NormalSummon") &&
                actions.Any(doc => doc.RootElement.GetProperty("action").GetString() == "Attack"),
                "Sandbox action trace covers normal summons and attacks");
            check(actions.All(doc => doc.RootElement.GetProperty("after").GetProperty("life").GetArrayLength() == 2 &&
                doc.RootElement.GetProperty("commands").GetArrayLength() > 0),
                "Sandbox actions preserve before/after LP snapshots and executed typed commands");
        }
        finally { foreach (var doc in entries) doc.Dispose(); }

        void Reject(SandboxPoolManifest bad, string label)
        {
            try { SandboxDuelRunner.ResolveManifest(catalog, bad); }
            catch (InvalidDataException) { check(true, label); return; }
            throw new Exception("Expected sandbox manifest rejection: " + label);
        }
        Reject(manifest with { Cards = reviewed.Skip(1).ToList() },
            "Sandbox rejects deck card not in reviewed pool");
        Reject(manifest with { Cards = reviewed.Select((entry, i) =>
            i == 0 ? entry with { ResourceId = 999 } : entry).ToList() },
            "Sandbox rejects resource ID that disagrees with catalog");
        Reject(manifest with { Cards = reviewed.Select((entry, i) =>
            i == 0 ? entry with { Policy = "Unknown" } : entry).ToList() },
            "Sandbox rejects unknown effect policy");
        bool blockedEffect = false;
        try
        {
            SandboxDuelRunner.ResolveManifest(catalog.Append(effect).ToList(), manifest with
            {
                Cards = [.. reviewed, new SandboxCardEntry(
                    effect.Index, effect.Id, "BaseMonsterSandbox",
                    "Explicitly scoped to plain monster stats only")],
                Deck0 = [.. deckIndices.Take(39), effect.Index]
            });
        }
        catch (InvalidDataException exception)
        {
            blockedEffect = exception.Message.Contains("Unsupported sandbox card/policy");
        }
        check(blockedEffect, "Sandbox rejects effect-category card even when its identity resolves");
        Reject(manifest with { Deck0 = Enumerable.Repeat(1, 40).ToList() },
            "Sandbox rejects more than three copies of a card");
        Reject(manifest with { Deck0 = deckIndices.Take(20).ToList() },
            "Sandbox rejects incomplete main deck");
        Reject(manifest with { Mode = "NativeValidated" },
            "Sandbox rejects native-parity claims in manifest");

        string temp = Path.Combine(Path.GetTempPath(), "joey-sandbox-" +
            Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            int exit = SandboxDuelRunner.Run(
                ["--fixture", "--duels", "2", "--seed", "123", "--output", temp],
                stdout, stderr);
            check(exit == 0 && File.Exists(temp) && stdout.ToString().Contains("Sandbox duels: 2"),
                "CLI executes synthetic headless fixture without DAT or Windows");
            check(SandboxDuelRunner.Run(
                ["--fixture", "--duels", "1", "--seed", "123", "--output", temp],
                new StringWriter(), new StringWriter()) == 2,
                "CLI refuses to overwrite an existing trace");
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
