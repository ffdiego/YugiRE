#include "yugire/duel_state.hpp"

int main() {
    std::uint8_t player[0xD44] = {};
    player[0] = 0x40; player[1] = 0x1F;   // 8000 LP
    player[4] = 6; player[5] = 27;       // hand/deck
    player[6] = 3; player[8] = 2;        // grave/banished
    player[9] = 3;                      // Exodia (0x200) + deck-out (0x100)
    player[0x10] = 0xD7; player[0x11] = 0x02; // index 727
    player[0x10 + 2*0x90] = 0xAB;
    player[0x10 + 2*0x90 + 1] = 0xF4;  // high nibble ignored
    player[0x10 + 5*0x90] = 0x58;
    player[0x10 + 5*0x90 + 6] = 2;
    player[0x10 + 10*0x90] = 0x4D;
    player[0x10 + 10*0x90 + 6] = 3;
    player[0x6D0] = 0xA2; player[0x6D1] = 0x05;
    player[0x6D4] = 0xCF; player[0x6D5] = 0xF4;
    auto s = yugire::parse_player_block(player);
    if (s.life_points != 8000 || s.hand_count != 6 || s.deck_count != 27)
        return 1;
    if (s.graveyard_count != 3 || s.banished_count != 2 ||
        s.terminal_flags != 0x300) return 2;
    if (s.monsters[0] != 727 || s.monsters[1] != 0 ||
        s.monsters[2] != 0x4AB || s.monsters[3] != 0 || s.monsters[4] != 0)
        return 3;
    if (s.other_field[0] != 0x58 || s.other_field[5] != 0x4D ||
        s.other_field[1] != 0) return 7;
    if (s.field_position_words[5] != 2 || s.field_position_words[10] != 3)
        return 8;
    if (s.hand_indices[0] != 0x5A2 || s.hand_indices[1] != 0x4CF ||
        s.hand_indices[6] != 0) return 9;
    auto empty = yugire::parse_player_block(nullptr);
    if (empty.life_points != 0 || empty.monsters[0] != 0) return 4;
    yugire::DuelSnapshot snap{};
    if (yugire::read_duel_snapshot(nullptr, &snap)) return 5;
    if (yugire::read_duel_snapshot(player, nullptr)) return 6;
    return 0;
}
