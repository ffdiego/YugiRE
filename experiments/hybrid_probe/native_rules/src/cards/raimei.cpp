#include "yugire/cards.hpp"
#include "yugire/engine.hpp"

namespace yugire { namespace cards {
unsigned short raimei(const CardRequest* request, void* /*opaque*/) {
    engine::record_call(Card::Raimei);
    if (request && !request->suppresses_damage()) {
        engine::emit_damage(Card::Raimei, request->selected_target_side());
    }
    return 0;
}
}} // namespace yugire::cards
