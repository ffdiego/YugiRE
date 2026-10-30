"""Check the static equipment link and Legendary Sword bonus branches."""

import contextlib
import hashlib
import io
import json
from pathlib import Path
import runpy
import struct

from catalog_card_callbacks import parse_callbacks, group_callbacks, TABLE_OFFSET, ROW_COUNT, ROW_SIZE
from validate_royal_command import main as validate_previous


def main():
    validate_previous()
    repository = Path(__file__).resolve().parent.parent
    binary = (repository.parent / "passion/joey_pc.exe").read_bytes()
    rows = parse_callbacks(binary[TABLE_OFFSET:TABLE_OFFSET + ROW_COUNT * ROW_SIZE],
                           [(0x00401000, 0x005DA000)])
    expected = (300, 0x00584500, 0x0057A470, 0, 0, 0x00593A20)
    if rows[24] != expected:
        raise ValueError("Legendary Sword registration mismatch")
    profiles, _ = group_callbacks(rows)
    members = profiles[expected[1:]]
    if len(members) != 41:
        raise ValueError("Equipment callback profile size mismatch")
    with contextlib.redirect_stdout(io.StringIO()):
        resources = runpy.run_path(str(repository / "scripts/inspect_parasite_resources.py"))
    with (repository.parent / "passion/data.dat").open("rb") as stream:
        stream.seek(1273896)
        ids = stream.read(2230)
    if hashlib.sha256(ids).hexdigest() != "a36cde2192fa85d9ccbcdb0ddb8ba8494a5199ea31d11a50a70b5fd32bf5879c":
        raise ValueError("CARD_ID fingerprint mismatch")
    index = 614
    name = resources["names"][index * 64:(index + 1) * 64].split(b"\0")[0].decode("ascii")
    if (name, struct.unpack_from("<H", ids, index * 2)[0]) != ("Legendary Sword", 300):
        raise ValueError("Equipment identity mismatch")
    start = struct.unpack_from("<I", resources["offsets"], index * 4)[0]
    description = resources["desc"][start:].split(b"\0")[0].decode("ascii")
    if description != "A Warrior-Type monster equipped with this card increases its ATK and DEF by 300 points.":
        raise ValueError("Local equipment text mismatch")
    normalized = binary[0x0017A18C + 300 - 0x47]
    candidate_branch = struct.unpack_from("<I", binary, 0x0017A15C + normalized * 4)[0]
    bonus_branch = struct.unpack_from("<I", binary, 0x00170080)[0]
    if (candidate_branch, bonus_branch) != (0x0057A146, 0x0056FD7F):
        raise ValueError("ID-300 type/bonus dispatch mismatch")
    checks = []
    for va, expected_bytes in [
        (0x0057A148, "66837c24200f"),
        (0x0056FD83, "8079020f"),
        (0x0056FF13, "be2c0100008bfe"),
        (0x00570066, "8b4424188b1003d689108b44241c8b0803cf5f5e8908"),
        (0x005845B3, "e8d803ffff"),
        (0x0056EF80, "e8bb0d0000"),
        (0x0056ABBE, "668988805da500668b4c240c668988c05da500"),
    ]:
        offset = va - 0x00400000
        actual = binary[offset:offset + len(bytes.fromhex(expected_bytes))].hex()
        if actual != expected_bytes:
            raise ValueError(f"Instruction mismatch at {va:#x}")
        checks.append(dict(va=hex(va), file_offset=hex(offset), bytes=actual))
    normalized_command = binary[0x001B9AEC + 0xB2 - 1]
    stub = struct.unpack_from("<I", binary, 0x001B9838 + normalized_command * 4)[0]
    offset = stub - 0x00400000
    if binary[offset] != 0xE9:
        raise ValueError("Expected command dispatch jump")
    handler = stub + 5 + struct.unpack_from("<i", binary, offset + 1)[0]
    if (normalized_command, stub, handler) != (115, 0x005B970D, 0x0040A230):
        raise ValueError("Equipment link command consumer mismatch")
    report = dict(target_sha256=hashlib.sha256(binary).hexdigest(),
                  card=dict(index=index, resource_id=300, name=name, description=description,
                            registration_index=24, profile="P025", profile_member_count=len(members)),
                  candidate_branch=hex(candidate_branch), bonus_branch=hex(bonus_branch),
                  dynamic_type=15, atk_bonus=300, def_bonus=300, association_kind=1,
                  link_command=dict(opcode="0xb2", normalized=normalized_command, stub=hex(stub), handler=hex(handler)),
                  instruction_checks=checks,
                  method="Static original/resource/registration/dispatch/instruction checks; no native execution.",
                  limits="One equipment's attach and recalculation path; complete unlink, battle use and cross-card interactions pending.")
    (repository / "docs/evidence/legendary-sword-validation.json").write_text(json.dumps(report, indent=2) + "\n")
    print("PASS: Legendary Sword identity, profile, equipment link and Warrior +300/+300 branches")


if __name__ == "__main__":
    main()
