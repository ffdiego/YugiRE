#!/usr/bin/env bash
# V16: producer and scene dispatch for the result banner fade inventoried in 36.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 00447000 --disassemble --instruction-count 640 --retry-missing \
  --memory 00447848:32 --memory 005DBBC8:16 \
  --output scratch/ghidra-result-scene.zip
