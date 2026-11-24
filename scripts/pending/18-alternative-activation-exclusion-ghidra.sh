#!/usr/bin/env bash
# V25/V26: Draws not present in the current SQLite.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 00450AD0 0043DBE0 00450730 00450A90 0043D810 \
  --disassemble --instruction-count 1024 --retry-missing \
  --memory 005DB304:16 --memory 005DACB4:16 \
  --output scratch/ghidra-alternative-activation-exclusion.zip
