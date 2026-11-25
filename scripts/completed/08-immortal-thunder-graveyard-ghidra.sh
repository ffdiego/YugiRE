#!/usr/bin/env bash
# PENDING: The Immortal of Thunder P065 dual-trigger, 3000/+5000 loss.
# Use a local Ghidra project of the original Win32 executable.
# Do not commit any native program data or exported ZIP.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
if [[ ! -f ghidra-projects/Joey.gpr ]]; then
  echo 'Missing local Ghidra project: ghidra-projects/Joey.gpr' >&2
  exit 2
fi
python3 scripts/export_ghidra_visual_bundle.py \
  --targets \
    00581040 0057C5F0 00585970 00567620 \
    00576F40 00577CF0 0056AF90 005784E0 \
    0057EAC0 005BB720 005BA490 00598C40 \
  --disassemble --instruction-count 768 --retry-missing \
  --output scratch/ghidra-immortal-thunder-contract.zip
echo 'Output: scratch/ghidra-immortal-thunder-contract.zip'
echo 'Need: P065 callback decomp and occurrence-state bits / field-to-GY timing.'
echo 'Share privately for analysis; never commit original EXE/DAT/ZIP.'
