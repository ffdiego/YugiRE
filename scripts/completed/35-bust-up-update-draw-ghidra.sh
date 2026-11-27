#!/usr/bin/env bash
# V16/V31: Update, Draw and the reaction busy predicate from vtables already collected in 27.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 00414CF0 00414DC0 00415580 00415840 00416050 00416260 00416BB0 00416BC0 \
  --disassemble --instruction-count 512 --retry-missing \
  --output scratch/ghidra-bust-up-update-draw.zip
