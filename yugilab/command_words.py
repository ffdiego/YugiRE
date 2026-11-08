"""Evidence-limited classification of changing 16-bit high command words.

Source: Joey Ghidra command-highword-{placement,card-commands,other}.txt
and card-id-instructions.txt, matched to two seeded Windows traces.

This classifies the *origin* of certain high-word differences, not whether
the original command consumer ignores those 16 bits. Preserve full raw words.
"""
from __future__ import annotations


# The native function FUN_004022E0 calls a loader that returns a pointer to
# CARD_ID, then MOV AX,[EAX+ESI*2]. This updates only AX, preserving the
# high 16 bits of that loaded pointer in EAX. These verified callsites PUSH
# full EAX into the common command producer FUN_005B9170 (args[1]).
_CARD_ID_POINTER_RETURNS = frozenset({
    ("0x00ce", "0x5abeab", 1),
    ("0x00e6", "0x5abd70", 1),
    ("0x006f", "0x5abf22", 1),
    ("0x00cf", "0x57977f", 1),
    ("0x0072", "0x5783f2", 1),
    ("0x00df", "0x595142", 1),
    ("0x0075", "0x578459", 1),
})

# These exhibit the same high word as the CARD_ID pointer in the very same
# process, but the supplied Ghidra report did not cover these specific
# return addresses. Keep them separate until their callees are inspected.
_CANDIDATE_POINTER_RETURNS = frozenset({
    ("0x0072", "0x5783bc", 1),
    ("0x00e6", "0x5acf72", 1),
})

# 005B9FE6 MOV DX,[00A577FA]; 005B9FEF SHL DX,15; 005B9FF5 OR EDX,0x61;
# 005B9FFA PUSH EDX. Writes to DX never clear EDX[31:16].
_PARTIAL_REGISTER_RETURNS = frozenset({
    ("0x0061", "0x5ba000", 0),
})


def classify_high_word_change(opcode: str, caller: str, arg_index: int,
                              left_value: int, right_value: int) -> str:
    """Classify one changed raw 32-bit command argument conservatively."""
    if (type(left_value) is not int or type(right_value) is not int or
        left_value < 0 or left_value > 0xFFFFFFFF or
        right_value < 0 or right_value > 0xFFFFFFFF):
        raise ValueError("Expected unsigned 32-bit command words")
    if left_value == right_value:
        return "identical"
    if (left_value & 0xFFFF) != (right_value & 0xFFFF):
        return "low16_changed"
    key = (str(opcode).lower(), str(caller).lower(), arg_index)
    if key in _CARD_ID_POINTER_RETURNS:
        return "verified_card_id_pointer_residue"
    if key in _PARTIAL_REGISTER_RETURNS:
        return "verified_partial_register_write"
    if key in _CANDIDATE_POINTER_RETURNS:
        return "candidate_card_id_pointer_residue"
    return "unresolved_high16"
