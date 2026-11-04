#pragma once
#include "card_request.hpp"

namespace yugire {
// The resource IDs are the TABLE IDs, not the internal CARD_ID indices.
enum class Card : unsigned int { Sparks = 342, Raimei = 1007 };
enum class EffectMode : unsigned int {
    Damage = 1,
    DrawFive = 2,
    DamageAndDrawFive = 3
};

namespace engine {
// Called once before either pointer is replaced. Exact EXE SHA is validated
// by the Python host, not by this DLL. A base address is passed explicitly so
// the DLL does not guess whether joey_pc.exe was relocated by ASLR.
bool configure(void* image_base, unsigned int sparks_damage,
               unsigned int raimei_damage, unsigned int sparks_mode,
               unsigned int raimei_mode);
unsigned int configured_mode(Card card);
unsigned int configured_damage(Card card);
unsigned int call_count(Card card);
unsigned int emit_count(Card card);
unsigned int draw_batch_count(Card card);
unsigned int manual_draw_batch_count();
void record_call(Card card);
void emit_damage(Card card, unsigned int target_side);
void emit_draw_cards(Card card, unsigned int activating_side, unsigned int count);
// May ONLY be called from the game's duel-update thread, never from RPC.
// Accepts an explicit 0/1 player side and exactly 5 cards in this experiment.
bool draw_from_console(unsigned int side, unsigned int count);
} // namespace engine
} // namespace yugire
