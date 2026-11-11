#include "yugire/engine.hpp"
#include "yugire/field_target.hpp"

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
// FUN_00578B60(side, amount) produces ordinary 0x61 draw commands via
// FUN_00578860, as used by Pot of Greed's native 0x005887C0 callback.
constexpr unsigned int kDrawCommandRva = 0x00178B60;
// FUN_00576960(request, position_mask, destination_region, flags) is the
// original effect-destruction helper used by Man-Eater Bug. It handles
// eligibility, linked cards, Banisher and 0x79 command production.
// Synthetic request use is EXPERIMENTAL and requires in-game validation.
constexpr unsigned int kDestroyEffectRva = 0x00176960;
constexpr unsigned int kMonsterFieldRva = 0x00655D74; // 0x00A55D74 - 0x00400000
constexpr unsigned int kPlayerStride = 0xD44u;
constexpr unsigned int kFieldSlotStride = 0x90u;
using DamageCommand = unsigned int (__cdecl*)(unsigned int, unsigned int);
using DrawCommand = void (__cdecl*)(unsigned int, unsigned int);
using DestroyEffect = unsigned int (__cdecl*)(const std::uint16_t*,
                                              unsigned int, unsigned int,
                                              unsigned int);
DamageCommand g_damage_command = nullptr;
DrawCommand g_draw_command = nullptr;
DestroyEffect g_destroy_effect = nullptr;
unsigned char* g_image_base = nullptr;
unsigned int g_sparks_damage = 0;
unsigned int g_raimei_damage = 0;
unsigned int g_sparks_mode = 1;
unsigned int g_raimei_mode = 1;
volatile long g_sparks_calls = 0;
volatile long g_raimei_calls = 0;
volatile long g_sparks_emits = 0;
volatile long g_raimei_emits = 0;
volatile long g_sparks_draws = 0;
volatile long g_raimei_draws = 0;
volatile long g_manual_draws = 0;
volatile long g_manual_damages = 0;
volatile long g_manual_destroy_attempts = 0;

volatile long* calls_for(Card card) {
    switch (card) {
        case Card::Sparks: return &g_sparks_calls;
        case Card::Raimei: return &g_raimei_calls;
        default: return nullptr;
    }
}
volatile long* emits_for(Card card) {
    switch (card) {
        case Card::Sparks: return &g_sparks_emits;
        case Card::Raimei: return &g_raimei_emits;
        default: return nullptr;
    }
}
volatile long* draws_for(Card card) {
    switch (card) {
        case Card::Sparks: return &g_sparks_draws;
        case Card::Raimei: return &g_raimei_draws;
        default: return nullptr;
    }
}
}

bool configure(void* image_base, unsigned int sparks_damage,
               unsigned int raimei_damage, unsigned int sparks_mode,
               unsigned int raimei_mode) {
    // Keep this cap below 16-bit command-field limits; 8000 LP is the
    // standard starting total. Large-damage behavior still needs live tests.
    constexpr unsigned int kMinDamage = 1;
    constexpr unsigned int kMaxDamage = 9999;
    if (!image_base || sparks_damage < kMinDamage || sparks_damage > kMaxDamage ||
        raimei_damage < kMinDamage || raimei_damage > kMaxDamage ||
        sparks_mode < 1 || sparks_mode > 3 ||
        raimei_mode < 1 || raimei_mode > 3) return false;
    auto* base = static_cast<unsigned char*>(image_base);
    g_damage_command = reinterpret_cast<DamageCommand>(base + kDamageCommandRva);
    g_draw_command = reinterpret_cast<DrawCommand>(base + kDrawCommandRva);
    g_destroy_effect = reinterpret_cast<DestroyEffect>(base + kDestroyEffectRva);
    g_image_base = base;
    g_sparks_damage = sparks_damage;
    g_raimei_damage = raimei_damage;
    g_sparks_mode = sparks_mode;
    g_raimei_mode = raimei_mode;
    g_sparks_calls = g_raimei_calls = 0;
    g_sparks_emits = g_raimei_emits = 0;
    g_sparks_draws = g_raimei_draws = 0;
    g_manual_draws = 0;
    g_manual_damages = 0;
    g_manual_destroy_attempts = 0;
    return true;
}

unsigned int configured_damage(Card card) {
    switch (card) {
        case Card::Sparks: return g_sparks_damage;
        case Card::Raimei: return g_raimei_damage;
        default: return 0;
    }
}
unsigned int configured_mode(Card card) {
    switch (card) {
        case Card::Sparks: return g_sparks_mode;
        case Card::Raimei: return g_raimei_mode;
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
unsigned int draw_batch_count(Card card) {
    volatile long* p = draws_for(card);
    return p ? atomic_load(p) : 0;
}
unsigned int manual_draw_batch_count() {
    return atomic_load(&g_manual_draws);
}
unsigned int manual_damage_command_count() {
    return atomic_load(&g_manual_damages);
}
unsigned int manual_destroy_attempt_count() {
    return atomic_load(&g_manual_destroy_attempts);
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
// Shared producer for effects and console commands. This schedules native
// 0x61 draw commands; the engine resolves them asynchronously. Never call
// from a Frida RPC thread: this is only entered on the game/duel thread.
static bool queue_draw_cards(unsigned int side, unsigned int count) {
    // Native draw helper supports variable quantities. Keep this initial
    // effect interface bounded to 1..5; console commands restrict to 5.
    if (!g_draw_command || side > 1u || count == 0u || count > 5u) return false;
    g_draw_command(side, count);
    return true;
}
// Draw recipient is the card controller, NOT request[6], which the original
// target-preparation callback fills with the opponent side.
void emit_draw_cards(Card card, unsigned int activating_side, unsigned int count) {
    if (queue_draw_cards(activating_side, count)) {
        volatile long* p = draws_for(card);
        if (p) atomic_inc(p);
    }
}
bool draw_from_console(unsigned int side, unsigned int count) {
    if (count != 5u || !queue_draw_cards(side, count)) return false;
    atomic_inc(&g_manual_draws);
    return true;
}
bool damage_from_console(unsigned int side, unsigned int amount) {
    if (!g_damage_command || side > 1u || amount == 0u || amount > 9999u)
        return false;
    g_damage_command(side, amount);
    atomic_inc(&g_manual_damages);
    return true;
}
bool destroy_from_console(unsigned int side, unsigned int slot,
                          unsigned int expected_word) {
    std::uint32_t mask = 0;
    if (!g_destroy_effect || !g_image_base ||
        !monster_slot_mask(side, slot, &mask)) return false;
    // Read fresh state after the queue is idle. Checking the occurrence key
    // avoids hitting a replacement card that happened to reuse the same slot.
    const auto* word = reinterpret_cast<const volatile std::uint32_t*>(
        g_image_base + kMonsterFieldRva +
        side * kPlayerStride + slot * kFieldSlotStride);
    if (!same_field_card(*word, expected_word)) return false;

    // 20-byte, stack-local request; internal index 753 is Man-Eater Bug.
    // This mirrors the request fields consumed by FUN_00576960, WITHOUT
    // editing the game's field records or bypassing the native filters.
    std::uint16_t request[10] = {};
    request[0] = 753u;
    request[1] = static_cast<std::uint16_t>(side);
    (void)g_destroy_effect(request, mask, 14u, 2u);
    atomic_inc(&g_manual_destroy_attempts);
    // This means "native helper invoked", not "card was destroyed":
    // native filters may refuse the target and resolution is asynchronous.
    return true;
}
}} // yugire::engine
