"""Cross-check two local card identities and their shared damage callback."""

import contextlib
import hashlib
import io
import json
from pathlib import Path
import runpy
import struct

from validate_parasite_lifecycle import main as validate_preservation


def main():
    validate_preservation()
    repository = Path(__file__).resolve().parent.parent
    with contextlib.redirect_stdout(io.StringIO()):
        resources = runpy.run_path(str(repository / "scripts/inspect_parasite_resources.py"))
    with (repository.parent / "passion/data.dat").open("rb") as stream:
        stream.seek(1273896)
        ids = stream.read(2230)
    if hashlib.sha256(ids).hexdigest() != "a36cde2192fa85d9ccbcdb0ddb8ba8494a5199ea31d11a50a70b5fd32bf5879c":
        raise ValueError("CARD_ID fingerprint mismatch")
    binary = (repository.parent / "passion/joey_pc.exe").read_bytes()
    cards = []
    for index, expected_id, expected_name, row_index, amount, immediate_va in [
        (755, 342, "Sparks", 66, 200, 0x00584F1F),
        (557, 345, "Ookazi", 69, 800, 0x00584FAB),
    ]:
        name = resources["names"][index * 64:(index + 1) * 64].split(b"\0")[0].decode("ascii")
        resource_id = struct.unpack_from("<H", ids, index * 2)[0]
        row = struct.unpack_from("<6I", binary, 0x001ED0A8 + row_index * 24)
        if (name, resource_id, row) != (expected_name, expected_id,
                (expected_id, 0x00584E60, 0, 0, 0, 0x00593CC0)):
            raise ValueError("Unexpected card registration")
        if struct.unpack_from("<I", binary, immediate_va - 0x00400000)[0] != amount:
            raise ValueError("Damage immediate mismatch")
        offset = struct.unpack_from("<I", resources["offsets"], index * 4)[0]
        description = resources["desc"][offset:].split(b"\0")[0].decode("ascii")
        cards.append(dict(name=name, index=index, resource_id=resource_id,
                          registration_index=row_index, callback=hex(row[1]),
                          target_callback=hex(row[5]), amount=amount,
                          immediate_va=hex(immediate_va), description=description))
    (repository / "docs/evidence/simple-lp-cards-validation.json").write_text(
        json.dumps(dict(cards=cards, method="Static registration, local resources and immediate checks; no target execution."), indent=2) + "\n")
    print("PASS: Sparks/Ookazi identities, shared callbacks and damage immediates")


if __name__ == "__main__":
    main()
