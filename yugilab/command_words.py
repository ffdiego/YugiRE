"""Evidence-limited classification of changing 16-bit high command words.

Source: Joey Ghidra command-highword-{placement,card-commands,other}.txt,
command-highword-remaining-{card-id,raw-callers}.txt, and
card-id-instructions.txt, command-highword-final-{37,86}.txt,
and two seeded Windows traces.

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
    # Confirmed by follow-up 2026-11-08 Ghidra report:
    # 0057839C CALL 004022E0; 005783AC PUSH EAX; 005783B7 CALL 005B9170.
    ("0x0072", "0x5783bc", 1),
    # 005ACF57 CALL 004022E0; 005ACF6B PUSH EAX;
    # 005ACF6D CALL 005B9170.
    ("0x00e6", "0x5acf72", 1),
})

# 005B9FE6 MOV DX,[00A577FA]; 005B9FEF SHL DX,15; 005B9FF5 OR EDX,0x61;
# 005B9FFA PUSH EDX. Writes to DX never clear EDX[31:16].
_PARTIAL_REGISTER_RETURNS = frozenset({
    ("0x0061", "0x5ba000", 0),
    # The 0058CF50..0058CF96 code was previously unknown to Ghidra.
    # 0058CF7B CALL 005B9170 (0x35); 0058CF80 MOV AX,[ESI+2];
    # 0058CF86 SHL AX,15; 0058CF8C OR AL,0x37;
    # 0058CF90 PUSH EAX. No instruction clears EAX[31:16].
    ("0x0037", "0x58cf96", 0),
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
    return "unresolved_high16"


# FUN_005B9170 appends four 16-bit words (local_8/local_6/local_4/local_2)
# to the command queue. These three opcodes take the default switch branch,
# with NO transformation of the input parameters before writing the words.
# Other opcodes may remap card IDs, so do not apply this direct-packet
# reconstruction to every producer call without studying its switch cases.
_DIRECT_QUEUED_OPCODES = frozenset({0x0037, 0x0061, 0x0086})


def direct_queued_words(args: list[int]) -> tuple[int, int, int, int] | None:
    """Exact 4 x ushort packet for the verified default-branch opcodes.

    Return None for opcodes that require other native queue transformations.
    """
    if (not isinstance(args, list) or len(args) != 4 or
        any(type(v) is not int or not 0 <= v <= 0xFFFFFFFF for v in args)):
        raise ValueError("Expected four unsigned 32-bit producer arguments")
    if (args[0] & 0x7FFF) not in _DIRECT_QUEUED_OPCODES:
        return None
    return tuple(value & 0xFFFF for value in args)
