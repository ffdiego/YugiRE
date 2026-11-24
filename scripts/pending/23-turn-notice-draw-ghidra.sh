#!/usr/bin/env bash
# V07: missing Update/Draw and virtual completion, not the ACTIONS phase table.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 00480C20 00480C60 --disassemble --instruction-count 1024 --retry-missing \
  --memory 005DBB1C:16 --memory 005DBB2C:16 \
  --output scratch/ghidra-turn-notice-draw-contract.zip
