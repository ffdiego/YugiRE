"""Validate the known insertion path; run from any working directory."""

import hashlib
import json
from pathlib import Path
import struct

from validate_command_4d import main as validate_previous_path


def main():
    # Recheck original hashes, saved database and the section mapping first.
    validate_previous_path()
    repository = Path(__file__).resolve().parent.parent
    binary = (repository.parent / "passion/joey_pc.exe").read_bytes()
    # These VAs are in the documented .text section, whose raw offset = RVA.
    checks = []
    for va, expected in [
        (0x0056ABBE, "668988805da500"),
        (0x0056ABCA, "668988c05da500"),
        (0x0056ABD1, "66ff02"),
        (0x00571BD7, "e8848fffff"),
        (0x005797A2, "80cd0b"),
        (0x005747FC, "0c86"),
    ]:
        offset = va - 0x00400000
        actual = binary[offset:offset + len(bytes.fromhex(expected))].hex()
        if actual != expected:
            raise ValueError(f"Instruction mismatch at {va:#x}")
        checks.append(dict(va=hex(va), file_offset=hex(offset), bytes=actual))
    index = binary[0x001B9AEC + 0x86 - 1]
    stub = struct.unpack_from("<I", binary, 0x001B9838 + index * 4)[0]
    if binary[stub - 0x00400000] != 0xE9:
        raise ValueError("Expected relative jump")
    target = stub + 5 + struct.unpack_from("<i", binary, stub - 0x00400000 + 1)[0]
    if (index, stub, target) != (0x5C, 0x005B969A, 0x00571BC0):
        raise ValueError("Unexpected command 0x86 dispatch")
    report = dict(
        target_sha256=hashlib.sha256(binary).hexdigest(),
        dispatch=dict(opcode="0x86", normalized=hex(index), stub=hex(stub), handler=hex(target)),
        instruction_checks=checks,
        category_11_bases=dict(count=hex(0x00A55D7E + 11 * 0x90),
                              index=hex(0x00A55D80 + 11 * 0x90),
                              metadata=hex(0x00A55DC0 + 11 * 0x90)),
        limitation="No type-13 Parasite Paracide producer established in this round.",
    )
    path = repository / "docs/evidence/association-insertion-validation.json"
    path.write_text(json.dumps(report, indent=2) + "\n")
    print("PASS: command 0x86 dispatch, insertion writes and category-11 bases")


if __name__ == "__main__":
    main()
