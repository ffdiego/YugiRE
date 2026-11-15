using System.Text.Json;
using Joey.Core;

namespace Joey.Lab;

/// <summary>
/// Creates an UNREVIEWED worklist from the original DAT. A compatible
/// summon category never proves a monster has no effects. The resulting JSON
/// deliberately cannot be executed until cards and both decks are reviewed.
/// </summary>
public static class SandboxPoolCandidates
{
    public static IReadOnlyList<Card> Suggest(IEnumerable<Card> cards, int count) =>
        cards.Where(card => card.Name.Length > 0 && card.IsMonster &&
                           card.Level is >= 1 and <= 4 &&
                           card.Attack > 0 && !NormalSummonRules.RequiresSpecialSummon(card))
            .OrderBy(card => card.Index).Take(count).ToArray();

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            string? dat = null, destination = null, game = "all";
            int count = 100;
            var used = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < args.Length; i++)
            {
                string flag = args[i];
                if (flag is not ("--dat" or "--output" or "--count" or "--game")
                    || i + 1 >= args.Length || !used.Add(flag))
                    throw new ArgumentException("Invalid/repeated/incomplete pool-candidates option: " + flag);
                string value = args[++i];
                switch (flag)
                {
                    case "--dat": dat = value; break;
                    case "--output": destination = value; break;
                    case "--game": game = value; break;
                    case "--count":
                        if (!int.TryParse(value, out count) || count is < 1 or > 500)
                            throw new ArgumentException("--count must be 1..500");
                        break;
                }
            }
            if (dat is null || destination is null || game is not ("all" or "joey"))
                throw new ArgumentException("Use --dat PATH --output PATH [--count N] [--game all|joey]");
            using var archive = new Archive(dat);
            var catalog = Catalog.Load(archive);
            byte[] packs = archive.Read(@"bin#\card_pack.bin");
            var available = CardListResources.ForPacks(catalog, packs, game == "joey" ? (ushort)4 : (ushort)7);
            var selected = Suggest(available, count);
            // The mode and blank notes enforce explicit review before duel-sim accepts this file.
            var proposed = new
            {
                schemaVersion = 1,
                mode = "UnreviewedCandidates",
                notes = "Candidates only: class/level/attack gates DO NOT prove absence of effects. Inspect each card manually, document the sandbox policy, then change mode to SandboxPartialOnly.",
                selectionGame = game,
                requestedCount = count,
                availableCandidates = selected.Count,
                cards = selected.Select(c => new
                {
                    internalIndex = c.Index, resourceId = c.Id, policy = "BaseMonsterSandbox",
                    note = "", nameForReview = c.Name,
                    levelForReview = c.Level, attackForReview = c.Attack,
                    defenseForReview = c.Defense
                }).ToArray(),
                deck0 = selected.Count >= 80 ? selected.Take(40).Select(c => c.Index).ToArray() : [],
                deck1 = selected.Count >= 80 ? selected.Skip(40).Take(40).Select(c => c.Index).ToArray() : []
            };
            destination = Path.GetFullPath(destination);
            if (File.Exists(destination)) throw new IOException("Output already exists: " + destination);
            string? dir = Path.GetDirectoryName(destination);
            if (dir is not null) Directory.CreateDirectory(dir);
            File.WriteAllText(destination, JsonSerializer.Serialize(proposed,
                new JsonSerializerOptions { WriteIndented = true }));
            output.WriteLine($"Wrote {selected.Count}/{count} UNREVIEWED candidates to {destination}.");
            output.WriteLine("No cards were automatically approved. This file is intentionally not executable as a sandbox manifest.");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
            or ArgumentException or JsonException or InvalidOperationException)
        {
            error.WriteLine("Failed to suggest sandbox pool: " + ex.Message);
            return 2;
        }
    }
}
