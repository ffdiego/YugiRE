#!/usr/bin/env bash
# V32: identify exact virtual gate and dispatch of the equip travel object.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 00456490 00456730 00456740 0040A230 \
  --disassemble --instruction-count 256 --retry-missing \
  --memory 005DB3E4:16 --memory 005B9AEC:256 --memory 005B9838:692 \
  --output scratch/ghidra-equipment-travel-contract.zip
