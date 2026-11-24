#!/usr/bin/env bash
# CARDS: P117/P159/P160 missing resurrection contracts.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 00580710 005886C0 00593530 005816E0 0058A4A0 00581C90 0057C5C0 00576F40 00577CF0 0056B440 005674E0 \
  --disassemble --instruction-count 768 --retry-missing \
  --output scratch/ghidra-revival-spells-contract.zip
echo "Share scratch/ghidra-revival-spells-contract.zip privately; never commit native assets."
