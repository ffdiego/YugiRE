#!/usr/bin/env bash
# CARDS C02.5–C02.7: only missing P074/P077/P114 callback bodies in cumulative v2.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 0057B180 005883C0 00581F40 0057B680 00580D20 0057A9A0 \
  --disassemble --instruction-count 768 --retry-missing \
  --output scratch/ghidra-selective-destruction-contract.zip
echo 'Share scratch/ghidra-selective-destruction-contract.zip privately; never commit native assets.'
