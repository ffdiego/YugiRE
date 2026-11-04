#pragma once
#include <cstdint>

namespace yugire {
// This POD is shared with the Frida adapter. All fields are uint32_t to
// avoid compiler-dependent packing. Card indices are INTERNAL, not resource IDs.
struct PlayerSnapshot {
    std::uint32_t life_points;
    std::uint32_t hand_count;
    std::uint32_t deck_count;
    std::uint32_t graveyard_count;
    std::uint32_t banished_count;
    std::uint32_t terminal_flags; // 0x100 deck-out, 0x200 Exodia, 0x400 Destiny
    std::uint32_t monsters[5];    // Physical slots 0..4
    std::uint32_t other_field[6]; // Physical slots 5..10 (visual meaning provisional)
    std::uint32_t field_position_words[11]; // Raw word at field_record + 6
    std::uint32_t hand_indices[80]; // First min(hand_count,80) entries
};
struct DuelSnapshot {
    PlayerSnapshot sides[2];
};
static_assert(sizeof(PlayerSnapshot) == 432, "Frida layout must be 432 bytes");
static_assert(sizeof(DuelSnapshot) == 864, "Frida layout must be 864 bytes");

// Read two documented per-player blocks from a known EXE's virtual image.
// Does not mutate game memory; caller must verify executable fingerprint.
bool read_duel_snapshot(const void* image_base, DuelSnapshot* output);

// Pure parser for offline tests; block contains at least 0xD44 bytes.
PlayerSnapshot parse_player_block(const std::uint8_t* player_block);
} // namespace yugire
