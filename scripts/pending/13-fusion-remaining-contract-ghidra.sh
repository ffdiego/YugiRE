#!/usr/bin/env bash
# V11/V17: only NEW evidence; do not rerun bundle 01.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 00422C00 00422840 00422E30 00486CE0 00487930 00487DF0 \
  --disassemble --instruction-count 1024 --retry-missing \
  --instruction-window 00452B91:512 --memory 005DCD00:8 \
  --output scratch/ghidra-fusion-remaining-contract.zip
