#!/usr/bin/env bash
# Runs CoreBoundaryReach.java headless on the analyzed Joey project (read-only).
# Usage: bash scripts/run-core-boundary.sh report.txt [VA|root:VA|stop:VA|table:VA[:count]|depth:N|addr:follow ...]
# With no tokens the defaults apply: 005BC700, 005B7B50 and table 005EFBB0.
# Writes docs/evidence/<report> and docs/evidence/<report>.functions.csv
set -euo pipefail

repository=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
workspace=$(dirname -- "$repository")
ghidra_dir=${GHIDRA_INSTALL_DIR:-"$workspace/tools/ghidra_12.2_PUBLIC"}
if [[ $# -lt 1 || ! $1 =~ ^[a-zA-Z0-9_-]+\.txt$ ]]; then
    echo "Usage: bash scripts/run-core-boundary.sh report.txt [VA|root:VA|stop:VA|table:VA[:count]|depth:N|addr:follow ...]" >&2
    exit 2
fi
report="$repository/docs/evidence/$1"
shift
mkdir -p "$repository/scratch"
scratch=$(mktemp -d "$repository/scratch/core-boundary.XXXXXX")
if ! XDG_CONFIG_HOME="$scratch/config" \
    GHIDRA_HEADLESS_JAVA_OPTIONS="-Dapplication.settingsdir=$scratch/settings -Dapplication.cachedir=$scratch/cache" \
    "$ghidra_dir/support/analyzeHeadless" "$repository/ghidra-projects" Joey \
    -process joey_pc.exe -noanalysis -readOnly -scriptPath "$repository/scripts" \
    -postScript CoreBoundaryReach.java "$scratch/report.txt" "$@" \
    -log "$scratch/headless.log" > "$scratch/console.log" 2>&1; then
    cat "$scratch/console.log" >&2
    exit 1
fi
if [[ ! -s "$scratch/report.txt" ]] || grep -Eq '^(ERROR|.*ERROR .*CoreBoundaryReach)' "$scratch/console.log"; then
    cat "$scratch/console.log" >&2
    exit 1
fi
cp -- "$scratch/report.txt" "$report"
cp -- "$scratch/report.txt.functions.csv" "$report.functions.csv"
printf 'Report: %s\nCSV: %s\nLog: %s\n' "$report" "$report.functions.csv" "$scratch/headless.log"
