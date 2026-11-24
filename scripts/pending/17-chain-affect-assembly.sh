#!/usr/bin/env bash
# V23/V24: existing transient decompilations are not assembly evidence.
# Only unresolved resource lookup, compositor operands and jump tables.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 0041F870 0040E260 0040E040 0040E1F0 \
  --disassemble --instruction-count 1024 --retry-missing \
  --output scratch/ghidra-chain-affect-assembly.zip
