"""Decode the fingerprinted EXE ritual bindings; never execute the target."""

import contextlib
import hashlib
import io
import json
from pathlib import Path
import runpy
import struct

from catalog_card_callbacks import parse_identities, parse_callbacks, CARD_ID_SHA256
from validate_command_4d import main as validate_preservation


def parse_rules(payload):
    if not payload or len(payload) % 4:
        raise ValueError("Empty or truncated ritual table")
    rules = []
    spells = set()
    for row, (word,) in enumerate(struct.iter_unpack("<I", payload)):
        if word == 0:
            if any(payload[(row + 1) * 4:]):
                raise ValueError("Nonzero data after ritual sentinel")
            return rules
        monster_id, spell_id, level = word & 0x1FFF, (word >> 13) & 0x1FFF, word >> 26
        if not monster_id or not spell_id or not level or spell_id in spells:
            raise ValueError("Invalid or duplicate ritual binding")
        spells.add(spell_id)
        rules.append(dict(row=row, raw_word=hex(word), monster_id=monster_id,
                          spell_id=spell_id, required_level=level))
    raise ValueError("Missing ritual sentinel")


def main():
    validate_preservation()
    repository = Path(__file__).resolve().parent.parent
    binary = (repository.parent / "passion/joey_pc.exe").read_bytes()
    rules = parse_rules(binary[0x001ED060:0x001ED0A8])
    callbacks = parse_callbacks(binary[0x001ED0A8:0x001ED0A8 + 443 * 24], [(0x00401000, 0x005DA000)])
    ritual_ids = {row[0] for row in callbacks if row[1:] == (0x005999D0, 0, 0x00599850, 0, 0)}
    if len(rules) != 16 or {rule["spell_id"] for rule in rules} != ritual_ids:
        raise ValueError("Ritual table and callback profile disagree")
    with contextlib.redirect_stdout(io.StringIO()):
        resources = runpy.run_path(str(repository / "scripts/inspect_parasite_resources.py"))
    with (repository.parent / "passion/data.dat").open("rb") as stream:
        stream.seek(1273896)
        ids = stream.read(2230)
    if hashlib.sha256(ids).hexdigest() != CARD_ID_SHA256:
        raise ValueError("Unexpected CARD_ID fingerprint")
    identities = parse_identities(ids, resources["names"], resources["props"])
    for rule in rules:
        for role in ["spell", "monster"]:
            matches = identities.get(rule[role + "_id"], [])
            if not matches or not all(item["name"] for item in matches):
                raise ValueError("Ritual binding has no named identity")
            rule[role] = matches
        levels = [(struct.unpack_from("<I", resources["props"], item["index"] * 4)[0] >> 25) & 15
                  for item in rule["monster"]]
        if any(level != rule["required_level"] for level in levels):
            raise ValueError("Ritual requirement disagrees with monster base Level")
        rule["monster_base_levels"] = levels
    black_luster = next(rule for rule in rules if rule["spell_id"] == 669)
    if (black_luster["row"], black_luster["monster_id"], black_luster["required_level"]) != (2, 363, 8):
        raise ValueError("Black Luster Ritual binding mismatch")
    index = black_luster["spell"][0]["index"]
    start = struct.unpack_from("<I", resources["offsets"], index * 4)[0]
    description = resources["desc"][start:].split(b"\0")[0].decode("ascii")
    if '"Black Luster Soldier"' not in description or "equal 8 or more" not in description:
        raise ValueError("Local Ritual text mismatch")
    report = dict(target_sha256=hashlib.sha256(binary).hexdigest(),
                  table=dict(va="0x5ed060", file_offset="0x1ed060", stride=4, count=len(rules),
                             sentinel_va="0x5ed0a0", fields="monster ID bits 0-12; spell ID bits 13-25; required Level bits 26-31"),
                  callback_profile="P269", rules=rules, black_luster_description=description,
                  validation=dict(profile_members_match=True, monster_base_levels_match=True,
                                  method="Static packed data, callback records and local resource cross-checks; no native execution.",
                                  limits="Material selection, payment, summon completion and timing not validated by this catalog."))
    (repository / "docs/evidence/ritual-rules-catalog.json").write_text(json.dumps(report, indent=2) + "\n")
    print("PASS: 16 ritual bindings, complete callback profile and monster base Levels")


if __name__ == "__main__":
    main()
