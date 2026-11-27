#!/usr/bin/env bash
# V04: only missing x87 constants and conversion helper, not the attack Draw.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 005C3E40 --disassemble --instruction-count 64 --retry-missing \
  --memory 005DA708:8 --memory 005DA728:16 \
  --output scratch/ghidra-native-sword-angle-contract.zip
