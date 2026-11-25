#!/usr/bin/env bash
# PENDING: native chain-disabled visual, six frames over 24 native updates.
# Requires local Ghidra Joey project; original DAT manifest still needs inspection.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
if [[ ! -f ghidra-projects/Joey.gpr ]]; then
  echo "Missing local Ghidra project ghidra-projects/Joey.gpr" >&2
  exit 2
fi
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 00408730 00452F20 004531A0 004531C0 0045C140 \
  --disassemble --instruction-count 512 --retry-missing \
  --output scratch/ghidra-chain-disabled-contract.zip
echo "Next: upload scratch/ghidra-chain-disabled-contract.zip privately."
echo "Inspect j/duel/chain/disable.txt locally; frame index alone does not prove sprite name."
