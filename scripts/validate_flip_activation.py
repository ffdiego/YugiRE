"""Check native FLIP classification tables and the face-change request path."""

import hashlib
import json
from pathlib import Path
import struct

from validate_tribute_cost import main as validate_previous


def main():
    validate_previous()
    repository = Path(__file__).resolve().parent.parent
    binary = (repository.parent / "passion/joey_pc.exe").read_bytes()
    classifications = []
    for resource_id, lower, byte_table, jump_table, expected in [
        (500, 0x1E9, 0x005677B0, 0x005677A8, True),
        (501, 0x1E9, 0x005677B0, 0x005677A8, False),
        (762, 0x280, 0x00567830, 0x00567824, True),
        (761, 0x280, 0x00567830, 0x00567824, False),
    ]:
        normalized = binary[byte_table - 0x00400000 + resource_id - lower]
        branch = struct.unpack_from("<I", binary, jump_table - 0x00400000 + normalized * 4)[0]
        if branch != (0x00567716 if expected else 0x00567691):
            raise ValueError("FLIP classification branch mismatch")
        classifications.append(dict(resource_id=resource_id, byte_table=hex(byte_table),
                                    jump_table=hex(jump_table), normalized=normalized,
                                    branch=hex(branch), classified=expected))
    checks = []
    for va, expected in [
        (0x00567694, "0517feffff83f87077f333d28a90b0775600ff2495a8775600"),
        (0x005676C4, "0580fdffff83f87a77c333c98a8830785600ff248d24785600"),
        (0x00567716, "b801000000c3"),
        (0x00567691, "33c0c3"),
        (0x00575078, "e8a325ffff"),
        (0x00575092, "e8e99d0000"),
        (0x005750E4, "e887400400"),
        (0x0057511F, "e86c530400"),
        (0x00407F02, "668b4f068bc1f7d032c183e00233c1a80266894706"),
        (0x005BA4B3, "e858feffff"),
    ]:
        offset = va - 0x00400000
        actual = binary[offset:offset + len(bytes.fromhex(expected))].hex()
        if actual != expected:
            raise ValueError(f"Instruction mismatch at {va:#x}")
        checks.append(dict(va=hex(va), file_offset=hex(offset), bytes=actual))
    normalized = binary[0x001B9AEC + 0x80 - 1]
    stub = struct.unpack_from("<I", binary, 0x001B9838 + normalized * 4)[0]
    offset = stub - 0x00400000
    if binary[offset] != 0xE9:
        raise ValueError("Expected command dispatch jump")
    handler = stub + 5 + struct.unpack_from("<i", binary, offset + 1)[0]
    if (normalized, stub, handler) != (86, 0x005B9668, 0x00407E60):
        raise ValueError("Face-change consumer mismatch")
    identities = []
    previous = json.loads((repository / "docs/evidence/monster-target-validation.json").read_text())
    card = next(card for card in previous["cards"] if card["resource_id"] == 500)
    if card["index"] != 753 or not card["description"].startswith("FLIP:"):
        raise ValueError("Man-Eater Bug resource evidence mismatch")
    identities.append(dict(name=card["name"], index=card["index"], resource_id=500,
                           description=card["description"]))
    report = dict(target_sha256=hashlib.sha256(binary).hexdigest(),
                  classifications=classifications, instruction_checks=checks, identities=identities,
                  face_command=dict(opcode="0x80", normalized=normalized, stub=hex(stub), handler=hex(handler)),
                  method="Native table values, instruction bytes and local FLIP description; no target execution.",
                  limits="Classification and one conditional face-change producer; battle timing, UI reachability and all FLIP interactions pending.")
    (repository / "docs/evidence/flip-activation-validation.json").write_text(json.dumps(report, indent=2) + "\n")
    print("PASS: native FLIP classification, command 0x80 and conditional request producer")


if __name__ == "__main__":
    main()
