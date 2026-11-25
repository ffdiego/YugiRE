#!/usr/bin/env bash
# A06: missing P164/P118/P087 callbacks in cumulative SQLite v2.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 00581770 0058AC10 005887C0 00593CA0 0057A930 005861F0 00594200 \
  --disassemble --instruction-count 512 --retry-missing \
  --output scratch/ghidra-actions-battle-callbacks.zip
