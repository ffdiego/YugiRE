#include "yugire/cards.hpp"
#include "yugire/engine.hpp"

#if !defined(_WIN32) || (!defined(_M_IX86) && !defined(__i386__))
#error "The Joey DLL must be Windows x86."
#endif
static_assert(sizeof(void*) == 4, "Requires x86");

// Names exported undecorated via yugire_rules.def. Frida must call these
// functions using its 'mscdecl' ABI, matching the Win32 __cdecl below.
constexpr unsigned int kInitMagic = 0x59554732;  // "YUG2"
constexpr unsigned int kAbiVersion = 1;

extern "C" unsigned int __cdecl YugiRE_GetAbiVersion() {
    return kAbiVersion;
}
extern "C" unsigned int __cdecl YugiRE_Initialize(
    void* image_base, unsigned int sparks_damage, unsigned int raimei_damage) {
    return yugire::engine::configure(image_base, sparks_damage, raimei_damage)
        ? kInitMagic : 0;
}
extern "C" unsigned short __cdecl YugiRE_Sparks(
    const yugire::CardRequest* request, void* opaque) {
    return yugire::cards::sparks(request, opaque);
}
extern "C" unsigned short __cdecl YugiRE_Raimei(
    const yugire::CardRequest* request, void* opaque) {
    return yugire::cards::raimei(request, opaque);
}
extern "C" unsigned int __cdecl YugiRE_GetCallCount(unsigned int resource_id) {
    return yugire::engine::call_count(static_cast<yugire::Card>(resource_id));
}
extern "C" unsigned int __cdecl YugiRE_GetEmitCount(unsigned int resource_id) {
    return yugire::engine::emit_count(static_cast<yugire::Card>(resource_id));
}
extern "C" unsigned int __cdecl YugiRE_GetConfiguredDamage(unsigned int resource_id) {
    return yugire::engine::configured_damage(static_cast<yugire::Card>(resource_id));
}
