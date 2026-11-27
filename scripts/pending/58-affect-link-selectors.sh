#!/usr/bin/env bash
# V24: missing static switch targets and binding producers; no live duel.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 00585200 00579C40 00593970 00586EF0 00586FB0 005849D0 \
  --disassemble --instruction-count 384 --retry-missing \
  --memory 0040E918:72 \
  --output scratch/ghidra-affect-link-selectors.zip
