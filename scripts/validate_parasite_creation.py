"""Validate the observed Parasite Paracide callback and association producer."""

import hashlib
import json
from pathlib import Path
import struct

from validate_association_insertion import main as validate_insertion


def main():
    validate_insertion()
    repository = Path(__file__).resolve().parent.parent
    binary = (repository.parent / "passion/joey_pc.exe").read_bytes()
    # Addresses used here are in .text/.data, with raw offset equal to RVA
    # for this fingerprinted PE. The preceding validator enforces the hash.
    base = 0x00400000
    table = 0x005ED0A8

    def row(index):
        return struct.unpack_from("<6I", binary, table - base + index * 24)

    low, high, index = 0, 0x1BA, 0xDD
    visited = []
    for _ in range(32):
        card_id = row(index)[0] & 0xFFFF
        visited.append(dict(index=index, resource_id=card_id))
        if card_id == 762:
            break
        if low == high:
            raise ValueError("ID 762 not found")
        if card_id < 762:
            low = index
        if 762 < card_id:
            high = index
        if (high + low) // 2 == index:
            low = high
        index = (high + low) // 2
    else:
        raise ValueError("Search did not terminate")
    expected_row = (762, 0x005875A0, 0, 0x005800A0, 0x0057CCC0, 0)
    if index != 150 or row(index) != expected_row:
        raise ValueError("Unexpected Parasite Paracide registration")

    checks = []
    for va, expected in [
        (0x0059DBEF, "e8ec46e6ff"),
        (0x0059DC32, "668b04d5a8d05e00"),
        (0x0059DECC, "8b04cdacd05e00"),
        (0x0059DEDF, "ffd0"),
        (0x0058760B, "668b460433d2c1e8058af0b801000000"),
        (0x0058761B, "83ca0d2bc1525750e86821ffff"),
    ]:
        offset = va - base
        actual = binary[offset:offset + len(bytes.fromhex(expected))].hex()
        if actual != expected:
            raise ValueError(f"Instruction mismatch at {va:#x}: {actual}")
        checks.append(dict(va=hex(va), file_offset=hex(offset), bytes=actual))
    report = dict(
        target_sha256=hashlib.sha256(binary).hexdigest(),
        table_base=hex(table), row_stride=24, row_index=index,
        row_va=hex(table + index * 24), row_file_offset=hex(table - base + index * 24),
        row_hex=binary[table - base + index * 24:table - base + (index + 1) * 24].hex(),
        row_words=[hex(value) for value in row(index)], search_trace=visited,
        instruction_checks=checks,
        producer=dict(callback="0x5875a0", call="0x587623", wrapper="0x579790",
                      destination="1 - (request_word_2 & 1), category 11",
                      metadata="13 | (((request_word_4 >> 5) & 255) << 8)"),
        limitation="Branch-specific static proof; full request and effect lifecycle unresolved.",
    )
    path = repository / "docs/evidence/parasite-creation-validation.json"
    path.write_text(json.dumps(report, indent=2) + "\n")
    print("PASS: ID-762 registration, callback search and type-13 producer")


if __name__ == "__main__":
    main()
