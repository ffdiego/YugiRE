"""Verify that a local Joey executable matches the known PE32 x86 build.

Uses only the Python standard library, never changes the executable.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import struct

EXPECTED_SHA256 = "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de"
DOCUMENTED_BASE = 0x00400000
FUNCTIONS = {
    "InvokePrimaryCardCallback": 0x0059DEB0,
    "QueueDuelCommand": 0x005B9170,
    "CalculateCardAttributes": 0x0056DD30,
}


def parse_pe(raw: bytes) -> dict:
    if len(raw) < 0x100 or raw[:2] != b"MZ":
        raise ValueError("not a DOS/PE executable")
    pe = struct.unpack_from("<I", raw, 0x3C)[0]
    if pe + 24 > len(raw) or raw[pe:pe + 4] != b"PE\x00\x00":
        raise ValueError("invalid PE header")
    machine, section_count, _, _, _, optional_size, _ = struct.unpack_from("<HHIIIHH", raw, pe + 4)
    opt = pe + 24
    if opt + optional_size > len(raw) or optional_size < 32:
        raise ValueError("invalid optional header")
    magic = struct.unpack_from("<H", raw, opt)[0]
    if magic != 0x10B:
        raise ValueError("requires PE32, not PE32+ or another format")
    image_base = struct.unpack_from("<I", raw, opt + 28)[0]
    sections = []
    sections_offset = opt + optional_size
    for i in range(section_count):
        off = sections_offset + i * 40
        if off + 40 > len(raw):
            raise ValueError("truncated section table")
        name = raw[off:off + 8].rstrip(b"\x00").decode("ascii", errors="replace")
        virtual_size, rva, raw_size, file_offset = struct.unpack_from("<IIII", raw, off + 8)
        sections.append({"name": name, "rva": rva, "virtual_size": virtual_size,
                         "raw_size": raw_size, "file_offset": file_offset})
    return {"machine": machine, "image_base": image_base, "sections": sections}


def probe(executable: Path) -> dict:
    raw = executable.read_bytes()
    info = parse_pe(raw)
    digest = hashlib.sha256(raw).hexdigest()
    checks = {
        "expected_sha256": digest == EXPECTED_SHA256,
        "x86_machine": info["machine"] == 0x014C,
        "image_base": info["image_base"] == DOCUMENTED_BASE,
    }
    found = {}
    for name, va in FUNCTIONS.items():
        rva = va - DOCUMENTED_BASE
        section = next((section for section in info["sections"]
                        if section["name"] == ".text"
                        and section["rva"] <= rva < section["rva"] + section["virtual_size"]), None)
        if section is None:
            checks[f"{name}_in_text"] = False
            continue
        byte_offset = section["file_offset"] + rva - section["rva"]
        good = byte_offset + 8 <= min(len(raw), section["file_offset"] + section["raw_size"])
        checks[f"{name}_in_text"] = good
        if good:
            found[name] = {"va": f"0x{va:08X}", "rva": f"0x{rva:08X}",
                           "bytes8_hex": raw[byte_offset:byte_offset + 8].hex().upper()}
    return {"schema": "joey-hybrid-preflight-v1", "exe_sha256": digest,
            "exe_size": len(raw), "machine_hex": f"0x{info['machine']:04X}",
            "image_base_hex": f"0x{info['image_base']:08X}",
            "checks": checks, "functions": found, "passed": all(checks.values())}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("exe", type=Path, help="path to the LOCAL original joey_pc.exe")
    args = parser.parse_args(argv)
    try:
        result = probe(args.exe)
    except (ValueError, OSError, struct.error) as exc:
        parser.exit(2, f"PRE-FLIGHT FAILED: {exc}\n")
    print(json.dumps(result, indent=2))
    return 0 if result["passed"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
