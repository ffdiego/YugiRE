#!/usr/bin/env bash
# PENDING: selection+fusion Draw evidence. Requires local Ghidra Joey project.
# Do not commit the resulting private ZIP, game binaries or memory dumps.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
if [[ ! -f ghidra-projects/Joey.gpr ]]; then
  echo "Missing local Ghidra project ghidra-projects/Joey.gpr" >&2
  exit 2
fi
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 0043B280 0055DCF0 004524E0 004524F0 004877B0 004877F0 \
  --disassemble --instruction-count 512 --retry-missing \
  --memory 005DB570:4 --memory 005DCCF8:8 --memory 005DCCB0:8 \
  --output scratch/ghidra-selection-fusion-contract.zip
echo "Next: upload scratch/ghidra-selection-fusion-contract.zip privately."
echo "Runtime cosine table 00A52EE8 is NOT recovered by static Ghidra export."
