#!/usr/bin/env bash
# C02.9: complement 30 with only the two Final Destiny callbacks.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 00589970 005813D0 --disassemble --instruction-count 768 --retry-missing \
  --output scratch/ghidra-final-destiny-contract.zip
