#include "yugire/field_target.hpp"

int main() {
    using yugire::same_field_card;
    constexpr std::uint32_t card = 0x120015A2u; // key, owner, internal index
    if (!same_field_card(card, card | 0x00A00000u)) return 1; // mutable flags
    if (same_field_card(0u, card) || same_field_card(card, 0u)) return 2;
    if (same_field_card(card ^ 0x1u, card)) return 3; // other index
    if (same_field_card(card ^ 0x1000u, card)) return 4; // other owner
    if (same_field_card(card ^ 0x01000000u, card)) return 5; // other occurrence
    std::uint32_t mask = 0;
    if (!yugire::monster_slot_mask(0, 0, &mask) || mask != 1u) return 6;
    if (!yugire::monster_slot_mask(1, 4, &mask) || mask != (1u << 20)) return 7;
    if (yugire::monster_slot_mask(2, 0, &mask) ||
        yugire::monster_slot_mask(0, 5, &mask) ||
        yugire::monster_slot_mask(0, 0, nullptr)) return 8;
    return 0;
}
