"""Decode only CE/CF hand placement records with established argument layout.

A decoded placement is not a legal input: the native producer may already
have paid costs, triggered responses or scheduled further operations.
"""
from __future__ import annotations
from dataclasses import dataclass


@dataclass(frozen=True)
class Placement:
    side: int
    resource_id: int
    hand_index: int
    zone: str
    slot: int
    face_up: bool
    defense: bool
    variant: int

    def setup_command(self) -> str:
        if self.zone == "monster":
            if not self.face_up and not self.defense:
                raise ValueError("face-down attack placement is not supported")
            pose = "set" if not self.face_up else ("defense" if self.defense else "attack")
            return f"move-id {self.resource_id} {self.side} hand {self.hand_index} monster {self.slot} {pose}"
        if self.zone == "spell" and not self.face_up and self.variant == 0:
            return f"move-id {self.resource_id} {self.side} hand {self.hand_index} spell {self.slot} set"
        # Active S/T state involves other flags and callbacks; do not activate
        # the card a second time or pretend that Set is equivalent.
        raise ValueError("face-up, special or field S/T placement not projected")


def decode_placement(record: dict) -> Placement:
    raw = int(record["raw_opcode_hex"], 16)
    op, side = raw & 0xfff, raw >> 15
    resource, packed, flags = (record[f"argument_{i}"] for i in (1, 2, 3))
    if raw & 0x7000 or not 0 < resource <= 0xfff:
        raise ValueError("unsupported placement opcode flags or resource ID")
    index, native_slot = packed & 0xff, packed >> 8
    if index >= 80:
        raise ValueError("hand index outside supported bounds")
    if op == 0xce:
        if native_slot >= 5 or flags & ~0x101:
            raise ValueError("unsupported CE slot or placement flags")
        return Placement(side, resource, index, "monster", native_slot,
                         bool(flags & 1), bool(flags & 0x100), flags)
    if op == 0xcf:
        if native_slot not in range(5,11) or (flags & 0xff) not in (0,1):
            raise ValueError("unsupported CF slot or face flags")
        zone = "field" if native_slot == 10 else "spell"
        return Placement(side, resource, index, zone, 0 if zone=="field" else native_slot-5,
                         bool(flags & 1), False, flags >> 8)
    raise ValueError("not a CE/CF record")
