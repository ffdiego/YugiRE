#!/usr/bin/env bash
# V30: missing mask compositor, existing Draw and manifest in v7.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 004CCD40 \
  --disassemble --instruction-count 1024 --retry-missing \
  --output scratch/ghidra-vija-mask-compositor.zip
