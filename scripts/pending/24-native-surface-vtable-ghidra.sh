#!/usr/bin/env bash
# V17/V30: exact surface vtable +98 and allocation chain, not another Vija Draw dump.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 00547180 004ABDD0 --disassemble --instruction-count 512 --retry-missing \
  --instruction-window 004AD9C0:96 \
  --memory 005DC6EC:228 \
  --output scratch/ghidra-native-surface-vtable-contract.zip
