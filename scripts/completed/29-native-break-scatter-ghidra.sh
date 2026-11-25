#!/usr/bin/env bash
# V22: assembly of scatter + CRT table generator, not another break Draw.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 00558FB0 00558BF0 0055C020 005C3BF5 005C3C02 \
  --disassemble --instruction-count 512 --retry-missing \
  --output scratch/ghidra-native-break-scatter-contract.zip
