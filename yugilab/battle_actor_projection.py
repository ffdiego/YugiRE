"""Assembly-backed role/slot projection of native 0x31 battle observations."""
from __future__ import annotations

OBS_SCHEMA = "joey-command-entry-battle-attributes-v1"


def project_battle_actors(raw: dict, sides: list[dict] | None) -> dict:
    """Keep the raw sample and identify actors only when the field agrees.

    005B7C50: block index equal to selector bit 0 is the attacker.
    Direct attacks clear the other block's card index; it is not a monster.
    No cross-engine occurrence mapping or equivalence verdict is implied.
    """
    if (raw.get("schema") != OBS_SCHEMA or
            raw.get("samplePoint") != "command_producer_onEnter_0x31" or
            raw.get("nonAtomic") is not True):
        raise ValueError("Unexpected native battle observation schema or sample point")
    selector = raw["selectorWord"]
    if type(selector) is not int or not 0 <= selector <= 0xFFFFFFFF:
        raise ValueError("Invalid battle selector word")
    blocks = raw["blocks"]
    if not isinstance(blocks, list) or len(blocks) != 2 or any(
            block.get("blockIndex") != i for i, block in enumerate(blocks)):
        raise ValueError("Expected native battle blocks 0 and 1 in order")
    attacker = selector & 1
    target = (selector >> 1) & 1
    direct = bool(selector & 4)
    actors = []
    for role, index, side, slot in (
            ("attacker", attacker, attacker, (selector >> 8) & 7),
            ("target", 1 - attacker, target, (selector >> 11) & 7)):
        block = blocks[index]
        descriptor = block["descriptorWord"]
        card = block["cardIndexWord"]
        if (type(descriptor) is not int or not 0 <= descriptor <= 0xFFFF or
                type(card) is not int or not 0 <= card <= 4095):
            raise ValueError("Invalid native battle block identity")
        row = dict(role=role, blockIndex=index, side=side, slot=slot, rawBlock=block,
                   occurrenceWord=None, faceDown=None, defense=None)
        if role == "target" and direct:
            row.update(slot=None, status="direct_target_absent" if card == 0
                       else "direct_target_nonzero")
        elif slot >= 5:
            row.update(status="invalid_monster_slot")
        elif card == 0:
            row.update(status="empty_battle_block")
        elif (descriptor & 0xF) != (slot << 1 | side):
            row.update(status="descriptor_selector_disagree")
        elif sides is None:
            row.update(status="no_field_sample")
        else:
            field = sides[side]["fieldSlots"][slot]
            if field["cardIndex"] != card:
                row.update(status="field_index_disagree")
            elif bool(field["positionWord"] & 1) != bool(descriptor & 0x20):
                row.update(status="field_pose_disagree")
            else:
                row.update(status="field_correlated", occurrenceWord=field["occurrenceWord"],
                           faceDown=not bool(field["positionWord"] & 2),
                           defense=bool(field["positionWord"] & 1))
        actors.append(row)
    return dict(selectorWord=selector, direct=direct, actors=actors,
                interpretationLimit="Battle-time attributes; field correlation is non-atomic and not a C# occurrence mapping.")
