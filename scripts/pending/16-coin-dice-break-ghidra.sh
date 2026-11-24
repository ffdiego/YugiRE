#!/usr/bin/env bash
# V20/V21/V22: missing virtual methods and exact caller argument order.
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 00457500 00457520 00456B70 00456B90 00450FB0 00450FD0 004571D0 004568C0 00450C30 0040A7C0 0040A900 0040A6A0 00404B40 \
  --disassemble --instruction-count 1024 --retry-missing \
  --memory 005DB404:16 --memory 005DB3F4:16 --memory 005DB314:16 \
  --output scratch/ghidra-coin-dice-break-contract.zip
