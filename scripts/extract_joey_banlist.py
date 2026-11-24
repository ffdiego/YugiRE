#!/usr/bin/env python3
"""Read the candidate Joey-native Forbidden/Limited table without modifying the EXE.

The table address and layout come from the independent PoCModLoader HookManager.cpp.
This script insists on the known Joey executable SHA256 by default; a mismatch
must never be presented as confirmed native data.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path

JOEY_SHA256 = "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de"
TABLE_VA = 0x005EFBCC
TABLE_COUNT = 38
LIMIT_CALL_VA = 0x005BE206


def _u16(data: bytes, offset: int) -> int:
    if offset < 0 or offset + 2 > len(data):
        raise ValueError("Truncated PE field")
    return struct.unpack_from("<H", data, offset)[0]


def _u32(data: bytes, offset: int) -> int:
    if offset < 0 or offset + 4 > len(data):
        raise ValueError("Truncated PE field")
    return struct.unpack_from("<I", data, offset)[0]


def va_to_file_offset(data: bytes, va: int, size: int) -> int:
    """Map image VA using the PE32 section table, not VA - image base."""
    if len(data) < 0x40 or data[:2] != b"MZ":
        raise ValueError("Not a PE DOS executable")
    pe = _u32(data, 0x3C)
    if pe + 24 > len(data) or data[pe:pe + 4] != (b"PE" + bytes(2)):
        raise ValueError("Invalid PE header")
    section_count = _u16(data, pe + 6)
    optional_length = _u16(data, pe + 20)
    optional_offset = pe + 24
    if section_count not in range(1, 97) or optional_length < 32 or (
        optional_offset + optional_length > len(data)
    ):
        raise ValueError("Invalid PE section count or optional header")
    if _u16(data, optional_offset) != 0x10B:
        raise ValueError("Only PE32 executables are supported")
    image_base = _u32(data, optional_offset + 28)
    rva = va - image_base
    if rva < 0 or size < 0:
        raise ValueError("Requested VA is outside the image")
    section_table = optional_offset + optional_length
    if section_table + 40 * section_count > len(data):
        raise ValueError("Truncated PE section table")
    for i in range(section_count):
        offset = section_table + i * 40
        virtual_size = _u32(data, offset + 8)
        virtual_address = _u32(data, offset + 12)
        raw_size = _u32(data, offset + 16)
        raw_offset = _u32(data, offset + 20)
        delta = rva - virtual_address
        if 0 <= delta and delta + size <= min(virtual_size, raw_size):
            result = raw_offset + delta
            if result + size > len(data):
                raise ValueError("Mapped table exceeds EXE contents")
            return result
    raise ValueError("Table VA is not backed by a PE32 file section")


def extract_native_limits(data: bytes, table_va: int = TABLE_VA,
                          count: int = TABLE_COUNT) -> list[dict[str, int]]:
    if count < 1 or count > 4096:
        raise ValueError("Invalid candidate entry count")
    offset = va_to_file_offset(data, table_va, count * 4)
    entries = []
    ids: set[int] = set()
    for i in range(count):
        word = _u32(data, offset + i * 4)
        card_id = word & 0xFFF
        limit = (word >> 16) & 0xFFFF
        reserved = (word >> 12) & 0xF
        if limit > 3:
            raise ValueError(f"Unexpected copy limit {limit} at entry {i}")
        if card_id in ids:
            raise ValueError(f"Repeated resource ID {card_id} at entry {i}")
        ids.add(card_id)
        entries.append({
            "entry": i, "resourceId": card_id, "limit": limit,
            "reservedBits12to15": reserved, "raw": word,
        })
    return entries


def build_report(data: bytes, *, strict_hash: bool = True,
                 table_va: int = TABLE_VA, count: int = TABLE_COUNT) -> dict:
    actual = hashlib.sha256(data).hexdigest()
    matched = actual == JOEY_SHA256
    if strict_hash and not matched:
        raise ValueError("EXE SHA256 does not match the documented Joey build")
    entries = extract_native_limits(data, table_va=table_va, count=count)
    return {
        "schemaVersion": 1,
        "source": "candidate static EXE table (PoCModLoader HookManager.cpp)",
        "exeSha256": actual,
        "fingerprintMatched": matched,
        "tableVirtualAddress": f"0x{table_va:08X}",
        "tableEntryCount": count,
        "nativeLimitCallCandidateVA": f"0x{LIMIT_CALL_VA:08X}",
        "defaultLimitForOtherCards": 3,
        "restrictionOption": "not read by this static extraction",
        "entries": entries,
        "verification": "address/layout inferred from external mod source; verify in Ghidra",
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--exe", type=Path, required=True, help="Local joey_pc.exe")
    parser.add_argument("--output", type=Path, required=True,
                        help="JSON report path under scratch/, not the original EXE")
    parser.add_argument("--allow-unverified", action="store_true",
                        help="Exploratory ONLY: accept a different EXE SHA256")
    args = parser.parse_args(argv)
    try:
        contents = args.exe.read_bytes()
        report = build_report(contents, strict_hash=not args.allow_unverified)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    except (OSError, ValueError) as exc:
        parser.error(str(exc))
    print(f"Candidate limits: {len(report['entries'])} rows; "
          f"fingerprint matched: {report['fingerprintMatched']}; "
          f"report: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
