#!/usr/bin/env bash
# PENDING: determine original card status indicators, from the shared
# duel/card/card.txt sprite bank through native drawing and phase gates.
# Requires private local Ghidra project; never version EXE, DAT or ZIP.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
if [[ ! -f ghidra-projects/Joey.gpr ]]; then
  echo "Missing local Ghidra project ghidra-projects/Joey.gpr" >&2
  exit 2
fi
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 004182E0 00418560 00418B60 004194D0 00419B70 \
            0041A070 0041A3B0 0040B270 0040B370 005B9470 \
            0041A800 00413DB0 00417F40 004803C0 00480690 \
  --disassemble --instruction-count 1024 --retry-missing \
  --output scratch/ghidra-card-status-contract.zip
echo "Share privately: scratch/ghidra-card-status-contract.zip"
echo "Separately summarize card.txt names/dimensions from your own data.dat:"
echo "  dotnet run --project src/Joey.App -- /path/to/data.dat --inspect-visual-dat"
echo "  python3 scripts/summarize_native_card_status_dat.py"
echo "Share privately: scratch/visual-review/card-status-manifests.json"
