#!/usr/bin/env bash
set -euo pipefail

repository=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)
workspace=$(dirname -- "$repository")
ghidra_dir=${GHIDRA_INSTALL_DIR:-"$workspace/tools/ghidra_12.2_PUBLIC"}
if [[ $# -lt 1 || ! $1 =~ ^[a-zA-Z0-9_-]+\.txt$ ]]; then
    echo "Usage: bash scripts/run-archive-query.sh report.txt [VA|ref:VA|xref:VA|ins:VA:count|decode:VA:count|tempfunc:VA|text:string ...]" >&2
    exit 2
fi
report="$repository/docs/evidence/$1"
shift
scratch=$(mktemp -d "$repository/scratch/query-linux.XXXXXX")
if ! XDG_CONFIG_HOME="$scratch/config" \
    GHIDRA_HEADLESS_JAVA_OPTIONS="-Dapplication.settingsdir=$scratch/settings -Dapplication.cachedir=$scratch/cache" \
    "$ghidra_dir/support/analyzeHeadless" "$repository/ghidra-projects" Joey \
    -process joey_pc.exe -noanalysis -readOnly -scriptPath "$repository/scripts" \
    -postScript QueryArchive.java "$scratch/report.txt" "$@" \
    -log "$scratch/headless.log" > "$scratch/console.log" 2>&1; then
    cat "$scratch/console.log" >&2
    exit 1
fi
if [[ ! -s "$scratch/report.txt" ]] || rg -q '^ERROR ' "$scratch/console.log"; then
    cat "$scratch/console.log" >&2
    exit 1
fi
cp -- "$scratch/report.txt" "$report"
printf 'Report: %s\nLog: %s\n' "$report" "$scratch/headless.log"
