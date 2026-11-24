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
echo "Check both logs and visual order; close App after Script completed. No automatic fidelity assertion."
