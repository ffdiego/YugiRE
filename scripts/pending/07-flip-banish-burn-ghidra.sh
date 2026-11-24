#!/usr/bin/env bash
# PENDING: P051/P052 FLIP cost banishment vs shared 800 direct damage.
# Run only with locally licensed original Joey data/Ghidra project.
# Never commit proprietary executable, DAT, RAM or exported ZIP.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
if [[ ! -f ghidra-projects/Joey.gpr ]]; then
  echo 'Missing Ghidra project: ghidra-projects/Joey.gpr' >&2
  exit 2
fi
python3 scripts/export_ghidra_visual_bundle.py \
  --targets \
    00582E60 00582EA0 0057DDD0 0057DDF0 \
    00584E60 005784E0 00578B80 005BCEA0 005BCC70 \
    005BB720 0057EAC0 00567620 005B6690 \
  --disassemble --instruction-count 640 --retry-missing \
  --output scratch/ghidra-flip-banish-burn-contract.zip
echo 'Output: scratch/ghidra-flip-banish-burn-contract.zip'
echo 'Expected: assembly/decomp for P051/P052 availability and cost producers.'
echo 'Only share privately; do not commit binary exports.'
