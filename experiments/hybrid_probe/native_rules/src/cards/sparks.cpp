#include "yugire/cards.hpp"
#include "yugire/engine.hpp"

namespace yugire { namespace cards {
unsigned short sparks(const CardRequest* request, void* /*opaque*/) {
    engine::record_call(Card::Sparks);
    if (request && !request->suppresses_damage()) {
        engine::emit_damage(Card::Sparks, request->selected_target_side());
    }
    return 0;
}
}} // namespace yugire::cards
