"""Reproduce the static Parasite Paracide case study checks, not game execution."""

import hashlib
import json
import struct
from pathlib import Path

from validate_parasite_continuation import main as validate_continuation


def main():
    validate_continuation()
    repository = Path(__file__).resolve().parent.parent
    binary = (repository.parent / "passion/joey_pc.exe").read_bytes()
    checks = []
    if struct.unpack_from("<I", binary, 0x001ABCA8 + 3 * 4)[0] != 0x005AB8BB:
        raise ValueError("Placement mode 4 dispatch mismatch")
    for va, expected in [
        (0x005B957B, "6a00e8eeeee4ff"),
        (0x0047C60A, "e8a1790f00"),
        (0x00573FED, "668901"),
        (0x0056F824, "c74424280a000000"),
        (0x0057CD30, "e82b7cffff"),
    ]:
        offset = va - 0x00400000
        actual = binary[offset:offset + len(bytes.fromhex(expected))].hex()
        if actual != expected:
            raise ValueError(f"Instruction mismatch at {va:#x}")
        checks.append(dict(va=hex(va), file_offset=hex(offset), bytes=actual))
    # Arithmetic model of the signed, saturated LP update, not native execution.
    examples = []
    for initial in [0, 500, 1000, 8000, 65535]:
        current = initial
        for _ in range(10):
            current = max(0, current - 100)
        if current != max(0, initial - 1000):
            raise ValueError("Incremental LP update mismatch")
        examples.append(dict(initial=initial, final=current))
    report = dict(target_sha256=hashlib.sha256(binary).hexdigest(),
                  instruction_checks=checks, lp_model_cases=examples,
                  method="Static byte checks and arithmetic model; game not executed.",
                  scope="Functional card flow; arbitrary cross-card interactions not exhaustively tested.")
    (repository / "docs/evidence/parasite-lifecycle-validation.json").write_text(
        json.dumps(report, indent=2) + "\n")
    print("PASS: lifecycle instruction checks and LP arithmetic examples")


if __name__ == "__main__":
    main()
