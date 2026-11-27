#!/usr/bin/env bash
# V17: YGA extension branch, alpha allocation selector, and its jump table.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 004AABC0 004ABC10 --disassemble --instruction-count 700 --retry-missing \
  --memory 004ABDAC:28 --memory 005EC968:4 \
  --output scratch/ghidra-yga-surface-selector.zip
