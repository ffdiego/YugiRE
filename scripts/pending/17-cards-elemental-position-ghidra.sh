#!/usr/bin/env bash
# CARDS C16: P253 position effect and phase dispatch evidence.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 0057B1F0 0057CD40 005907D0 005944D0 00569790 005A4990 005A76E0 0056B440 00593440 005B3FA0 \
  --disassemble --instruction-count 768 --retry-missing \
  --memory 005DD000:192 \
  --output scratch/ghidra-elemental-position-contract.zip
echo 'Share scratch/ghidra-elemental-position-contract.zip privately; never commit native assets.'
