#!/usr/bin/env bash
set -euo pipefail
if [[ $# -lt 1 ]]; then
    echo "Usage: $0 /path/to/data.dat [--reaction-wait-audio] [--reaction-scene-state=N]" >&2
    exit 2
fi
data="$(realpath -- "$1")"
shift
test -f "$data"
for option in "$@"; do
    case "$option" in
        --reaction-wait-audio|--reaction-scene-state=*) ;;
        *) echo "Unknown review option: $option" >&2; exit 2 ;;
    esac
done
cd "$(dirname "$0")/../.."
mkdir -p scratch/visual-review
log="$(mktemp scratch/visual-review/reaction07-review.XXXXXX.log)"
dotnet run --project src/Joey.App -- "$data" --review-reaction=07 --no-audio "$@" 2>&1 | tee "$log"
