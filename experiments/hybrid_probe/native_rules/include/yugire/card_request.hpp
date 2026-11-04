#pragma once

// The first 20 bytes are observed by Frida. Field names beyond the offsets
// below are intentionally NOT invented: their semantics remain incomplete.
namespace yugire {
struct CardRequest {
    unsigned char raw[20];

    bool suppresses_damage() const {
        return (raw[4] & 0x04u) != 0;
    }
    unsigned int selected_target_side() const {
        return static_cast<unsigned int>(raw[6] & 1u);
    }
};
static_assert(sizeof(CardRequest) == 20, "Request must preserve its observed byte layout");
} // namespace yugire
