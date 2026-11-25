#!/usr/bin/env bash
# V17/V07: polygon geometry ABI and representative BGR24 dispatch.
# Does not repeat turn notice Draw or pending surface-vtable extraction.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 004223F0 00422540 00422840 00422E30 0052CF10 00433150 00546D70 00438100 00438E30 00530220 \
  004365C0 00437360 0052F570 00439B60 00424FC0 \
  --disassemble --instruction-count 512 --retry-missing \
  --instruction-window 0051EBD0:128 \
  --instruction-window 00438CD4:128 --instruction-window 00439A04:128 \
  --instruction-window 00530DFE:128 \
  --output scratch/ghidra-native-quad-raster-contract.zip
