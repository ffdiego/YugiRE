#!/usr/bin/env python3
"""Decode the 12-byte combat stats buffer produced by Joey's FUN_0056DD30.

Read-only parser, no game hooks or calls. The input must be bytes captured from
memory by a separate observer. Values are in little-endian memory order.

    python scripts/decode_ai_combat_stats.py 1101060F40060000B0040000
    python scripts/decode_ai_combat_stats.py --json 00000000E8030000E8030000

The file contains calculated stats, not original card properties, and does
NOT reproduce the full native attribute calculation.
"""
from __future__ import annotations

import argparse
from dataclasses import asdict, dataclass
import json
import re
import struct


@dataclass(frozen=True)
class CombatStats:
    raw_hex: str
    internal_card_index: int
    type_code: int
    attribute_code: int
    attack: int
    defense: int

    def to_dict(self) -> dict:
        return asdict(self)


def decode_stats(raw: bytes) -> CombatStats:
    """Read exactly one 12-byte, little-endian FUN_0056DD30 output buffer."""
    if len(raw) != 12:
        raise ValueError(f"Expected exactly 12 bytes; got {len(raw)}")
    index, type_code, attribute, attack, defense = struct.unpack("<HBBii", raw)
    return CombatStats(raw.hex().upper(), index, type_code, attribute, attack, defense)


def decode_hex(text: str) -> CombatStats:
    compact = re.sub(r"[\s:_-]", "", text)
    if compact.lower().startswith("0x"):
        compact = compact[2:]
    if re.fullmatch(r"[0-9a-fA-F]{24}", compact) is None:
        raise ValueError(f"Expected 12 hex bytes: {text!r}")
    return decode_stats(bytes.fromhex(compact))


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("stats", nargs="+", help="12-byte little-endian context, in hex")
    parser.add_argument("--json", action="store_true", help="Output structured JSON")
    args = parser.parse_args(argv)
    try:
        values = [decode_hex(item) for item in args.stats]
    except ValueError as exc:
        parser.error(str(exc))
    if args.json:
        print(json.dumps([v.to_dict() for v in values], indent=2))
    else:
        for v in values:
            print(f"{v.raw_hex}: index={v.internal_card_index} "
                  f"type=0x{v.type_code:02X} attribute=0x{v.attribute_code:02X} "
                  f"ATK={v.attack} DEF={v.defense}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
