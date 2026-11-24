#!/usr/bin/env bash
# V09/V10: native action-mask/cursor selector; only missing assembly and table.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 005A0530 005B8910 005BF490 005BF4B0 005A0CD0 \
  --disassemble --instruction-count 512 --retry-missing \
  --memory 005DD980:52 --memory 005DA594:20 \
  --output scratch/ghidra-native-cursor-dispatch-contract.zip
