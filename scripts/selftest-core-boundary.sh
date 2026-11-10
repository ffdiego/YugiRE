#!/usr/bin/env bash
# Self-test for CoreBoundaryReach.java on a synthetic PE (no game files needed).
# Needs: i686-w64-mingw32-gcc and a Ghidra install (GHIDRA_INSTALL_DIR).
# The fingerprint check is disabled only in a temporary copy of the script.
set -euo pipefail
here=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
ghidra_dir=${GHIDRA_INSTALL_DIR:?set GHIDRA_INSTALL_DIR to the Ghidra folder}
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
cat > "$work/test.c" <<'C'
#include <windows.h>
typedef void (*fn)(void);
static volatile int g;
__attribute__((noinline)) static int pure_leaf(int x){ return x*3+1; }
__attribute__((noinline)) int pure_mid(int x){ return pure_leaf(x)+pure_leaf(x+1); }
__attribute__((noinline)) void mem_user(void){ void* p=HeapAlloc(GetProcessHeap(),0,16); HeapFree(GetProcessHeap(),0,p); }
__attribute__((noinline)) void present(void){ MessageBoxA(0,"x","y",0); }
__attribute__((noinline)) DWORD clock_user(void){ return timeGetTime(); }
__attribute__((noinline)) void state_a(void){ g+=pure_mid(1); }
__attribute__((noinline)) void state_b(void){ mem_user(); }
__attribute__((noinline)) void state_c(void){ present(); }
fn state_table[3] = { state_a, state_b, state_c };
__attribute__((noinline)) void dispatcher(int idx){ state_table[idx](); }
__attribute__((noinline)) void indirect_only(fn f){ f(); }
__attribute__((noinline)) void callback_only(void){ present(); }
__attribute__((noinline)) void core_root(int idx){ dispatcher(idx); g += clock_user(); indirect_only(state_a); indirect_only(callback_only); }
int start(void){ core_root(1); ExitProcess(0); return 0; }
C
i686-w64-mingw32-gcc -O0 -fno-stack-protector -nostdlib -nostartfiles -Wl,-e,_start \
    -o "$work/test.exe" "$work/test.c" -lkernel32 -luser32 -lwinmm
sym() { i686-w64-mingw32-nm "$work/test.exe" | awk -v s="_$1" '$3==s{print $1}'; }
mkdir -p "$work/scripts" "$work/proj"
sed 's/if (!EXPECTED_SHA.equalsIgnoreCase(sha)) {/if (false) {/' "$here/CoreBoundaryReach.java" > "$work/scripts/CoreBoundaryReach.java"
cd "$work"   # headless resolves a script name in the cwd first; keep it away from the repo
"$ghidra_dir/support/analyzeHeadless" "$work/proj" Test -import "$work/test.exe" \
    -scriptPath "$work/scripts" -postScript CoreBoundaryReach.java "$work/report.txt" \
    "$(sym core_root)" "table:$(sym state_table):3" addr:follow -deleteProject > "$work/console.log" 2>&1 \
    || { cat "$work/console.log"; exit 1; }
# analyzeHeadless exits 0 even when the script fails, so check the report exists
[[ -s "$work/report.txt" ]] || { echo "FAIL: no report produced"; grep -E '^ERROR' "$work/console.log" | head -5; trap - EXIT; echo "see $work/console.log"; exit 1; }
fail=0
expect() { grep -Eq "$1" "$work/report.txt" || { echo "FAIL: $2"; fail=1; }; }
expect 'ui +USER32!MessageBoxA'            'USER32 import reached'
expect 'time +WINMM!timeGetTime'           'WINMM import reached'
expect 'memory +KERNEL32!HeapAlloc'        'KERNEL32 heap import reached'
expect ' PURE '                            'at least one PURE function'
expect ' NEEDS_STUB '                      'at least one NEEDS_STUB function'
expect 'table [0-9a-f]+: 3 code pointer'   'table seeded with 3 entries'
expect 'sites=1'                           'unresolved indirect call reported'
grep -q "$(sym callback_only)" "$work/report.txt.functions.csv" || { echo "FAIL: addr:follow reached callback_only"; fail=1; }
[[ $fail -eq 0 ]] && echo "selftest OK" || { echo "see $work/report.txt"; trap - EXIT; exit 1; }
