"""Check shared discard-cost callbacks and their static hand eligibility model."""

import contextlib
import hashlib
import io
import json
from pathlib import Path
import runpy
import struct

from validate_monster_destruction import main as validate_destruction


def eligible_count(hand, excluded_index=0, forbid_spells=False):
    count = 0
    for index, class_code, has_parasite_association in hand:
        eligible = index != excluded_index
        if not eligible:
            excluded_index = 0
        if (eligible and index != 0 and not has_parasite_association
                and not (forbid_spells and class_code == 0x16)):
            count += 1
    return count


def main():
    validate_destruction()
    repository = Path(__file__).resolve().parent.parent
    binary = (repository.parent / "passion/joey_pc.exe").read_bytes()
    with contextlib.redirect_stdout(io.StringIO()):
        resources = runpy.run_path(str(repository / "scripts/inspect_parasite_resources.py"))
    with (repository.parent / "passion/data.dat").open("rb") as stream:
        stream.seek(1273896)
        ids = stream.read(2230)
    if hashlib.sha256(ids).hexdigest() != "a36cde2192fa85d9ccbcdb0ddb8ba8494a5199ea31d11a50a70b5fd32bf5879c":
        raise ValueError("CARD_ID fingerprint mismatch")
    cards = []
    for index, resource_id, name, row_index, amount in [
        (363, 0x3FF, "Tribute to The Doomed", 187, 1),
        (893, 0x405, "Magic Jammer", 193, 1),
        (319, 0x42B, "Final Destiny", 224, 5),
        (1015, 0x430, "Darkness Approaches", 229, 2),
        (748, 0x4B1, "Big Shield Gardna", None, None),
        (913, 0x49C, "Anti-Spell Fragrance", None, None),
    ]:
        actual_name = resources["names"][index * 64:(index + 1) * 64].split(b"\0")[0].decode("ascii")
        if (actual_name, struct.unpack_from("<H", ids, index * 2)[0]) != (name, resource_id):
            raise ValueError("Unexpected card identity")
        if row_index is not None:
            row = struct.unpack_from("<6I", binary, 0x001ED0A8 + row_index * 24)
            if (row[0], row[4]) != (resource_id, 0x0057D7C0):
                raise ValueError("Shared cost callback mismatch")
            normalized = binary[0x0017D830 + resource_id - 0x3FF]
            branch = struct.unpack_from("<I", binary, 0x0017D820 + normalized * 4)[0]
            branch_offset = branch - 0x00400000
            if binary[branch_offset] != 0xB8 or struct.unpack_from("<I", binary, branch_offset + 1)[0] != amount:
                raise ValueError("Discard amount branch mismatch")
        offset = struct.unpack_from("<I", resources["offsets"], index * 4)[0]
        description = resources["desc"][offset:].split(b"\0")[0].decode("ascii")
        cards.append(dict(index=index, resource_id=resource_id, name=name,
                          registration_index=row_index, discard_amount=amount, description=description))
    checks = []
    for va, expected in [
        (0x00580D35, "6a006a00508a410283e0016a0150e8e8e3ffff"),
        (0x0057D80E, "e8edf50300"),
        (0x005BB218, "e8132cfeff"),
        (0x005BCA6F, "e87cc7feff"),
        (0x005BC672, "e83903000085c07574"),
        (0x005BC68D, "e8aef7ffff"),
        (0x00575A2F, "e86cffffff"),
        (0x005835D8, "804b040e"),
    ]:
        offset = va - 0x00400000
        actual = binary[offset:offset + len(bytes.fromhex(expected))].hex()
        if actual != expected:
            raise ValueError(f"Instruction mismatch at {va:#x}")
        checks.append(dict(va=hex(va), file_offset=hex(offset), bytes=actual))
    normalized = binary[0x001B9AEC + 0x8E - 1]
    stub = struct.unpack_from("<I", binary, 0x001B9838 + normalized * 4)[0]
    offset = stub - 0x00400000
    if binary[offset] != 0xE9:
        raise ValueError("Expected dispatch jump")
    handler = stub + 5 + struct.unpack_from("<i", binary, offset + 1)[0]
    if (normalized, stub, handler) != (98, 0x005B96B8, 0x00408EE0):
        raise ValueError("Discard command consumer mismatch")
    if struct.unpack_from("<I", binary, 0x001BCC34 + 4)[0] != 0x005BCA42:
        raise ValueError("Selection kind 2 dispatch mismatch")
    model_cases = []
    for name, hand, excluded, forbid_spells, expected in [
        ("empty", [], 363, False, 0),
        ("source_only", [(363, 22, False)], 363, False, 0),
        ("source_and_monster", [(363, 22, False), (753, 10, False)], 363, False, 1),
        ("duplicate_source_excluded_once", [(363, 22, False)] * 2, 363, False, 1),
        ("parasite_excluded", [(363, 22, False), (197, 10, True)], 363, False, 0),
        ("restricted_spell", [(363, 22, False), (557, 22, False)], 363, True, 0),
        ("restricted_monster", [(363, 22, False), (753, 10, False)], 363, True, 1),
        ("source_not_excluded", [(363, 22, False)], 0, False, 1),
    ]:
        actual = eligible_count(hand, excluded, forbid_spells)
        if actual != expected:
            raise ValueError(f"Hand model mismatch: {name}")
        model_cases.append(dict(name=name, eligible=actual, requested_one_allowed=actual >= 1))
    report = dict(target_sha256=hashlib.sha256(binary).hexdigest(), cards=cards,
                  instruction_checks=checks, model_cases=model_cases,
                  discard_command=dict(opcode="0x8e", normalized=normalized, stub=hex(stub), handler=hex(handler)),
                  method="Static resource/registration/instruction checks and hand eligibility model; no native execution.",
                  limits="Selection UI, synchronization, negation/refunds and full interactions not dynamically tested.")
    (repository / "docs/evidence/tribute-cost-validation.json").write_text(json.dumps(report, indent=2) + "\n")
    print("PASS: shared discard callbacks, costs 1/2/5, command 0x8e and hand eligibility cases")


if __name__ == "__main__":
    main()
