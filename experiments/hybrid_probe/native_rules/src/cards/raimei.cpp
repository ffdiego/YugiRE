#include "yugire/cards.hpp"
#include "yugire/engine.hpp"

namespace yugire { namespace cards {
unsigned short raimei(const CardRequest* request, void* /*opaque*/) {
    engine::record_call(Card::Raimei);
    if (request && !request->suppresses_damage()) {
        const unsigned int mode = engine::configured_mode(Card::Raimei);
        if ((mode & 1u) != 0u) {
            engine::emit_damage(Card::Raimei, request->selected_target_side());
        }
        if ((mode & 2u) != 0u) {
            // After damage, enqueue five draws through Joey's native
            // queue for the activating player (not the damage target).
            engine::emit_draw_five(Card::Raimei, request->activating_side());
        }
    }
    return 0;
}
}} // namespace yugire::cards
