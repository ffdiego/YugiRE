#include "yugire/engine.hpp"

#if !defined(_WIN32) || (!defined(_M_IX86) && !defined(__i386__))
#error "Compile as 32-bit Windows, not x64."
#endif
static_assert(sizeof(void*) == 4, "x86 address size required");
static_assert(sizeof(unsigned int) == 4, "32-bit integer required");

// These intrinsics avoid C++ runtime/standard-library dependencies. The
// ordinary MSVC CMake configuration is still free to link its static CRT.
#if defined(_MSC_VER)
extern "C" long _InterlockedIncrement(volatile long*);
extern "C" long _InterlockedCompareExchange(volatile long*, long, long);
#pragma intrinsic(_InterlockedIncrement)
#pragma intrinsic(_InterlockedCompareExchange)
static unsigned int atomic_load(volatile long* p) {
    return static_cast<unsigned int>(_InterlockedCompareExchange(p, 0, 0));
}
static void atomic_inc(volatile long* p) { (void)_InterlockedIncrement(p); }
#else
static unsigned int atomic_load(volatile long* p) {
    return static_cast<unsigned int>(__atomic_load_n(p, __ATOMIC_SEQ_CST));
}
static void atomic_inc(volatile long* p) {
    (void)__atomic_add_fetch(p, 1, __ATOMIC_SEQ_CST);
}
#endif

namespace yugire { namespace engine {
namespace {
// Helper FUN_005784E0, known to produce the LP-change command (0x43).
// Original image base: 0x00400000; original helper VA: 0x005784E0.
constexpr unsigned int kDamageCommandRva = 0x001784E0;
using DamageCommand = unsigned int (__cdecl*)(unsigned int, unsigned int);
DamageCommand g_damage_command = nullptr;
unsigned int g_sparks_damage = 0;
unsigned int g_ookazi_damage = 0;
volatile long g_sparks_calls = 0;
volatile long g_ookazi_calls = 0;
volatile long g_sparks_emits = 0;
volatile long g_ookazi_emits = 0;

volatile long* calls_for(Card card) {
    switch (card) {
        case Card::Sparks: return &g_sparks_calls;
        case Card::Ookazi: return &g_ookazi_calls;
        default: return nullptr;
    }
}
volatile long* emits_for(Card card) {
    switch (card) {
        case Card::Sparks: return &g_sparks_emits;
        case Card::Ookazi: return &g_ookazi_emits;
        default: return nullptr;
    }
}
}

bool configure(void* image_base, unsigned int sparks_damage,
               unsigned int ookazi_damage) {
    if (!image_base || !((sparks_damage == 200) || (sparks_damage == 300)) ||
        !((ookazi_damage == 800) || (ookazi_damage == 900))) return false;
    auto* base = static_cast<unsigned char*>(image_base);
    g_damage_command = reinterpret_cast<DamageCommand>(base + kDamageCommandRva);
    g_sparks_damage = sparks_damage;
    g_ookazi_damage = ookazi_damage;
    g_sparks_calls = g_ookazi_calls = 0;
    g_sparks_emits = g_ookazi_emits = 0;
    return true;
}

unsigned int configured_damage(Card card) {
    switch (card) {
        case Card::Sparks: return g_sparks_damage;
        case Card::Ookazi: return g_ookazi_damage;
        default: return 0;
    }
}
unsigned int call_count(Card card) {
    volatile long* p = calls_for(card);
    return p ? atomic_load(p) : 0;
}
unsigned int emit_count(Card card) {
    volatile long* p = emits_for(card);
    return p ? atomic_load(p) : 0;
}
void record_call(Card card) {
    volatile long* p = calls_for(card);
    if (p) atomic_inc(p);
}
void emit_damage(Card card, unsigned int target_side) {
    if (g_damage_command) {
        g_damage_command(target_side & 1u, configured_damage(card));
        volatile long* p = emits_for(card);
        if (p) atomic_inc(p);
    }
}
}} // yugire::engine
