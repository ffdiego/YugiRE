#pragma once
#include "yugire/engine.hpp"

namespace yugire {
// EffectContext owns no memory or global engine state. It operates on the
// original 20-byte callback request and delegates effects to engine adapters.
// Normal effect resolution is on the game's duel thread, not the Frida RPC.
class EffectContext {
public:
    explicit EffectContext(Card card, const CardRequest* request)
        : card_(card), request_(request) {}

    bool CanResolve() const {
        return request_ != nullptr && !request_->suppresses_damage();
    }

    unsigned int Controller() const {
        return request_ ? request_->activating_side() : 0u;
    }
    unsigned int Opponent() const {
        return request_ ? request_->selected_target_side() : 0u;
    }
    void InflictDamage(unsigned int target_side) const;
    void DrawCards(unsigned int recipient_side, unsigned int count) const;
    unsigned short ResolveConfigured() const;

private:
    Card card_;
    const CardRequest* request_;
};
} // namespace yugire
