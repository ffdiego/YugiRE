#!/usr/bin/env python3
"""Pure, partial helpers from Joey's AI attribute/effect evaluation.

Covers only verified branches of FUN_0056FD40, FUN_005674E0 and the
nonnegative halving loop of FUN_0056DD30. This is NOT the duel engine:
link eligibility, Prohibition, Mirror Wall counting and all other global
conditions must be resolved by the native game or a separate evaluator.
"""
from __future__ import annotations

RESOURCE_ALIASES = (
    frozenset((0x3EB, 0x40A)),  # Polymerization variants
    frozenset((0x3D, 0x4E1)),
    frozenset((0x22, 0x4BA)),
    frozenset((0x39, 0x76D)),  # Kuriboh variants
    frozenset((0x4DD, 0x76E)),
)

# FUN_0056FD40: base additions, before other field and native modifiers.
_FIXED_DELTAS = {
    0x28A: (500, 0),   # Kunai with Chain
    0x291: (300, 300), # Metalmorph
    0x12F: (1000, 0),  # Axe of Despair
    0x136: (500, 0),
    0x138: (0, 800),   # Horn of Light
    0x139: (700, 700), # Horn of the Unicorn
    0x140: (700, 0),   # Malevolent Nuzzler
    0x412: (500, 500), # Sword of Deep-Seated
    0x422: (-500, -500),  # Ring of Magnetism
    0x4DA: (-500, 0), # Beast of Gilfer
    0x58C: (1000, -1000), # Mask of Brutality
}
_TYPE_300_BOTH = {
    300: 15, 0x12E: 3, 0x130: 10, 0x133: 11, 0x135: 13,
    0x137: 17, 0x13A: 1, 0x13B: 19, 0x13E: 12,
    0x141: 2, 0x142: 18, 0x144: 7, 0x145: 9,
    0x146: 16, 0x147: 14,
}
_ATTRIBUTE_400_MINUS_200 = {
    0x12D: 0x200, 0x132: 0x100, 0x134: 0x300,
    0x143: 0x500, 0x3F4: 0x400, 0x3F5: 0x600,
}


def normalized_resource_id(resource_id: int) -> int:
    """FUN_005674E0 normalizes IDs > 1999 by subtracting 2000 once."""
    if not 0 <= resource_id <= 0xFFFF:
        raise ValueError("Resource ID must be an unsigned 16-bit value")
    return resource_id - 2000 if resource_id > 1999 else resource_id


def equivalent_resource_ids(a: int, b: int) -> bool:
    """FUN_005674E0's narrowly defined alias matching, given resource IDs.

    The native FUN_005674E0 receives *internal card indices* and first calls
    FUN_004022E0. Pass the mapped resource IDs here, not internal indices.
    """
    left, right = normalized_resource_id(a), normalized_resource_id(b)
    return left == right or any(left in group and right in group
                                for group in RESOURCE_ALIASES)


def equipment_deltas(resource_id: int, *, type_code: int | None = None,
                     attribute_code: int | None = None,
                     monster_resource_id: int | None = None) -> tuple[int, int] | None:
    """A subset of exact (ATK, DEF) contributions in FUN_0056FD40.

    None = resource branch not modeled, NOT necessarily zero.
    Branch conditions use *already computed* type/attribute codes. This does
    not check active equipment links or suppression; it only computes deltas.
    """
    if resource_id in _FIXED_DELTAS:
        return _FIXED_DELTAS[resource_id]
    if resource_id in _TYPE_300_BOTH:
        return (300, 300) if type_code == _TYPE_300_BOTH[resource_id] else (0, 0)
    if resource_id in _ATTRIBUTE_400_MINUS_200:
        return (400, -200) if attribute_code == _ATTRIBUTE_400_MINUS_200[resource_id] else (0, 0)
    if resource_id == 0x131:
        return (700, 0) if type_code == 10 else (0, 0)
    if resource_id in (0x28D, 0x29B):
        match = 0x400 if resource_id == 0x28D else 0x100
        return (700, 0) if attribute_code == match else (0, 0)
    if resource_id == 0x13C:
        return (500, 0) if monster_resource_id in (0x3D, 0x3E, 0x4E1) else (0, 0)
    if resource_id == 0x49E:
        return (700, 0) if type_code == 15 else (0, 0)
    if resource_id == 0x58E:
        return (800, 0) if type_code == 15 else (0, 0)
    if resource_id == 0x604:
        return (300, 0) if monster_resource_id == 0x53B else (0, 0)
    return None


def halve_attack_repeatedly(attack: int, count: int) -> int:
    """FUN_0056DD30's Mirror Wall loop for nonnegative, no-overflow values."""
    if attack < 0 or count < 0:
        raise ValueError("This offline approximation requires nonnegative inputs")
    for _ in range(count):
        attack = (attack * 5 + 5) // 10
    return attack
