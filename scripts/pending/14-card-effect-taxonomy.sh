#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
python3 scripts/card_effect_taxonomy.py --dat "${1:-../passion/data.dat}" --output scratch/card-taxonomy.json
