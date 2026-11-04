#include "yugire/cards.hpp"
#include "yugire/effect_context.hpp"

namespace yugire { namespace cards {
unsigned short raimei(const CardRequest* request, void* /*opaque*/) {
    return EffectContext(Card::Raimei, request).ResolveConfigured();
}
}} // namespace yugire::cards
