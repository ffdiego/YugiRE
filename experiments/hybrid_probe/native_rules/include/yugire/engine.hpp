#pragma once
#include "card_request.hpp"

namespace yugire {
// The resource IDs are the TABLE IDs, not the internal CARD_ID indices.
enum class Card : unsigned int { Sparks = 342, Raimei = 1007 };

namespace engine {
// Called once before either pointer is replaced. Exact EXE SHA is validated
// by the Python host, not by this DLL. A base address is passed explicitly so
// the DLL does not guess whether joey_pc.exe was relocated by ASLR.
bool configure(void* image_base, unsigned int sparks_damage,
               unsigned int raimei_damage);
unsigned int configured_damage(Card card);
unsigned int call_count(Card card);
unsigned int emit_count(Card card);
void record_call(Card card);
void emit_damage(Card card, unsigned int target_side);
} // namespace engine
} // namespace yugire
