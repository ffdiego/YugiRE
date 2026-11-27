using System.Text.Json;
using Joey.Core;

namespace Joey.Lab;

// Authoritative C# observations at script boundaries. The native comparison
// must still correlate decisions, occurrences and the corresponding windows.
internal static class DuelCheckpointExport
{
    internal sealed record CardIdentity(ushort ResourceId, int InternalIndex, long? Occurrence);
    internal sealed record MonsterSlot(CardIdentity Card, bool FaceDown, bool Defense, int? Attack, int? DefenseValue);
    internal sealed record SpellSlot(CardIdentity Card, bool FaceDown);
    internal sealed record SideState(int Life, CardIdentity[] Hand, CardIdentity[] Deck,
        CardIdentity[] Graveyard, CardIdentity[] Banished, CardIdentity[] FusionDeck,
        MonsterSlot?[] Monsters, SpellSlot?[] Spells, SpellSlot? FieldSpell);
    internal sealed record Checkpoint(DuelScriptBoundary Boundary, int Turn, int ActivePlayer, string Phase,
        bool MainPhase2, int? Winner, bool IsDraw, SideState[] Sides);
    private sealed record Document(string Schema, bool Complete, IReadOnlyList<Checkpoint> Checkpoints);

    internal static Checkpoint Capture(DemoDuel duel, DuelScriptBoundary boundary)
    {
        CardIdentity Identity(int side, DuelZone zone, int slot, Card card) =>
            new(card.Id, card.Index, duel.TryCaptureCardTarget(side, zone, slot, out var target)
                ? target.Occurrence.Value : null);

        SideState CaptureSide(int side)
        {
            CardIdentity[] Cards(DuelZone zone, IReadOnlyList<Card> cards) =>
                cards.Select((card, index) => Identity(side, zone, index, card)).ToArray();

            MonsterSlot?[] monsters = new MonsterSlot?[5];
            SpellSlot?[] spells = new SpellSlot?[5];
            for (int slot = 0; slot < 5; slot++)
            {
                if (duel.Monsters[side, slot] is { } monster)
                {
                    bool hidden = duel.MonsterFaceDown[side, slot];
                    monsters[slot] = new(Identity(side, DuelZone.Monster, slot, monster), hidden,
                        duel.DefensePosition[side, slot], hidden ? null : duel.AttackValue(side, slot),
                        hidden ? null : duel.DefenseValue(side, slot));
                }
                if (duel.Spells[side, slot] is { } spell)
                    spells[slot] = new(Identity(side, DuelZone.SpellTrap, slot, spell), duel.SpellFaceDown[side, slot]);
            }
            SpellSlot? field = duel.FieldSpells[side] is { } fieldCard
                ? new(Identity(side, DuelZone.FieldSpell, 0, fieldCard), duel.FieldSpellFaceDown[side]) : null;
            return new(duel.Life[side], Cards(DuelZone.Hand, duel.HandFor(side)),
                Cards(DuelZone.Deck, duel.DeckSnapshot(side)), Cards(DuelZone.Graveyard, duel.Graveyard(side)),
                Cards(DuelZone.Banished, duel.Banished(side)), Cards(DuelZone.FusionDeck, duel.FusionDeck(side)),
                monsters, spells, field);
        }

        return new(boundary, duel.Turn, duel.ActivePlayer, duel.Phase.ToString(), duel.IsMainPhase2,
            duel.Winner, duel.IsDraw, [CaptureSide(0), CaptureSide(1)]);
    }

    internal static void Write(TextWriter output, bool complete, IReadOnlyList<Checkpoint> checkpoints) =>
        output.WriteLine(JsonSerializer.Serialize(new Document("yugire-duel-checkpoints-v1", complete, checkpoints),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));
}
