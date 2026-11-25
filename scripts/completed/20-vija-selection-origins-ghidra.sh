#!/usr/bin/env bash
# V30 and V11: missing Vija Draw; static origin tables not yet captured.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 004584C0 --disassemble --instruction-count 1024 --retry-missing \
  --instruction-window 004580F0:240 --instruction-window 00409AB0:220 \
  --memory 005DA410:20 --memory 005DB414:16 --memory 005DB424:16 \
  --memory 005DB5B0:80 \
  --output scratch/ghidra-vija-selection-origins.zip
