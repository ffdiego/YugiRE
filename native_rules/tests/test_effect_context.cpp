#include "yugire/effect_context.hpp"

namespace {
unsigned int mode = 0, calls = 0, damages = 0, draws = 0;
unsigned int target = 99, controller = 99, amount = 0;
void reset(unsigned int newMode) {
    mode = newMode; calls = damages = draws = 0;
    target = controller = 99; amount = 0;
}
}
namespace yugire { namespace engine {
void record_call(Card) { ++calls; }
unsigned int configured_mode(Card) { return mode; }
void emit_damage(Card, unsigned int side) { ++damages; target = side; }
void emit_draw_cards(Card, unsigned int side, unsigned int count) {
    ++draws; controller = side; amount = count;
}
}} // namespace yugire::engine

int main() {
    yugire::CardRequest req{};
    req.raw[2] = 1;  // activator side 1
    req.raw[6] = 0;  // opponent target side 0
    for (unsigned int m = 1; m <= 3; ++m) {
        reset(m);
        yugire::EffectContext ctx(yugire::Card::Raimei, &req);
        if (ctx.ResolveConfigured() != 0 || calls != 1) return 1;
        if (damages != (m & 1u) || draws != ((m & 2u) ? 1u : 0u)) return 2;
        if ((m & 1u) && target != 0) return 3;
        if ((m & 2u) && (controller != 1 || amount != 5)) return 4;
    }
    reset(3);
    req.raw[4] = 4; // native request suppression
    yugire::EffectContext suppressed(yugire::Card::Sparks, &req);
    if (suppressed.ResolveConfigured() != 0 ||
        calls != 1 || damages != 0 || draws != 0) return 5;
    reset(3);
    yugire::EffectContext nullRequest(yugire::Card::Sparks, nullptr);
    if (nullRequest.ResolveConfigured() != 0 ||
        calls != 1 || damages != 0 || draws != 0) return 6;
    return 0;
}
