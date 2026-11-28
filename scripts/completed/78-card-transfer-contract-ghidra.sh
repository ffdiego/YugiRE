#!/usr/bin/env bash
# V32: transfer consumers, descriptor requests and producer branches.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 00571670 00409240 00408EE0 0040CDB0 00576B30 00575AF0 --retry-missing \
  --instruction-window 00576D97:10 --instruction-window 00571670:164 \
  --instruction-window 00575ADF:60 --memory 005B9B6E:1 --memory 005B9B79:2 \
  --output scratch/ghidra-control-graveyard-travel.zip
