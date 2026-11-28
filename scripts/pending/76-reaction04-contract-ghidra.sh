#!/usr/bin/env bash
# V18/V31 reaction04: header 12 and sound descriptor name.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 00416BC0 00417680 004030F0 00405CB0 \
  --disassemble --instruction-count 128 --retry-missing \
  --memory 005F0540:12 \
  --output scratch/ghidra-reaction04-contract.zip
