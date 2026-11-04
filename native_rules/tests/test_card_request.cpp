#include "yugire/card_request.hpp"

int main() {
    yugire::CardRequest request{};
    request.raw[2] = 1; // Activating player
    request.raw[6] = 0; // Opponent selected for damage by target-preparation
    if (request.activating_side() != 1) return 1;
    if (request.selected_target_side() != 0) return 2;
    if (request.suppresses_damage()) return 3;
    request.raw[4] |= 4;
    if (!request.suppresses_damage()) return 4;
    request.raw[2] = 0;
    request.raw[6] = 1;
    if (request.activating_side() != 0 || request.selected_target_side() != 1) return 5;
    return 0;
}
