/*
 * Deliberately small Windows x86 fixture for a Frida interception experiment.
 * Never load any Joey binary or game data. Build with MSVC cl.exe (x86 target).
 */
#include <windows.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#if !defined(_MSC_VER) || !defined(_M_IX86)
#error This fixture requires the MSVC x86 compiler (x86 Native Tools Prompt).
#endif

#define FIXTURE_MAGIC "YUGIRE_ESI_FIXTURE_V1"
#define EXPECTED_ESI 0x00000D44u

struct ProbeResult {
    uint32_t restored_esi;
    uint32_t fallthrough_marker; /* 1: AL=0 fallthrough; 0: AL=2/JNE taken */
    uint32_t esp_after_first_pop;
};

/*
 * At function entry, [ESP] is a genuine return address.
 * After push esi / mov esi,D44h / push esi, [ESP] is a saved ESI value,
 * NOT a return address. An interior call listener must not rewrite it.
 *
 * The exact six-byte hook window is A8 02 75 1B 0C 02:
 *   test al,2; jne +0x1b; or al,2.
 * The non-taken path then has 5 bytes (mov ecx,1) + 20 x NOP, so both
 * branches join at probe+31. The probe address is found via a unique signature.
 *
 * Arguments under cdecl: [ESP+4]=al_input, [ESP+8]=ProbeResult* at entry.
 * No dereference is ever performed through the potentially corrupted ESI.
 */
__declspec(naked) static void __cdecl esi_probe_fixture(
    unsigned int al_input, struct ProbeResult *out)
{
    __asm {
        push esi
        mov esi, 0D44h
        push esi
        mov eax, dword ptr [esp + 0Ch]
        and eax, 2
        xor ecx, ecx
        /* Exactly 6 bytes at this instruction-level interception point. */
        _emit 0A8h
        _emit 002h
        _emit 075h
        _emit 01Bh
        _emit 00Ch
        _emit 002h
        /* If JNE not taken, mark the fallthrough path. */
        mov ecx, 1
        /* 20 NOP bytes after the 5-byte mov ecx,1. */
        _emit 090h
        _emit 090h
        _emit 090h
        _emit 090h
        _emit 090h
        _emit 090h
        _emit 090h
        _emit 090h
        _emit 090h
        _emit 090h
        _emit 090h
        _emit 090h
        _emit 090h
        _emit 090h
        _emit 090h
        _emit 090h
        _emit 090h
        _emit 090h
        _emit 090h
        _emit 090h
        /* JNE destination: probe+31. */
        pop esi
        mov edx, dword ptr [esp + 0Ch]
        mov dword ptr [edx], esi
        mov dword ptr [edx + 4], ecx
        mov dword ptr [edx + 8], esp
        pop esi
        ret
    }
}

static unsigned char *find_unique_probe_address(void)
{
    static const unsigned char signature[] = {0xA8, 0x02, 0x75, 0x1B, 0x0C, 0x02};
    unsigned char *base = (unsigned char *)(uintptr_t)esi_probe_fixture;
    unsigned char *found = NULL;
    unsigned int i;
    /* Scan only the short known fixture function, not arbitrary process memory. */
    for (i = 0; i + sizeof(signature) <= 128; i++) {
        if (memcmp(base + i, signature, sizeof(signature)) == 0) {
            if (found != NULL)
                return NULL;
            found = base + i;
        }
    }
    return found;
}

int main(int argc, char **argv)
{
    struct ProbeResult out = {0, 0, 0};
    unsigned int al_input;
    unsigned int expected_branch;
    char command[32];
    unsigned char *probe;
    int healthy;

    if (argc != 2 || (strcmp(argv[1], "0") != 0 && strcmp(argv[1], "2") != 0)) {
        fputs("Usage: esi_fixture_x86.exe 0|2\n", stderr);
        return 2;
    }
    al_input = (unsigned int)atoi(argv[1]);
    expected_branch = (al_input == 2) ? 0u : 1u;
    probe = find_unique_probe_address();
    if (probe == NULL) {
        fputs("ERROR: missing/ambiguous six-byte probe signature\n", stderr);
        return 3;
    }
    printf(FIXTURE_MAGIC " READY pid=%lu probe=0x%08lX al=%u\n",
           (unsigned long)GetCurrentProcessId(),
           (unsigned long)(uintptr_t)probe, al_input);
    fflush(stdout);
    if (fgets(command, sizeof(command), stdin) == NULL || strcmp(command, "GO\n") != 0) {
        fputs("ERROR: explicit GO command required\n", stderr);
        return 4;
    }

    esi_probe_fixture(al_input, &out);
    healthy = out.restored_esi == EXPECTED_ESI && out.fallthrough_marker == expected_branch;
    printf(FIXTURE_MAGIC " RESULT al=%u esi=0x%08lX route=%lu esp=0x%08lX healthy=%d\n",
           al_input, (unsigned long)out.restored_esi,
           (unsigned long)out.fallthrough_marker,
           (unsigned long)out.esp_after_first_pop, healthy);
    fflush(stdout);
    Sleep(150); /* Give the Frida host time to retrieve the last small event. */
    return healthy ? 0 : 20;
}
