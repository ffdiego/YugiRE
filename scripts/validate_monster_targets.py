"""Check card registrations and a model of the native target entry bitfields."""

import contextlib
import hashlib
import io
import json
from pathlib import Path
import runpy
import struct

from validate_parasite_lifecycle import main as validate_preservation


def occurrence_key(word):
    return ((word >> 12) & 1) + 2 * ((word >> 24) & 127)


def pack_target(side, position, word):
    return side | (position << 4) | (occurrence_key(word) << 8)


def resolve_target(entry, count, index, states):
    if index >= count:
        return False
    word = states[((entry & 1), (entry >> 4) & 15)]
    return bool(word & 0xFFF) and occurrence_key(word) == entry >> 8


def main():
    validate_preservation()
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
    for index, resource_id, name, row_index, expected_row in [
        (753, 500, "Man-Eater Bug", 91,
         (500, 0x00585C80, 0x0057A930, 0, 0, 0x005944D0)),
        (363, 1023, "Tribute to The Doomed", 187,
         (1023, 0x00585C80, 0x0057A9A0, 0x00580D20, 0x0057D7C0, 0x005944D0)),
    ]:
        actual_name = resources["names"][index * 64:(index + 1) * 64].split(b"\0")[0].decode("ascii")
        actual_id = struct.unpack_from("<H", ids, index * 2)[0]
        row = struct.unpack_from("<6I", binary, 0x001ED0A8 + row_index * 24)
        if (actual_name, actual_id, row) != (name, resource_id, expected_row):
            raise ValueError("Unexpected card registration")
        offset = struct.unpack_from("<I", resources["offsets"], index * 4)[0]
        description = resources["desc"][offset:].split(b"\0")[0].decode("ascii")
        cards.append(dict(name=name, index=index, resource_id=resource_id,
                          registration_index=row_index, callbacks=[hex(x) for x in row[1:]],
                          description=description))
    checks = []
    # These .text VAs map to raw offsets VA - image base in this fingerprinted PE.
    for va, expected in [
        (0x005929FB, "80e10f"),
        (0x00592A17, "c0e0048bb496745da5000ac1"),
        (0x00592A27, "c1ea1880e27fc1eb0cd0e280e30102d3"),
        (0x00592A63, "c1e80d6689544106"),
        (0x00583482, "668b547e068a4c7e068ac28adec0e80483e10f83e00f81e3ff000000"),
        (0x005834AD, "668b5604c1ea0d3bfa"),
        (0x005834DE, "a9ff0f0000"),
        (0x005834EC, "8bc85fc1e918c1e80c83e17f83e0015e5d8d144833c03bd35b0f94c0c3"),
    ]:
        offset = va - 0x00400000
        actual = binary[offset:offset + len(bytes.fromhex(expected))].hex()
        if actual != expected:
            raise ValueError(f"Instruction mismatch at {va:#x}")
        checks.append(dict(va=hex(va), file_offset=hex(offset), bytes=actual))
    accepted = rejected_keys = rejected_empty = rejected_count = 0
    for side in range(2):
        for position in range(5):
            for key in range(256):
                word = 753 | ((key & 1) << 12) | ((key >> 1) << 24)
                entry = pack_target(side, position, word)
                if (entry & 1, (entry >> 4) & 15, entry >> 8) != (side, position, key):
                    raise ValueError("Target encoding roundtrip mismatch")
                states = {(side, position): word}
                if not resolve_target(entry, 1, 0, states):
                    raise ValueError("Matching target rejected by model")
                accepted += 1
                for other_key in range(256):
                    if other_key == key:
                        continue
                    states[(side, position)] = 753 | ((other_key & 1) << 12) | ((other_key >> 1) << 24)
                    if resolve_target(entry, 1, 0, states):
                        raise ValueError("Different key accepted by model")
                    rejected_keys += 1
                states[(side, position)] = word & ~0xFFF
                if resolve_target(entry, 1, 0, states):
                    raise ValueError("Empty position accepted by model")
                rejected_empty += 1
                states[(side, position)] = word
                if resolve_target(entry, 0, 0, states):
                    raise ValueError("Missing entry accepted by model")
                rejected_count += 1
    report = dict(target_sha256=hashlib.sha256(binary).hexdigest(), cards=cards,
                  instruction_checks=checks,
                  model_cases=dict(accepted=accepted, rejected_keys=rejected_keys,
                                   rejected_empty=rejected_empty, rejected_count=rejected_count),
                  method="Static bytes, resource identities and Python bitfield model; no native execution.",
                  limits="Valid monster positions only; key reuse, selection UI, costs and interactions not tested.")
    (repository / "docs/evidence/monster-target-validation.json").write_text(
        json.dumps(report, indent=2) + "\n")
    print("PASS: monster card registrations, target bitfields and stale-target model")


if __name__ == "__main__":
    main()
