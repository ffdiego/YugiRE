#include "yugire/effect_context.hpp"

namespace yugire {
void EffectContext::InflictDamage(unsigned int target_side) const {
    if (CanResolve()) engine::emit_damage(card_, target_side);
}
void EffectContext::DrawCards(unsigned int recipient_side, unsigned int count) const {
    if (CanResolve()) engine::emit_draw_cards(card_, recipient_side, count);
}
unsigned short EffectContext::ResolveConfigured() const {
    engine::record_call(card_);
    if (!CanResolve()) return 0;
    const unsigned int mode = engine::configured_mode(card_);
    if ((mode & 1u) != 0u) InflictDamage(Opponent());
    if ((mode & 2u) != 0u) DrawCards(Controller(), 5u);
    return 0;
}
} // namespace yugire
