using System.Security.Cryptography;
using System.Text.Json;
using Joey.Core;

namespace Joey.Lab;

// Explicit sandbox policy, NOT evidence that a card has no native effects.
// Every imported card needs an operator-reviewed entry; this runner does not
// infer playable support from class, callback profile or descriptions.
public sealed record SandboxCardEntry(int InternalIndex, ushort ResourceId, string Policy, string Note);
public sealed record SandboxPoolManifest(
    int SchemaVersion, string Mode, List<SandboxCardEntry> Cards,
    List<int> Deck0, List<int> Deck1);
public sealed record SandboxDuelOutcome(int DuelIndex, int Seed, int? Winner,
    string Status, int FinalTurn, int Actions, int Life0, int Life1);

public static class SandboxDuelRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            bool fixture = false;
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--fixture")
                {
                    if (fixture) throw new ArgumentException("Repeated --fixture");
                    fixture = true;
                    continue;
                }
                if (args[i] is not ("--dat" or "--manifest" or "--output" or "--duels" or "--seed" or "--max-turns")
                    || i + 1 >= args.Length || !options.TryAdd(args[i], args[i + 1]))
                    throw new ArgumentException("Invalid, repeated or incomplete option: " + args[i]);
                i++;
            }
            if (!options.TryGetValue("--output", out string? destination))
                throw new ArgumentException("--output is required");
            if (!options.TryGetValue("--duels", out string? countText) ||
                !int.TryParse(countText, out int duels) || duels is < 1 or > 500)
                throw new ArgumentException("--duels must be 1..500");
            if (!options.TryGetValue("--seed", out string? seedText) || !int.TryParse(seedText, out int seed))
                throw new ArgumentException("--seed must be an integer");
            int maxTurns = 160;
            if (options.TryGetValue("--max-turns", out string? turnText) &&
                (!int.TryParse(turnText, out maxTurns) || maxTurns is < 2 or > 1000))
                throw new ArgumentException("--max-turns must be 2..1000");
            if (fixture && (options.ContainsKey("--dat") || options.ContainsKey("--manifest")) ||
                !fixture && (!options.ContainsKey("--dat") || !options.ContainsKey("--manifest")))
                throw new ArgumentException("Use --fixture OR both --dat and --manifest");

            List<Card> deck0, deck1;
            string source;
            int poolCount;
            string? datSha256 = null, manifestSha256 = null;
            if (fixture)
            {
                (deck0, deck1) = SyntheticFixture();
                source = "SyntheticFixture";
                poolCount = deck0.Concat(deck1).Select(c => c.Index).Distinct().Count();
            }
            else
            {
                string dat = options["--dat"];
                using (var stream = File.OpenRead(dat))
                    datSha256 = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
                manifestSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(options["--manifest"]))).ToLowerInvariant();
                using var archive = new Archive(dat);
                var catalog = Catalog.Load(archive);
                var manifest = JsonSerializer.Deserialize<SandboxPoolManifest>(
                    File.ReadAllText(options["--manifest"]), JsonOptions)
                    ?? throw new InvalidDataException("Empty sandbox manifest");
                (deck0, deck1, poolCount) = ResolveManifest(catalog, manifest);
                source = "OperatorDeclaredSandboxPool";
            }

            destination = Path.GetFullPath(destination);
            if (File.Exists(destination))
                throw new IOException("Output already exists; will not overwrite: " + destination);
            string? folder = Path.GetDirectoryName(destination);
            if (folder is not null) Directory.CreateDirectory(folder);

            using (var events = new StreamWriter(new FileStream(destination, FileMode.CreateNew, FileAccess.Write)))
            {
                var totals = Simulate(deck0, deck1, duels, seed, maxTurns, events, source, poolCount,
                    datSha256, manifestSha256);
                output.WriteLine($"Sandbox duels: {totals.Count}; winners 0/1: " +
                    $"{totals.Count(x => x.Winner == 0)}/{totals.Count(x => x.Winner == 1)}; " +
                    $"turn-limit: {totals.Count(x => x.Status == "TurnLimit")}; " +
                    $"actions: {totals.Sum(x => x.Actions)}");
                output.WriteLine("Saved sandbox-only JSONL: " + destination);
            }
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
            or InvalidDataException or JsonException or InvalidOperationException or OverflowException)
        {
            error.WriteLine("Invalid or failed sandbox run: " + ex.Message);
            return 2;
        }
    }

    public static (List<Card> Deck0, List<Card> Deck1, int PoolCount) ResolveManifest(
        IReadOnlyList<Card> catalog, SandboxPoolManifest manifest)
    {
        if (manifest.SchemaVersion != 1 || manifest.Mode != "SandboxPartialOnly" ||
            manifest.Cards is null || manifest.Deck0 is null || manifest.Deck1 is null)
            throw new InvalidDataException("Expected schemaVersion=1, mode=SandboxPartialOnly, cards and decks");
        var ids = catalog.ToDictionary(card => card.Index);
        var approved = new Dictionary<int, Card>();
        foreach (var item in manifest.Cards)
        {
            if (item is null || item.Note is null || item.Note.Trim().Length < 12)
                throw new InvalidDataException("Each card requires a nonempty review note (at least 12 characters)");
            if (!ids.TryGetValue(item.InternalIndex, out var card) || card.Id != item.ResourceId)
                throw new InvalidDataException("Unresolved or mismatched internal index and resource ID");
            if (!approved.TryAdd(item.InternalIndex, card))
                throw new InvalidDataException("Duplicate reviewed card identity: " + item.InternalIndex);
            bool accepted = item.Policy switch
            {
                "BaseMonsterSandbox" => card.IsMonster && card.Level is >= 1 and <= 4 &&
                    !NormalSummonRules.RequiresSpecialSummon(card) && card.Attack > 0,
                "DirectDamageSandbox" => !card.IsMonster && DirectDamageRules.Amount(card.Id).HasValue,
                _ => false
            };
            if (!accepted)
                throw new InvalidDataException("Unsupported sandbox card/policy: " +
                    item.InternalIndex + " (" + item.Policy + ")");
        }
        if (approved.Count == 0) throw new InvalidDataException("Empty reviewed pool");
        List<Card> ResolveDeck(List<int> indices, string side)
        {
            if (indices.Count is < 40 or > 80)
                throw new InvalidDataException($"{side} must contain 40..80 main-deck cards");
            if (indices.GroupBy(index => index).Any(group => group.Count() > 3))
                throw new InvalidDataException($"{side} exceeds three copies of one internal index");
            var deck = new List<Card>(indices.Count);
            foreach (int index in indices)
            {
                if (!approved.TryGetValue(index, out var card))
                    throw new InvalidDataException($"{side} includes an unreviewed card: {index}");
                deck.Add(card);
            }
            return deck;
        }
        return (ResolveDeck(manifest.Deck0, "deck0"), ResolveDeck(manifest.Deck1, "deck1"),
            approved.Count);
    }

    private static (List<Card>, List<Card>) SyntheticFixture()
    {
        static List<Card> Deck(int side)
        {
            var result = new List<Card>();
            for (int i = 0; i < 38; i++)
            {
                int attackUnits = 95 + (i % 12) * 8;
                int defenseUnits = 80 + (i % 9) * 6;
                uint props = (1u << 20) | (4u << 25) |
                    ((uint)attackUnits << 9) | (uint)defenseUnits;
                result.Add(new Card(2000 + side * 100 + i, (ushort)(3000 + side * 100 + i),
                    $"Synthetic monster {side}/{i}", "", props));
            }
            result.Add(new Card(2900 + side, 342, "Synthetic Sparks", "", 21u << 20));
            result.Add(new Card(2950 + side, 345, "Synthetic Ookazi", "", 21u << 20));
            return result;
        }
        return (Deck(0), Deck(1));
    }

    public static IReadOnlyList<SandboxDuelOutcome> Simulate(
        IReadOnlyList<Card> deck0, IReadOnlyList<Card> deck1, int duels, int seed,
        int maxTurns, TextWriter events, string source = "SyntheticFixture", int poolCount = 0,
        string? datSha256 = null, string? manifestSha256 = null)
    {
        if (duels is < 1 or > 500 || maxTurns is < 2 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(duels), "Bounded duels and turns required");
        if (deck0.Count is < 40 or > 80 || deck1.Count is < 40 or > 80)
            throw new ArgumentException("Sandbox decks must contain 40..80 cards each");
        events.WriteLine(JsonSerializer.Serialize(new
        {
            kind = "sandbox_run", schemaVersion = 1, origin = source,
            nativeParity = false, seed, duels, maxTurns, poolCount, datSha256, manifestSha256,
            deck0Indices = deck0.Select(c => c.Index).ToArray(),
            deck1Indices = deck1.Select(c => c.Index).ToArray()
        }));
        var summaries = new List<SandboxDuelOutcome>();
        for (int game = 0; game < duels; game++)
        {
            int gameSeed = unchecked(seed + game * 1009);
            DemoDuel duel = DemoDuel.FromDecks(deck0, deck1, gameSeed);
            int sequence = 0;
            object Snapshot() => new
            {
                life = duel.Life.ToArray(),
                decks = new[] { duel.DeckCount(0), duel.DeckCount(1) },
                handCountActive = duel.Hand.Count,
                monsters = Enumerable.Range(0, 2).Select(p => Enumerable.Range(0, 5)
                    .Select(z => duel.Monsters[p, z]?.Index).ToArray()).ToArray(),
                spells = Enumerable.Range(0, 2).Select(p => Enumerable.Range(0, 5)
                    .Select(z => duel.Spells[p, z]?.Index).ToArray()).ToArray(),
                winner = duel.Winner
            };
            void Step(string action, Func<(bool Success, string Message)> operation,
                int? sourceIndex = null, int? targetPlayer = null, int? targetZone = null)
            {
                int turn = duel.Turn, side = duel.ActivePlayer;
                string phase = duel.Phase.ToString();
                object before = Snapshot();
                var (success, message) = operation();
                if (!success)
                    throw new InvalidOperationException($"Rejected sandbox action {action}: {message}");
                events.WriteLine(JsonSerializer.Serialize(new
                {
                    kind = "sandbox_action", duelIndex = game, seq = ++sequence,
                    turn, side, phase, action, sourceIndex, targetPlayer, targetZone,
                    message, before, after = Snapshot()
                }));
            }
            while (duel.Winner is null && duel.Turn <= maxTurns)
            {
                Step("Draw", () => { bool ok = duel.Draw(out string message); return (ok, message); });
                if (duel.Winner is not null) break;

                // Only whitelisted fixed-damage spells; not a native effect/chain executor.
                while (duel.Winner is null)
                {
                    int spell = Enumerable.Range(0, duel.Hand.Count).FirstOrDefault(
                        i => DirectDamageRules.Amount(duel.Hand[i].Id).HasValue &&
                             duel.CanActivateFromHand(i), -1);
                    if (spell < 0) break;
                    int cardIndex = duel.Hand[spell].Index;
                    Step("FixedDamage", () => { bool ok = duel.Play(spell, out string message); return (ok, message); },
                        cardIndex);
                }
                if (duel.Winner is not null) break;

                // No tribute/special summon in the initial sandbox. Choose highest ATK.
                int summon = Enumerable.Range(0, duel.Hand.Count)
                    .Where(i => duel.Hand[i].IsMonster && duel.Hand[i].Level <= 4 &&
                                duel.CanSummonFromHand(i))
                    .OrderByDescending(i => duel.Hand[i].Attack)
                    .ThenBy(i => duel.Hand[i].Index)
                    .FirstOrDefault(-1);
                if (summon >= 0)
                {
                    int cardIndex = duel.Hand[summon].Index;
                    Step("NormalSummon", () => { bool ok = duel.SummonLeftmost(summon, false, out string message); return (ok, message); },
                        cardIndex);
                }
                if (duel.Turn > 1 && duel.Winner is null)
                {
                    Step("BeginBattle", () => { bool ok = duel.BeginBattle(out string message); return (ok, message); });
                    int side = duel.ActivePlayer, other = 1 - side;
                    for (int zone = 0; zone < 5 && duel.Winner is null; zone++)
                    {
                        if (duel.Monsters[side, zone] is not Card card || duel.DefensePosition[side, zone])
                            continue;
                        int target = Enumerable.Range(0, 5)
                            .Where(z => duel.Monsters[other, z] is not null)
                            .OrderBy(z => duel.DefensePosition[other, z]
                                ? duel.DefenseValue(other, z) : duel.AttackValue(other, z))
                            .ThenBy(z => z).FirstOrDefault(-1);
                        int sourceZone = zone;
                        Step("Attack", () => { bool ok = duel.Attack(sourceZone, target, out string message); return (ok, message); },
                            card.Index, other, target);
                    }
                }
                if (duel.Winner is not null) break;
                Step("EndTurn", () => { bool ok = duel.EndTurn(out string message); return (ok, message); });
                while (duel.Phase == DemoPhase.End && duel.Winner is null)
                {
                    int index = duel.Hand.Count - 1;
                    int cardIndex = duel.Hand[index].Index;
                    Step("EndPhaseDiscard", () => { bool ok = duel.DiscardForEndTurn(index, out string message); return (ok, message); },
                        cardIndex);
                }
            }
            string status = duel.Winner is null ? "TurnLimit" : "SandboxWinner";
            var outcome = new SandboxDuelOutcome(game, gameSeed, duel.Winner,
                status, duel.Turn, sequence, duel.Life[0], duel.Life[1]);
            summaries.Add(outcome);
            events.WriteLine(JsonSerializer.Serialize(new
            {
                kind = "sandbox_duel_end", schemaVersion = 1, origin = source,
                nativeParity = false, outcome
            }));
        }
        events.WriteLine(JsonSerializer.Serialize(new
        {
            kind = "sandbox_complete", complete = true, schemaVersion = 1,
            duelCount = summaries.Count, actionCount = summaries.Sum(s => s.Actions),
            winner0 = summaries.Count(s => s.Winner == 0),
            winner1 = summaries.Count(s => s.Winner == 1),
            turnLimit = summaries.Count(s => s.Status == "TurnLimit"),
            nativeParity = false
        }));
        return summaries;
    }
}
