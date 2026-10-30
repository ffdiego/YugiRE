"""Check static association cleanup and a transcribed parallel-array model."""

import hashlib
import json
from pathlib import Path
import struct

from validate_legendary_sword import main as validate_previous


COORDINATE_KINDS = {1, 2, 4, 5, 6, 7, 8}


def erase_model(positions, metadata, count, index):
    if index < count:
        count -= 1
        for current in range(index, count):
            positions[current] = positions[current + 1]
            metadata[current] = metadata[current + 1]
    return count


def cleanup_model(positions, metadata, count, leaving):
    index = 0
    while index < count:
        if metadata[index] & 15 in COORDINATE_KINDS and positions[index] == leaving:
            count = erase_model(positions, metadata, count, index)
        else:
            index += 1
    return count


def main():
    validate_previous()
    repository = Path(__file__).resolve().parent.parent
    binary = (repository.parent / "passion/joey_pc.exe").read_bytes()
    checks = []
    for va, expected in [
        (0x0056A505, "e8d6f0ffff"),
        (0x0056A50D, "4e83ef0283ed02"),
        (0x00569620, "486689410a"),
        (0x00569632, "668b700242668930668b704266897040"),
        (0x00570857, "e8349cffff"),
        (0x0057778D, "e89ef3ffff"),
    ]:
        offset = va - 0x00400000
        actual = binary[offset:offset + len(bytes.fromhex(expected))].hex()
        if actual != expected:
            raise ValueError(f"Instruction mismatch at {va:#x}")
        checks.append(dict(va=hex(va), file_offset=hex(offset), bytes=actual))
    dispatch = []
    for kind in range(1, 9):
        normalized = binary[0x0016A55C + kind - 1]
        branch = struct.unpack_from("<I", binary, 0x0016A554 + normalized * 4)[0]
        if branch != (0x0056A4EB if kind in COORDINATE_KINDS else 0x0056A514):
            raise ValueError("Coordinate association cleanup dispatch mismatch")
        dispatch.append(dict(kind=kind, normalized=normalized, branch=hex(branch)))
    leaving = 0x0501
    scenarios = [
        ("empty", []),
        ("single_match", [(leaving, 1)]),
        ("no_match", [(0x0601, 1), (0x0700, 2)]),
        ("adjacent_matches", [(leaving, 1), (leaving, 2), (leaving, 4)]),
        ("middle_pair", [(0x0100, 0xAB01), (leaving, 1), (0x0200, 0xCD02)]),
        ("last_pair", [(0x0100, 1), (leaving, 8)]),
        ("noncoordinate_same_value", [(leaving, kind) for kind in [0, 3, 9, 11, 13, 15]]),
        ("metadata_high_bits", [(leaving, 0xAB01), (leaving, 0xCD03), (0x0600, 0xEF02)]),
        ("mixed_duplicates", [(leaving, 1), (0x0600, 4), (leaving, 13), (leaving, 7)]),
    ]
    cases = []
    for name, pairs in scenarios:
        positions = [pair[0] for pair in pairs] + [0xDEED, 0xDEED]
        metadata = [pair[1] for pair in pairs] + [0xABCD, 0xABCD]
        count = cleanup_model(positions, metadata, len(pairs), leaving)
        active = list(zip(positions[:count], metadata[:count]))
        expected = [pair for pair in pairs if not (pair[0] == leaving and pair[1] & 15 in COORDINATE_KINDS)]
        if active != expected or count != len(expected):
            raise ValueError(f"Cleanup model mismatch: {name}")
        if name == "single_match" and (positions[0], metadata[0]) != pairs[0]:
            raise ValueError("The native erase helper does not zero inactive tail entries")
        cases.append(dict(name=name, before=pairs, active_after=active, count_after=count))
    report = dict(target_sha256=hashlib.sha256(binary).hexdigest(), instruction_checks=checks,
                  kind_dispatch=dispatch, model_cases=cases,
                  cleanup_scan=dict(sides=2, positions_per_side=12, coordinate_kinds=sorted(COORDINATE_KINDS)),
                  method="Static bytes/table checks and nine Python model scenarios; no native execution.",
                  limits="Valid internal counts and nonnegative indices; complete timing, all movement routes and special equipment interactions pending.")
    (repository / "docs/evidence/equipment-cleanup-validation.json").write_text(json.dumps(report, indent=2) + "\n")
    print("PASS: coordinate cleanup dispatch, paired compaction and nine transcription scenarios")


if __name__ == "__main__":
    main()
