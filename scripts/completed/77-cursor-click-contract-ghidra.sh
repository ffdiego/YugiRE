#!/usr/bin/env bash
# V09/V10: cursor consumer and click-wave lifecycle, not card action effects.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 0047FE90 0054B030 0054B100 0054B240 00451F70 00450FB0 00451FB0 00410F31 \
  --disassemble --instruction-count 256 --retry-missing \
  --memory 005DA594:20 --memory 005DB354:36 \
  --output scratch/ghidra-cursor-click-contract.zip
