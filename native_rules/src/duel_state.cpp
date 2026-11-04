#include "yugire/duel_state.hpp"

namespace yugire {
namespace {
constexpr std::uint32_t kDocumentedImageBase = 0x00400000u;
constexpr std::uint32_t kPlayerZeroVa = 0x00A55D64u;
constexpr std::uint32_t kPlayerStride = 0xD44u;
constexpr std::uint32_t kMonsterStart = 0x10u; // VA 0x00A55D74
constexpr std::uint32_t kMonsterStride = 0x90u;
constexpr std::uint32_t kFieldSlotCount = 11u;
constexpr std::uint32_t kHandOffset = 0x6D0u; // 0x00A56434 - 0x00A55D64
constexpr std::uint32_t kMaxHandCards = 80u;
static_assert(kMonsterStart + (kFieldSlotCount - 1u)*kMonsterStride + 8u <= kPlayerStride,
              "Field read exceeds player block");
static_assert(kHandOffset + kMaxHandCards * 4u <= kPlayerStride,
              "Hand read exceeds player block");
static_assert(kPlayerZeroVa > kDocumentedImageBase, "Invalid RVA");

std::uint16_t read_u16(const volatile std::uint8_t* p) {
    return static_cast<std::uint16_t>(p[0]) |
           static_cast<std::uint16_t>(p[1]) << 8;
}
std::uint32_t read_u32(const volatile std::uint8_t* p) {
    return static_cast<std::uint32_t>(p[0]) |
           static_cast<std::uint32_t>(p[1]) << 8 |
           static_cast<std::uint32_t>(p[2]) << 16 |
           static_cast<std::uint32_t>(p[3]) << 24;
}
PlayerSnapshot parse_block(const volatile std::uint8_t* base) {
    PlayerSnapshot p{};
    // Documented in scripts/joeytrace_core.py and docs/30-joeytrace.md.
    p.life_points = read_u16(base);
    p.hand_count = base[4];
    p.deck_count = base[5];
    p.graveyard_count = base[6];
    // Byte at +8 shares a word with terminal-condition bits.
    p.banished_count = base[8];
    p.terminal_flags = read_u16(base + 8) & 0x0700u;
    for (std::uint32_t slot = 0; slot < kFieldSlotCount; ++slot) {
        const auto* record = base + kMonsterStart + slot * kMonsterStride;
        const std::uint32_t index = read_u32(record) & 0x0FFFu;
        if (slot < 5u) p.monsters[slot] = index;
        else p.other_field[slot - 5u] = index;
        p.field_position_words[slot] = read_u16(record + 6u);
    }
    const std::uint32_t count =
        p.hand_count < kMaxHandCards ? p.hand_count : kMaxHandCards;
    for (std::uint32_t i = 0; i < count; ++i) {
        p.hand_indices[i] = read_u32(base + kHandOffset + i * 4u) & 0x0FFFu;
    }
    return p;
}
} // namespace

PlayerSnapshot parse_player_block(const std::uint8_t* player_block) {
    if (!player_block) return PlayerSnapshot{};
    return parse_block(player_block);
}
bool read_duel_snapshot(const void* image_base, DuelSnapshot* output) {
    if (!image_base || !output) return false;
    auto* base = static_cast<const std::uint8_t*>(image_base);
    const auto* player_zero = base +
        (kPlayerZeroVa - kDocumentedImageBase);
    for (std::uint32_t side = 0; side < 2; ++side) {
        output->sides[side] = parse_block(player_zero + side * kPlayerStride);
    }
    return true;
}
} // namespace yugire
