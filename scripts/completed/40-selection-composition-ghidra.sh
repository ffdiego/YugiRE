#!/usr/bin/env bash
# V11/V17: dispatch for the stable selection blit and software alpha branch.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 0041AB40 004CB740 --disassemble --instruction-count 540 --retry-missing \
  --instruction-window 004B2D12:48 --instruction-window 004B2DCF:46 \
  --memory 004B3140:44 --memory 004CBDAC:44 \
  --output scratch/ghidra-selection-composition.zip
