#!/usr/bin/env bash
# V27/V28/V29: precise operands for Layerfont; missing help Update/Draw.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 00455FF0 00456360 0045E3D0 0045E4A0 \
  --disassemble --instruction-count 1024 --retry-missing \
  --memory 005DB3C4:16 --memory 005DB3D4:16 --memory 005DB630:16 \
  --output scratch/ghidra-layerfont-help-contract.zip
