#!/usr/bin/env bash
# PENDING: Dimensional Warrior (255) P188 post-damage dual-banish.
# Needs local Joey PC Ghidra project; do not version exported ZIP/EXE/DAT.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
if [[ ! -f ghidra-projects/Joey.gpr ]]; then
  echo 'Missing Ghidra project: ghidra-projects/Joey.gpr' >&2
  exit 2
fi
python3 scripts/export_ghidra_visual_bundle.py \
  --targets \
    0058CC00 005784E0 00598590 005BB720 \
    0056A320 00576F40 00577CF0 00567620 \
    0057EAC0 005B9170 0056C510 \
  --disassemble --instruction-count 768 --retry-missing \
  --output scratch/ghidra-dimensional-warrior-banish.zip
echo 'Output: scratch/ghidra-dimensional-warrior-banish.zip'
echo 'Verify native after-damage timing, removal destination and callbacks.'
echo 'Share privately; never commit native executable or export ZIP.'
