#!/usr/bin/env bash
# PENDING: Greenkappa P085 + Tornado Bird P255 two-Spell/Trap-target callbacks.
# Extract assembly and decomp from the local, read-only Ghidra Joey project.
# Do NOT commit generated ZIP, EXE, DAT, memory dumps, or proprietary assets.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
if [[ ! -f ghidra-projects/Joey.gpr ]]; then
  echo 'Missing local Ghidra project: ghidra-projects/Joey.gpr' >&2
  exit 2
fi
python3 scripts/export_ghidra_visual_bundle.py \
  --targets \
    0057FA90 0057AC40 00593F80 00586040 \
    00583120 0057B530 00597270 005909A0 \
    0056A320 005B6690 005B6E40 \
    00576300 00576200 00576960 00576940 00577A40 \
  --disassemble --instruction-count 512 --retry-missing \
  --output scratch/ghidra-flip-spelltrap-two-targets.zip
echo 'Result: scratch/ghidra-flip-spelltrap-two-targets.zip'
echo 'Share this ZIP privately for analysis; do not commit.'
echo 'Success: all seven missing callbacks have assembly or explicit unresolved status.'
