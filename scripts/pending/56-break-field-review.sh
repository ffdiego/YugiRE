#!/usr/bin/env bash
set -euo pipefail
if [[ $# -lt 2 || $# -gt 3 ]]; then
    echo "Usage: $0 /path/to/data.dat ID,HEX [perspective]" >&2
    exit 2
fi
data="$(realpath -- "$1")"
test -f "$data"
review="$2"
perspective="${3:-0}"
cd "$(dirname "$0")/../.."
mkdir -p scratch/visual-review
log="$(mktemp scratch/visual-review/break-review.XXXXXX.log)"
dotnet run --project src/Joey.App -- "$data" "--review-break=$review" "--review-perspective=$perspective" --no-audio 2>&1 | tee "$log"
