#!/usr/bin/env bash
# A03/A04: run locally with private data.dat; no native exe modification.
set -euo pipefail
if [[ $# -ne 1 || ! -f "$1" ]]; then echo "Usage: bash $0 /path/to/data.dat" >&2; exit 2; fi
root="$(cd "$(dirname "$0")/../.." && pwd)"
dat="$(realpath "$1")"
cd "$root"
mkdir -p scratch/actions-console
scenario=scripts/fixtures/duel-console-visual-smoke.txt
dotnet run --project src/Joey.Lab -- duel-console --dat "$dat" "$scenario" | tee scratch/actions-console/headless.txt
dotnet run --project src/Joey.App -- "$dat" "--duel-script=$scenario" | tee scratch/actions-console/visual.txt
# Compare the execution trace and fixed-layout snapshot lines, excluding UI diagnostics.
python - <<'PY'
import pathlib, re
root = pathlib.Path("scratch/actions-console")
def rows(name):
    text = (root/name).read_text(encoding="utf-8", errors="replace")
    if "Script completed." not in text:
        raise SystemExit(f"{name}: incomplete script")
    return [line for line in text.splitlines()
            if re.match(r"^(\d+: |(?:[-+] )?(?:turn=|queue busy=|legacy choice:|side [01] ))", line)]
a, b = rows("headless.txt"), rows("visual.txt")
if a != b:
    import difflib
    print("\n".join(difflib.unified_diff(a,b,fromfile="headless",tofile="visual",lineterm="")))
    raise SystemExit("Trace/state mismatch")
print("Matching trace/state; still inspect visual order manually.")
PY
