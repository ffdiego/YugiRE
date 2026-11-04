#include "yugire/cards.hpp"
#include "yugire/engine.hpp"

namespace yugire { namespace cards {
unsigned short ookazi(const CardRequest* request, void* /*opaque*/) {
    engine::record_call(Card::Ookazi);
    if (request && !request->suppresses_damage()) {
        engine::emit_damage(Card::Ookazi, request->selected_target_side());
    }
    return 0;
}
}} // namespace yugire::cards
