#pragma once

// The first 20 bytes are observed by Frida. Field names beyond the offsets
// below are intentionally NOT invented: their semantics remain incomplete.
namespace yugire {
struct CardRequest {
    unsigned char raw[20];

    bool suppresses_damage() const {
        return (raw[4] & 0x04u) != 0;
    }
    // Original damage callback at 0x00584E60 uses 1 - (request[2] & 1)
    // for the opponent. This bit therefore identifies the actor in the
    // ordinary activation path. Special activation modes are untested.
    unsigned int activating_side() const {
        return static_cast<unsigned int>(raw[2] & 1u);
    }
    unsigned int selected_target_side() const {
        return static_cast<unsigned int>(raw[6] & 1u);
    }
};
static_assert(sizeof(CardRequest) == 20, "Request must preserve its observed byte layout");
} // namespace yugire
