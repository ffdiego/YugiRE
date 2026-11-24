#!/usr/bin/env bash
# PENDING: ATK/DEF font dispatcher and exact coordinate/opacity contract.
# Requires local Ghidra project. Do not commit EXE, DAT or generated ZIP.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
if [[ ! -f ghidra-projects/Joey.gpr ]]; then
  echo "Missing local Ghidra project ghidra-projects/Joey.gpr" >&2
  exit 2
fi
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 00418A80 00418EF0 004194D0 00417F40 \
            00480200 00480230 00480300 00480500 \
  --disassemble --instruction-count 768 --retry-missing \
  --output scratch/ghidra-field-attributes-contract.zip
echo "Next: privately share scratch/ghidra-field-attributes-contract.zip"
echo "Inspect number.txt and fontm BMP dimensions via --inspect-visual-dat."
