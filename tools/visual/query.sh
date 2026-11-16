#!/usr/bin/env bash
# Query an existing disposable Ghidra project; persistent source is never opened.
set -euo pipefail
if [[ $# -lt 3 ]]; then
    echo 'Usage: query.sh GHIDRA_HOME DISPOSABLE_PROJECT_DIR OUTPUT [QueryArchive arguments...]' >&2
    exit 2
fi
ghidra_visual=$1
project_visual=$2
output_visual=$3
shift 3
repo_visual=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)
query_visual=$(mktemp -d /tmp/joey-visual-query.XXXXXX)
XDG_CONFIG_HOME="$query_visual/config" \
GHIDRA_HEADLESS_JAVA_OPTIONS="-Dapplication.settingsdir=$query_visual/settings -Dapplication.cachedir=$query_visual/cache" \
"$ghidra_visual/support/analyzeHeadless" "$project_visual" Joey \
    -process joey_pc.exe -noanalysis -readOnly -scriptPath "$repo_visual/scripts" \
    -postScript QueryArchive.java "$output_visual" "$@" \
    -log "$query_visual/headless.log" > "$query_visual/console.log" 2>&1
if [[ ! -s "$output_visual" ]] || rg -q '^ERROR ' "$query_visual/console.log"; then
    cat "$query_visual/console.log" >&2
    exit 1
fi
printf 'Report: %s\nLog: %s\n' "$output_visual" "$query_visual/headless.log"
