#include "yugire/cards.hpp"
#include "yugire/effect_context.hpp"

namespace yugire { namespace cards {
unsigned short sparks(const CardRequest* request, void* /*opaque*/) {
    return EffectContext(Card::Sparks, request).ResolveConfigured();
}
}} // namespace yugire::cards
