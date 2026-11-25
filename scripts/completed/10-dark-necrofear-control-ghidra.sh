#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 00583050 005944D0 005906B0 0057A470 00579CE0 0056C510 \
    00576F40 00577CF0 00579330 005783C0 00579790 005747E0 \
    0059DEF0 00599E30 005AB640 0059DF40 005778F0 0056B440 \
  --disassemble --instruction-count 768 --retry-missing \
  --output scratch/ghidra-dark-necrofear-control.zip
echo 'Share scratch/ghidra-dark-necrofear-control.zip privately; never commit native assets.'
