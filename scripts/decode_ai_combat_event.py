#!/usr/bin/env python3
"""Decode the Joey AI's internal four-byte hypothetical combat event.

This is not a YDR replay decoder. Supply the bytes captured from the native
combat evaluator (FUN_005B0D90 / FUN_005B0F40), little-endian.
Pure standard-library Python. Does not interact with the game.

  python scripts/decode_ai_combat_event.py A8096400 200406FF
  python scripts/decode_ai_combat_event.py --json 08080000
"""
from __future__ import annotations

import argparse
from dataclasses import asdict, dataclass
import json
import re
import struct

KNOWN_FLAGS = 0x0002 | 0x0004 | 0x0008 | 0x0070 | 0x0380 | 0x0400 | 0x0800


@dataclass(frozen=True)
class CombatEvent:
    raw_hex: str
    low_word: int
    score_raw_u16: int
    score_signed_i16: int
    attacker_slot: int
    target_slot: int | None
    special_target_path: bool
    selected_by_outer_loop: bool
    useful_flag: bool
    attacker_removed_in_simulation: bool
    defender_removed_in_simulation: bool
    unknown_flags_hex: str

    @property
    def base_useful_condition(self) -> bool | None:
        """Rule for FUN_005B0F40; special route uses another constructor."""
        if self.special_target_path:
            return None
        return self.score_signed_i16 > 0 or (
            self.defender_removed_in_simulation
            and not self.attacker_removed_in_simulation
        )

    def to_dict(self) -> dict:
        return {**asdict(self), "base_useful_condition": self.base_useful_condition}


def decode_event(raw: bytes) -> CombatEvent:
    if len(raw) != 4:
        raise ValueError(f"Expected exactly 4 bytes; got {len(raw)}")
    low_word, score_raw = struct.unpack("<HH", raw)
    score = struct.unpack("<h", raw[2:4])[0]
    special = bool(low_word & 0x0002)
    return CombatEvent(
        raw_hex=raw.hex().upper(), low_word=low_word,
        score_raw_u16=score_raw, score_signed_i16=score,
        attacker_slot=(low_word >> 4) & 7,
        target_slot=None if special else (low_word >> 7) & 7,
        special_target_path=special,
        selected_by_outer_loop=bool(low_word & 0x0004),
        useful_flag=bool(low_word & 0x0008),
        attacker_removed_in_simulation=bool(low_word & 0x0400),
        defender_removed_in_simulation=bool(low_word & 0x0800),
        unknown_flags_hex=f"0x{low_word & ~KNOWN_FLAGS & 0xFFFF:04X}",
    )


def decode_hex(text: str) -> CombatEvent:
    compact = re.sub(r"[\s:_-]", "", text)
    if compact.lower().startswith("0x"):
        compact = compact[2:]
    if not re.fullmatch(r"[0-9a-fA-F]{8}", compact):
        raise ValueError(f"Expected four hex bytes, e.g. A8096400: {text!r}")
    return decode_event(bytes.fromhex(compact))


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("events", nargs="+", help="Four little-endian hex bytes")
    parser.add_argument("--json", action="store_true", help="Output structured JSON")
    args = parser.parse_args(argv)
    try:
        events = [decode_hex(value) for value in args.events]
    except ValueError as exc:
        parser.error(str(exc))
    if args.json:
        print(json.dumps([e.to_dict() for e in events], indent=2))
    else:
        for e in events:
            target = "special/unspecified" if e.special_target_path else str(e.target_slot)
            print(f"{e.raw_hex}: attacker={e.attacker_slot} target={target} "
                  f"score={e.score_signed_i16:+d} useful={e.useful_flag} "
                  f"remove_attacker={e.attacker_removed_in_simulation} "
                  f"remove_defender={e.defender_removed_in_simulation} "
                  f"selected={e.selected_by_outer_loop} unknown={e.unknown_flags_hex}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
