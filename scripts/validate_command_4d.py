"""Validate the observed command 0x4D path statically against the known EXE.

Run: python3 YugiRE/scripts/validate_command_4d.py
Writes metadata only; never executes or alters game files or Ghidra databases.
"""

import hashlib
import json
from pathlib import Path
import struct


def main():
    repository = Path(__file__).resolve().parent.parent
    workspace = repository.parent
    evidence = repository / "docs" / "evidence"
    binary = (workspace / "passion" / "joey_pc.exe").read_bytes()
    expected_hash = "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de"
    if hashlib.sha256(binary).hexdigest() != expected_hash:
        raise ValueError("Unexpected target hash")
    pe = struct.unpack_from("<I", binary, 0x3C)[0]
    count = struct.unpack_from("<H", binary, pe + 6)[0]
    optional_size = struct.unpack_from("<H", binary, pe + 20)[0]
    image_base = struct.unpack_from("<I", binary, pe + 24 + 28)[0]
    sections = pe + 24 + optional_size

    def offset(va):
        rva = va - image_base
        for index in range(count):
            header = sections + index * 40
            _, section_rva, size, start = struct.unpack_from("<IIII", binary, header + 8)
            if section_rva <= rva < section_rva + size:
                return start + rva - section_rva
        raise ValueError(f"VA outside file-backed sections: {va:#x}")

    checks = []
    for va, expected in [
        (0x005B9249, "66813da858a5000001"),
        (0x005B9491, "8a88ec9a5b00"),
        (0x005B9497, "ff248d38985b00"),
        (0x00571C25, "e89695ffff"),
        (0x0056B202, "668988805da500"),
        (0x0056B209, "668990c05da500"),
    ]:
        actual = binary[offset(va):offset(va) + len(bytes.fromhex(expected))].hex()
        if actual != expected:
            raise ValueError(f"Instruction mismatch at {va:#x}: {actual}")
        checks.append(dict(va=hex(va), file_offset=hex(offset(va)), bytes=actual))

    normalized = binary[offset(0x005B9AEC) + 0x4D - 1]
    stub = struct.unpack_from("<I", binary, offset(0x005B9838) + normalized * 4)[0]
    if binary[offset(stub)] != 0xE9:
        raise ValueError("Expected relative jump")
    handler = stub + 5 + struct.unpack_from("<i", binary, offset(stub) + 1)[0]
    if (normalized, stub, handler) != (0x2C, 0x005B9586, 0x00571BF0):
        raise ValueError("Dispatch path mismatch")

    originals = []
    for item in json.loads((evidence / "initial-inspection.json").read_text())["files"]:
        path = workspace / "passion" / item["name"]
        with path.open("rb") as stream:
            digest = hashlib.file_digest(stream, "sha256").hexdigest()
        if digest.upper() != item["sha256"] or path.stat().st_size != item["size"]:
            raise ValueError(f"Original changed: {path.name}")
        originals.append(dict(name=path.name, sha256=digest, size=path.stat().st_size))
    database = repository / "ghidra-projects/Joey.rep/idata/00/~00000000.db/db.1.gbf"
    with database.open("rb") as stream:
        database_hash = hashlib.file_digest(stream, "sha256").hexdigest()
    previous = json.loads((evidence / "parasite-static-validation.json").read_text())
    if database_hash != previous["database"]["sha256"]:
        raise ValueError("Program database differs from previous round")

    report = dict(
        target_sha256=expected_hash,
        dispatch=dict(opcode="0x4d", normalized=hex(normalized), stub=hex(stub), handler=hex(handler)),
        instruction_checks=checks,
        slot_aliases=dict(
            category=11,
            index_base=hex(0x00A55D80 + 11 * 0x90),
            metadata_base=hex(0x00A55DC0 + 11 * 0x90),
            block_stride="0xd44", slot_stride=2,
        ),
        originals=originals, database_sha256=database_hash,
        method="Static PE reads and address arithmetic; no target execution.",
    )
    (evidence / "command-4d-validation.json").write_text(json.dumps(report, indent=2) + "\n")
    print("PASS: dispatch, six instruction sequences, five originals and program database")


if __name__ == "__main__":
    main()
