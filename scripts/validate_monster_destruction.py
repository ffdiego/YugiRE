"""Check the static destruction route, destination writes and token boundaries."""

import contextlib
import hashlib
import io
import json
from pathlib import Path
import runpy
import struct

from validate_monster_targets import main as validate_targets


def destination_model(resource_id, banisher_counted):
    """Transcribed subset: field source, destination 14, incoming flag bit 1 set."""
    is_token = 0x780 <= resource_id <= 0x783
    contributes_to_grave = not is_token and not banisher_counted
    command_mode = 1 if contributes_to_grave else 16
    consumer_region = 15 if command_mode == 16 else 14
    return command_mode, consumer_region, not is_token


def main():
    validate_targets()
    repository = Path(__file__).resolve().parent.parent
    binary = (repository.parent / "passion/joey_pc.exe").read_bytes()
    checks = []
    for va, expected in [
        (0x005769F7, "6853040000e8bf2fffff83c40485c07408"),
        (0x00576A08, "814c242c00000100"),
        (0x00576C5E, "83fa0e7518f7c3000001007410c74424300f000000c744243800000000"),
        (0x00576D29, "8b4c24388b542428f7d91bc95383e1f183c110f7da1bd25181e2008000005083ca7952"),
        (0x00404E8B, "e800b91600"),
        (0x00404EC1, "e80aba1600"),
        (0x00576F07, "e874030000"),
        (0x00570848, "e8a3d50400"),
        (0x0056C615, "e8a6afffff"),
        (0x0056C6D6, "e8e5aeffff"),
    ]:
        offset = va - 0x00400000
        actual = binary[offset:offset + len(bytes.fromhex(expected))].hex()
        if actual != expected:
            raise ValueError(f"Instruction mismatch at {va:#x}")
        checks.append(dict(va=hex(va), file_offset=hex(offset), bytes=actual))
    normalized = binary[0x001B9AEC + 0x79 - 1]
    stub = struct.unpack_from("<I", binary, 0x001B9838 + normalized * 4)[0]
    offset = stub - 0x00400000
    if binary[offset] != 0xE9:
        raise ValueError("Expected command dispatch jump")
    handler = stub + 5 + struct.unpack_from("<i", binary, offset + 1)[0]
    if (normalized, stub, handler) != (79, 0x005B966D, 0x00404B40):
        raise ValueError("Unexpected command 0x79 dispatch")
    with contextlib.redirect_stdout(io.StringIO()):
        resources = runpy.run_path(str(repository / "scripts/inspect_parasite_resources.py"))
    with (repository.parent / "passion/data.dat").open("rb") as stream:
        stream.seek(1273896)
        ids = stream.read(2230)
    if hashlib.sha256(ids).hexdigest() != "a36cde2192fa85d9ccbcdb0ddb8ba8494a5199ea31d11a50a70b5fd32bf5879c":
        raise ValueError("CARD_ID fingerprint mismatch")
    cards = []
    for index, resource_id, expected_name in [
        (741, 0x453, "Banisher of the Light"),
        (726, 0x488, "Premature Burial"),
        (1061, 0x447, "Call Of The Haunted"),
        (1111, 0x780, "Insect Monster Token"),
        (1112, 0x781, "Kuriboh Token"),
        (1113, 0x782, "Sheep Token"),
        (1114, 0x783, "Slime Token"),
    ]:
        name = resources["names"][index * 64:(index + 1) * 64].split(b"\0")[0].decode("ascii")
        if (name, struct.unpack_from("<H", ids, index * 2)[0]) != (expected_name, resource_id):
            raise ValueError("Unexpected exception card identity")
        start = struct.unpack_from("<I", resources["offsets"], index * 4)[0]
        description = resources["desc"][start:].split(b"\0")[0].decode("ascii")
        cards.append(dict(index=index, resource_id=resource_id, name=name, description=description))
    cases = []
    for side in range(2):
        for owner in range(2):
            for banisher_counted in [False, True]:
                for resource_id in [500, 1023, 0x77F, 0x780, 0x781, 0x782, 0x783, 0x784]:
                    mode, region, append = destination_model(resource_id, banisher_counted)
                    expected_append = resource_id not in range(0x780, 0x784)
                    if append != expected_append:
                        raise ValueError("Token interval boundary mismatch")
                    if append and (region, mode) != ((15, 16) if banisher_counted else (14, 1)):
                        raise ValueError("Destination model mismatch")
                    # The destination helpers derive their block from the state word's bit 12.
                    word = 753 | (owner << 12)
                    destination_block = (word >> 12) & 1
                    if destination_block != owner:
                        raise ValueError("Owner block mismatch")
                    cases.append(dict(side=side, owner=owner, banisher_counted=banisher_counted,
                                      resource_id=resource_id, mode=mode, region=region,
                                      append=append, destination_block=destination_block))
    report = dict(target_sha256=hashlib.sha256(binary).hexdigest(),
                  command=dict(opcode="0x79", normalized=normalized, stub=hex(stub), handler=hex(handler)),
                  instruction_checks=checks, cards=cards, model_case_count=len(cases),
                  model_samples=[cases[i] for i in [0, 3, 8, 11, 16, 24, 32, 40, 48, 56]],
                  method="Static bytes/resources and transcribed destination model; no native execution.",
                  limits="No complete eligibility, dependency, event or rendering emulation; region names corroborated by local card text.")
    (repository / "docs/evidence/monster-destruction-validation.json").write_text(
        json.dumps(report, indent=2) + "\n")
    print("PASS: command 0x79 dispatch, exception identities and destruction destination model")


if __name__ == "__main__":
    main()
