#!/usr/bin/env bash
# V16: document the second duelend manifest consumer registered by 00483B10.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 00483C40 00444490 --disassemble --instruction-count 100 --retry-missing \
  --memory 005DBC48:16 --memory 005EBD80:24 \
  --output scratch/ghidra-result-fade.zip
