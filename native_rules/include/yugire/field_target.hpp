#pragma once
#include <cstdint>

namespace yugire {
// The index occupies bits 0..11; bit 12 is the owner and bits 24..30
// form the occurrence key documented in man-eater-bug-targeting.md.
// Other bits can change as battle/animation flags evolve.
constexpr std::uint32_t kFieldIdentityMask = 0x7F001FFFu;

inline bool same_field_card(std::uint32_t observed, std::uint32_t expected) {
    return (observed & 0xFFFu) != 0u &&
           (expected & 0xFFFu) != 0u &&
           (observed & kFieldIdentityMask) == (expected & kFieldIdentityMask);
}
inline bool monster_slot_mask(unsigned int side, unsigned int slot,
                              std::uint32_t* output) {
    if (!output || side > 1u || slot >= 5u) return false;
    *output = 1u << (side * 16u + slot);
    return true;
}
} // namespace yugire
