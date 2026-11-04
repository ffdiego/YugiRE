// Minimal C++17 / x86 Win32 runtime: no C++ library or CRT dependency.
// The exported ABI and the callback's concrete behavior were reconstructed
// for joey_pc.exe SHA256 db8fe7b7ea4b12f18a9bb1777b5b8eab254011db2a52571a0264725505b6de.
// Export names are declared in joey_rules.def (without x86 decoration).

#if !defined(_WIN32) || !defined(_M_IX86) && !defined(__i386__)
#error "Build this library for 32-bit Windows (x86), not x64."
#endif

static_assert(sizeof(void*) == 4, "The Joey callback ABI uses 32-bit pointers.");
static_assert(sizeof(unsigned int) == 4, "The Joey callback ABI uses 32-bit integers.");
static_assert(sizeof(unsigned short) == 2, "The Joey callback returns a 16-bit value.");

#if defined(_MSC_VER)
extern "C" long _InterlockedIncrement(volatile long*);
extern "C" long _InterlockedCompareExchange(volatile long*, long, long);
#pragma intrinsic(_InterlockedIncrement)
#pragma intrinsic(_InterlockedCompareExchange)
static inline void count_call(volatile long* p) { (void)_InterlockedIncrement(p); }
static inline unsigned int load_calls(volatile long* p) {
    return static_cast<unsigned int>(_InterlockedCompareExchange(p, 0, 0));
}
#else
static inline void count_call(volatile long* p) { (void)__atomic_add_fetch(p, 1, __ATOMIC_SEQ_CST); }
static inline unsigned int load_calls(volatile long* p) {
    return static_cast<unsigned int>(__atomic_load_n(p, __ATOMIC_SEQ_CST));
}
#endif

namespace {
using DamageCommand = unsigned int (__cdecl *)(unsigned int side, unsigned int points);

static DamageCommand g_emitDamage = nullptr;
static unsigned int g_damage = 0;
static volatile long g_calls = 0;
static volatile long g_emitted = 0;

// Function RVA = 0x005784E0 - 0x00400000.
constexpr unsigned int kEmitDamageRva = 0x001784E0;
constexpr unsigned int kMagic = 0x59554731; // "YUG1"
}

// Must be invoked before installing the function pointer. The Python host
// verifies the loaded EXE hash before invoking this initialization function.
extern "C" unsigned int __cdecl YugiRE_Initialize(void* mainModuleBase, unsigned int damage) {
    if (mainModuleBase == nullptr || (damage != 200 && damage != 300)) return 0;
    const auto base = reinterpret_cast<unsigned char*>(mainModuleBase);
    g_emitDamage = reinterpret_cast<DamageCommand>(base + kEmitDamageRva);
    g_damage = damage;
    g_calls = 0;
    g_emitted = 0;
    return kMagic;
}

// Matches the dispatcher call: primary(CardRequest*, opaque_arg2),
// caller pops 8 bytes, EAX low 16 bits is the result.
extern "C" unsigned short __cdecl YugiRE_Sparks(const void* cardRequest,
                                                  void* /*opaque_arg2*/) {
    count_call(&g_calls);
    if (cardRequest == nullptr || g_emitDamage == nullptr) return 0;

    const auto* request = static_cast<const unsigned char*>(cardRequest);
    if ((request[4] & 0x04) == 0) {
        // The original target-preparation slot (+20) already selects the side.
        const unsigned int targetSide = static_cast<unsigned int>(request[6] & 1);
        g_emitDamage(targetSide, g_damage); // native engine: enqueues LP command 0x43
        count_call(&g_emitted);
    }
    return 0;
}

extern "C" unsigned int __cdecl YugiRE_GetCallCount() { return load_calls(&g_calls); }
extern "C" unsigned int __cdecl YugiRE_GetEmitCount() { return load_calls(&g_emitted); }
extern "C" unsigned int __cdecl YugiRE_GetConfiguredDamage() { return g_damage; }
