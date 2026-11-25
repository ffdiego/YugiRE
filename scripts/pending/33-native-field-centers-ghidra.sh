#!/usr/bin/env bash
# V02/V05/V06: raw static centers, no repeat of Draw or DAT collections.
set -euo pipefail
cd "$(dirname "$0")/../.."
# Exporter requires at least one function: use the table consumer for provenance.
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 0045C280 \
  --memory 005DB560:80 --memory 005DB4E8:80 \
  --output scratch/ghidra-native-field-centers-contract.zip
