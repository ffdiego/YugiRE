#!/usr/bin/env bash
# PENDING: Prohibition 1162 link semantics, assignment timing, and continuous
# effect precedence with Jinzo / Royal Decree / Royal Command.
# Prerequisite: local Ghidra project matching joey_pc.exe; ZIP is private.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
if [[ ! -f ghidra-projects/Joey.gpr ]]; then
  echo 'Missing local Ghidra project: ghidra-projects/Joey.gpr' >&2
  exit 2
fi
python3 scripts/export_ghidra_visual_bundle.py \
  --targets \
    0058C430 00595F20 0056B440 005674E0 0059D370 \
    00569820 005699F0 005699C0 00569B00 00569B30 \
    00598D70 00598C40 0057EAC0 0057EE80 005BB720 \
    005872B0 0056AA70 \
  --disassemble --instruction-count 768 --retry-missing \
  --output scratch/ghidra-prohibition-links-contract.zip
echo 'Result: scratch/ghidra-prohibition-links-contract.zip'
echo 'Success criterion: primary/target-preparation Prohibition callbacks plus'
echo 'link types 9/11 and declared-identity producer resolved or marked missing.'
echo 'Share generated ZIP privately for investigation; do not commit EXE/DAT/ZIP.'
