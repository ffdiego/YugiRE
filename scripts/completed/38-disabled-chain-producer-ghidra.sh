#!/usr/bin/env bash
# V12: preserve the Jinzo branch and the field scan that queues B1.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 005AB820 0059CCF0 --disassemble --instruction-count 440 --retry-missing \
  --memory 005ABCC0:53 \
  --output scratch/ghidra-disabled-chain-producer.zip
