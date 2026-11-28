#!/usr/bin/env bash
# V09/V10: cursor consumer and click-wave lifecycle, not card action effects.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 0047FE90 0047FEF3 004800D4 00410F31 0054B390 0043A5D0 00549080 00549060 \
  --disassemble --instruction-count 256 --retry-missing \
  --output scratch/ghidra-cursor-click-contract.zip
