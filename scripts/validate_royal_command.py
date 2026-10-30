"""Check Royal Command identity, registration and static suppression branches."""

import contextlib
import hashlib
import io
import json
from pathlib import Path
import runpy
import struct

from validate_flip_activation import main as validate_previous


def main():
    validate_previous()
    repository = Path(__file__).resolve().parent.parent
    binary = (repository.parent / "passion/joey_pc.exe").read_bytes()
    with contextlib.redirect_stdout(io.StringIO()):
        resources = runpy.run_path(str(repository / "scripts/inspect_parasite_resources.py"))
    with (repository.parent / "passion/data.dat").open("rb") as stream:
        stream.seek(1273896)
        ids = stream.read(2230)
    if hashlib.sha256(ids).hexdigest() != "a36cde2192fa85d9ccbcdb0ddb8ba8494a5199ea31d11a50a70b5fd32bf5879c":
        raise ValueError("CARD_ID fingerprint mismatch")
    index = 108
    name = resources["names"][index * 64:(index + 1) * 64].split(b"\0")[0].decode("ascii")
    resource_id = struct.unpack_from("<H", ids, index * 2)[0]
    row = struct.unpack_from("<6I", binary, 0x001ED0A8 + 418 * 24)
    if (name, resource_id, row) != ("Royal Command", 0x5FA, (0x5FA, 0x00588D90, 0, 0, 0, 0)):
        raise ValueError("Royal Command registration mismatch")
    start = struct.unpack_from("<I", resources["offsets"], index * 4)[0]
    description = resources["desc"][start:].split(b"\0")[0].decode("ascii")
    if description != "Negates the activation and effects of all Flip Effect monsters.":
        raise ValueError("Royal Command local text mismatch")
    checks = []
    for va, expected in [
        (0x0057EB7D, "68fa050000e839aefeff83c40485c0741e"),
        (0x005BB87D, "68fa050000e839e1faff83c40485c0750a"),
        (0x0057540E, "68fa050000e8a845ffff83c40485c07539"),
        (0x00588DB1, "803d8a5ca50080"),
        (0x00588F58, "2df5050000746383e805740a"),
        (0x00588FAB, "e870e6fdff"),
        (0x00588FB7, "804e040e"),
        (0x00598E1F, "576a05e8c90afeff"),
        (0x005BB8B6, "8b15885ca5005f81e2c9ff00005e80cac85d668915885ca500"),
    ]:
        offset = va - 0x00400000
        actual = binary[offset:offset + len(bytes.fromhex(expected))].hex()
        if actual != expected:
            raise ValueError(f"Instruction mismatch at {va:#x}")
        checks.append(dict(va=hex(va), file_offset=hex(offset), bytes=actual))
    normalized = binary[0x001B9AEC + 0xB1 - 1]
    stub = struct.unpack_from("<I", binary, 0x001B9838 + normalized * 4)[0]
    offset = stub - 0x00400000
    if binary[offset] != 0xE9:
        raise ValueError("Expected command dispatch jump")
    handler = stub + 5 + struct.unpack_from("<i", binary, offset + 1)[0]
    if (normalized, stub, handler) != (114, 0x005B9708, 0x00408730):
        raise ValueError("Suppression command consumer mismatch")
    report = dict(target_sha256=hashlib.sha256(binary).hexdigest(),
                  card=dict(index=index, resource_id=resource_id, name=name, description=description,
                            registration_index=418, row=[hex(value) for value in row]),
                  instruction_checks=checks,
                  command=dict(opcode="0xb1", normalized=normalized, stub=hex(stub), handler=hex(handler)),
                  state_fields=dict(global_flags_va="0xa577ec", global_mask="0x20",
                                    position_flags_offset="0x8c", position_mask="0x20",
                                    prior_request_flags_offset=4, prior_request_or_mask="0xe",
                                    resolution_skip_state=100),
                  method="Static original/resource/registration/instruction checks; no native execution.",
                  limits="Does not execute the branches, prove response ordering or cover all suppression interactions.")
    (repository / "docs/evidence/royal-command-validation.json").write_text(json.dumps(report, indent=2) + "\n")
    print("PASS: Royal Command identity, registration, suppression branches and command 0xb1")


if __name__ == "__main__":
    main()
