#!/usr/bin/env bash
# V31: only the missing mouth selector and string pointers from the known opening voice descriptors.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 00415840 00415580 \
  --disassemble --instruction-count 256 --retry-missing \
  --memory 005DA798:16 --memory 005F0780:36 \
  --output scratch/ghidra-opening-bust-tables.zip
