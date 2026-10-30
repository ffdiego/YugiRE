"""Catalog the fingerprinted EXE callback table; do not execute/decompile effects."""

import collections
import contextlib
import csv
import hashlib
import io
import json
from pathlib import Path
import runpy
import struct

from validate_command_4d import main as validate_preservation


TABLE_VA = 0x005ED0A8
TABLE_OFFSET = 0x001ED0A8
ROW_COUNT = 443
ROW_SIZE = 24
SLOTS = ["primary", "candidate", "availability", "preparation", "target_preparation"]
TARGET_SHA256 = "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de"
CARD_ID_SHA256 = "a36cde2192fa85d9ccbcdb0ddb8ba8494a5199ea31d11a50a70b5fd32bf5879c"


def parse_callbacks(payload, executable_ranges):
    """Parse an exact slice of sorted six-DWORD records; reject invalid pointers."""
    if not payload or len(payload) % ROW_SIZE:
        raise ValueError("Callback table is empty or truncated")
    rows = []
    for row_index, values in enumerate(struct.iter_unpack("<6I", payload)):
        resource_id, *callbacks = values
        if resource_id > 0xFFFF:
            raise ValueError("Nonzero upper bits in resource ID field")
        if rows and resource_id <= rows[-1][0]:
            raise ValueError("Callback IDs must be strictly increasing")
        for callback in callbacks:
            if callback and not any(start <= callback < end for start, end in executable_ranges):
                raise ValueError(f"Non-executable callback in row {row_index}: {callback:#x}")
        rows.append(values)
    return rows


def parse_identities(ids, names, properties):
    """Keep duplicate IDs as aliases and empty names as explicit data."""
    if len(ids) % 2 or len(names) != len(ids) // 2 * 64 or len(properties) != len(ids) * 2:
        raise ValueError("Card identity resource sizes disagree")
    result = collections.defaultdict(list)
    for index, (resource_id,) in enumerate(struct.iter_unpack("<H", ids)):
        if resource_id == 0xFFFF:
            continue
        record = names[index * 64:(index + 1) * 64]
        if b"\0" not in record:
            raise ValueError("Unterminated card name")
        name = record.split(b"\0", 1)[0].decode("ascii")
        word = struct.unpack_from("<I", properties, index * 4)[0]
        result[resource_id].append(dict(index=index, name=name, class_code=(word >> 20) & 31))
    return dict(result)


def native_search_model(rows, target):
    """Transcribe 0x0059DBE0 after its conversion to uint16 resource ID."""
    low, high = 0, len(rows) - 1
    index = high // 2
    for _ in range(32):
        value = rows[index][0]
        if target == value:
            return index
        if low == high:
            return -1
        if value < target:
            low = index
        if target < value:
            high = index
        if (high + low) // 2 == index:
            low = high
        index = (high + low) // 2
    raise ValueError("Native search model failed to terminate")


def group_callbacks(rows):
    profiles = collections.defaultdict(list)
    primary = collections.defaultdict(list)
    for index, row in enumerate(rows):
        profiles[tuple(row[1:])].append(index)
        primary[row[1]].append(index)
    return dict(profiles), dict(primary)


