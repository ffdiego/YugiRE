"""Reference math for YugiLab duel seeds and Joey's MSVC 32-bit rand.

Confirmed from original Ghidra functions:
- FUN_005C3BF5(seed) stores uint32 in CRT per-thread RNG data +0x14
- _rand @005C3C02: state = state*0x343FD + 0x269EC3 (mod 2**32)
  then returns (state >> 16) & 0x7FFF.

The YugiLab mix32-v1 derives non-overlapping per-duel seed values from a
master seed and globally unique duel index; it is NOT part of Joey itself.
"""
from __future__ import annotations

UINT32_MAX = 0xFFFFFFFF


def derive_duel_seed(base: int, global_duel_index: int) -> int:
    """Exact Python equivalent of Frida agent mixDuelSeed(base,index)."""
    for name, number in (("base", base), ("global_duel_index", global_duel_index)):
        if type(number) is not int or not 0 <= number <= UINT32_MAX:
            raise ValueError(f"{name} must be an unsigned 32-bit integer")
    value = (base + global_duel_index) & UINT32_MAX
    value ^= value >> 16
    value = (value * 0x7FEB352D) & UINT32_MAX
    value ^= value >> 15
    value = (value * 0x846CA68B) & UINT32_MAX
    value ^= value >> 16
    return value & UINT32_MAX


def msvc_rand_step(state: int) -> tuple[int, int]:
    """Return (new_state_uint32, rand_0_to_32767) from the original Joey CRT."""
    if type(state) is not int or not 0 <= state <= UINT32_MAX:
        raise ValueError("state must be an unsigned 32-bit integer")
    state = (state * 0x343FD + 0x269EC3) & UINT32_MAX
    return state, (state >> 16) & 0x7FFF
