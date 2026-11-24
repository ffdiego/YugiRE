#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 005BC620 005B7BD0 005BC5D0 005A59B0 005A41C0 005A8090 \
    005B7B50 005A5A40 005A5120 005A8A50 \
  --disassemble --instruction-count 512 --retry-missing \
  --memory 005EFB78:28 --memory 005EFB94:28 \
  --output scratch/ghidra-actions-turn-phases.zip
echo 'Share scratch/ghidra-actions-turn-phases.zip privately; never commit native assets.'
