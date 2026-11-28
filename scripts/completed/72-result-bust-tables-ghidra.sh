#!/usr/bin/env bash
# V31 result: recover only missing selector and descriptor names.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 00416050 00416260 \
  --disassemble --instruction-count 512 --retry-missing \
  --memory 005DA7E0:16 --memory 005F04E0:84 \
  --output scratch/ghidra-result-bust-tables.zip
