#!/usr/bin/env bash
# V04/V06: only the missing Draws and argument windows; do not redo whole inventory.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 004142C0 0047C660 --disassemble --instruction-count 1024 --retry-missing \
  --instruction-window 00414110:180 --instruction-window 00408470:150 \
  --memory 005DA710:16 --memory 005DBAA4:16 \
  --output scratch/ghidra-attack-life-draw-contract.zip