def build_catalog(binary, ids, names, properties):
    if hashlib.sha256(binary).hexdigest() != TARGET_SHA256:
        raise ValueError("Unexpected EXE fingerprint")
    if hashlib.sha256(ids).hexdigest() != CARD_ID_SHA256:
        raise ValueError("Unexpected CARD_ID fingerprint")
    for payload, expected in [
        (names, "74848d0b2cd7d5fac7a767075bd5b01e0366183ce934afa6070ce24394a24032"),
        (properties, "1a772642a04d86a2cfcebba2d0f4ff40a1737761296d0ea6a2645f5ed2bf0b9e"),
    ]:
        if hashlib.sha256(payload).hexdigest() != expected:
            raise ValueError("Unexpected name/property fingerprint")
    # This pinned PE maps both .text and the table's .data VAs to raw RVA offsets.
    rows = parse_callbacks(binary[TABLE_OFFSET:TABLE_OFFSET + ROW_COUNT * ROW_SIZE],
                           [(0x00401000, 0x005DA000)])
    if len(rows) != ROW_COUNT:
        raise ValueError("Unexpected callback table size")
    for va, expected in [(0x0059DBEA, "bfba010000"),
                         (0x0059DC01, "b9dd000000"),
                         (0x0059DC32, "668b04d5a8d05e00")]:
        offset = va - 0x00400000
        if binary[offset:offset + len(bytes.fromhex(expected))] != bytes.fromhex(expected):
            raise ValueError("Search/table boundary instruction mismatch")
    expected_rows = {row[0]: index for index, row in enumerate(rows)}
    for resource_id, row_index, expected_callbacks in [
        (342, 66, (0x00584E60, 0, 0, 0, 0x00593CC0)),
        (345, 69, (0x00584E60, 0, 0, 0, 0x00593CC0)),
        (500, 91, (0x00585C80, 0x0057A930, 0, 0, 0x005944D0)),
        (762, 150, (0x005875A0, 0, 0x005800A0, 0x0057CCC0, 0)),
        (1023, 187, (0x00585C80, 0x0057A9A0, 0x00580D20, 0x0057D7C0, 0x005944D0)),
        (1530, 418, (0x00588D90, 0, 0, 0, 0)),
    ]:
        if expected_rows.get(resource_id) != row_index or tuple(rows[row_index][1:]) != expected_callbacks:
            raise ValueError("Previously analyzed callback registration mismatch")
    for target in range(65536):
        if native_search_model(rows, target) != expected_rows.get(target, -1):
            raise ValueError(f"Search model mismatch for {target}")
    identities = parse_identities(ids, names, properties)
    profiles, primary = group_callbacks(rows)
    profile_ids = {callbacks: f"P{number:03d}" for number, callbacks in enumerate(sorted(profiles), 1)}
    records = []
    for index, row in enumerate(rows):
        matches = identities.get(row[0], [])
        status = "unmapped_id" if not matches else ("empty_name" if not any(x["name"] for x in matches) else "named")
        records.append(dict(row=index, resource_id=row[0], profile=profile_ids[tuple(row[1:])],
                            status=status, identities=matches))
    summary = dict(records=len(rows), exact_profiles=len(profiles),
                   distinct_primary_callbacks=sum(bool(pointer) for pointer in primary),
                   distinct_callback_addresses=len({value for row in rows for value in row[1:] if value}),
                   null_primary_records=len(primary.get(0, [])),
                   all_null_records=len(profiles.get((0, 0, 0, 0, 0), [])),
                   named_records=sum(row["status"] == "named" for row in records),
                   empty_name_records=sum(row["status"] == "empty_name" for row in records),
                   unmapped_ids=[row["resource_id"] for row in records if row["status"] == "unmapped_id"],
                   source_ids_with_registration=len(set(identities) & set(expected_rows)),
                   source_ids_without_registration=len(set(identities) - set(expected_rows)),
                   per_slot_distinct_nonnull={slot: len({row[i + 1] for row in rows if row[i + 1]})
                                             for i, slot in enumerate(SLOTS)})
    return dict(target_sha256=TARGET_SHA256, card_id_sha256=CARD_ID_SHA256,
                names_sha256=hashlib.sha256(names).hexdigest(),
                properties_sha256=hashlib.sha256(properties).hexdigest(),
                table=dict(va=hex(TABLE_VA), file_offset=hex(TABLE_OFFSET), stride=ROW_SIZE,
                           count=ROW_COUNT, first_id=rows[0][0], last_id=rows[-1][0]),
                slot_order=SLOTS, summary=summary,
                profiles=[dict(id=profile_ids[callbacks], callbacks=[hex(v) if v else None for v in callbacks], rows=profiles[callbacks])
                          for callbacks in sorted(profiles)],
                primary_groups=[dict(callback=hex(pointer) if pointer else None, rows=members)
                                for pointer, members in sorted(primary.items(), key=lambda item: (-len(item[1]), item[0]))],
                records=records, validation=dict(search_model_domain=65536,
                    method="Static table/resource checks and finite-domain search model; no native execution.",
                    limitation="Pointer equality defines groups, not semantic equivalence or complete effect coverage."))


def write_outputs(repository, catalog):
    evidence = repository / "docs/evidence"
    (evidence / "card-callback-catalog.json").write_text(json.dumps(catalog, indent=2) + "\n")
    profiles = {profile["id"]: profile for profile in catalog["profiles"]}
    with (evidence / "card-callback-catalog.csv").open("w", newline="") as stream:
        writer = csv.writer(stream, lineterminator="\n")
        writer.writerow(["row", "row_va", "resource_id", "resource_id_hex", "profile", "status", "indices", "names", *SLOTS])
        for row in catalog["records"]:
            writer.writerow([row["row"], hex(TABLE_VA + row["row"] * ROW_SIZE), row["resource_id"],
                             hex(row["resource_id"]), row["profile"], row["status"],
                             " | ".join(str(x["index"]) for x in row["identities"]),
                             " | ".join(x["name"] for x in row["identities"]),
                             *[value or "" for value in profiles[row["profile"]]["callbacks"]]])


def main():
    validate_preservation()
    repository = Path(__file__).resolve().parent.parent
    with contextlib.redirect_stdout(io.StringIO()):
        resources = runpy.run_path(str(repository / "scripts/inspect_parasite_resources.py"))
    with (repository.parent / "passion/data.dat").open("rb") as stream:
        stream.seek(1273896)
        ids = stream.read(2230)
    binary = (repository.parent / "passion/joey_pc.exe").read_bytes()
    catalog = build_catalog(binary, ids, resources["names"], resources["props"])
    write_outputs(repository, catalog)
    print(json.dumps(catalog["summary"], indent=2))
    print("PASS: callback table, executable pointers, resource identities and 65536 search model cases")


if __name__ == "__main__":
    main()
