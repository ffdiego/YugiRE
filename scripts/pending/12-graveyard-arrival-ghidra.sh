#!/usr/bin/env bash
# C07: unresolved historical discard gates and missing callback functions.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 005859C0 00585AE0 00584610 0057F550 00593A20 00575AF0 00575970 005759A0 005A8E10 005A90E0 005A92D0 005995E0 005999D0 \
  --disassemble --instruction-count 768 --retry-missing \
  --output scratch/ghidra-graveyard-arrival-contract.zip
echo "Share scratch/ghidra-graveyard-arrival-contract.zip privately; never commit native assets."
