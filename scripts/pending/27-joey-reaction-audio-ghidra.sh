#!/usr/bin/env bash
# V31/V18: targeted reaction routing and sound duration/descriptor evidence.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 00403900 00402E00 00416B80 005C04E0 005C0F00 005C0F80 \
  --disassemble --instruction-count 512 --retry-missing \
  --memory 005DA3C0:24 --memory 005DA840:16 --memory 005DA3FC:16 \
  --memory 005EFD48:1792 \
  --output scratch/ghidra-joey-reaction-audio-contract.zip
