#!/usr/bin/env python3
"""Build a bounded differential corpus from an existing passive Joey capture.

No execution, instrumentation, emulation or save access. The builder preserves
unknown context instead of choosing a position or absence of effects to fit an
expected event. DD30 buffers are inputs to a *conditional* pure evaluator;
score and removal bits remain independent observed expectations.

  python scripts/build_ai_combat_corpus.py capture.jsonl --expected-sha256 HASH \
      --dat ../passion/data.dat > corpus.json
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import struct

from ai_decision_trace_audit import audit
from decode_ai_combat_event import decode_hex as decode_event
from decode_ai_combat_stats import decode_hex as decode_stats

EXE_SHA256 = "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de"
DAT_SHA256 = "7e8841ceb015861dacf898d4a62a909e399da14bb7d69c40de3afa3a4610e1ed"
CARD_ID_SHA256 = "a36cde2192fa85d9ccbcdb0ddb8ba8494a5199ea31d11a50a70b5fd32bf5879c"
KNOWN_CAPTURE = {
    "commands-20261111-022940-355228-pid36348.jsonl":
        "dd2029e8397d22401b7790ce3f007c9353cef94d5e6a9c14dbe1690d20206a90",
    "commands-20261111-034109-625866-pid29768.jsonl":
        "ea1ed03746df2fbd56f4ed9c2fd8abee7f502b2687a683c4597346183f7df449",
    "commands-20261111-043826-452599-pid23536.jsonl":
        "6c93df404998d8b0807ecfb98831a73db5485d274d0cfb29b5ae876a6d6fc5d3",
}
SELECTORS = {"select_normal", "select_priority"}
UNKNOWN_CONTEXT = [
    "defender position word / attack-versus-defense branch",
    "attacker and defender player flags 0x400000",
    "eligible Metalmorph link count",
    "piercing link predicates and source sides",
    "resource 0x58F / gate 0x14D suppression predicates",
    "initial descriptor bits preserved by mask 0xF005",
]


def read_card_ids(path: Path) -> dict[int, int]:
    """Read the pinned, uncompressed card-ID member through its DAT index."""
    with path.open("rb") as source:
        if hashlib.file_digest(source, "sha256").hexdigest() != DAT_SHA256:
            raise ValueError("Unsupported DAT SHA256")
        source.seek(0)
        if source.read(8) != b"KCEJYUGI":
            raise ValueError("Invalid DAT header")
        count = struct.unpack("<I", source.read(4))[0]
        match = None
        for _ in range(count):
            row = source.read(268)
            if len(row) != 268:
                raise ValueError("Truncated DAT index")
            name = bytes((v >> 4) | ((v & 15) << 4) for v in row[:256])
            name = name.split(b"\0", 1)[0].decode("ascii").lower()
            if name == "bin#\\card_id.bin":
                if match is not None:
                    raise ValueError("Duplicate card-ID member")
                match = struct.unpack_from("<III", row, 256)
        if match is None:
            raise ValueError("Missing card-ID member")
        offset, size, stored = match
        if size != stored or size > 65536 or size % 2:
            raise ValueError("Unsupported card-ID member layout")
        source.seek(offset)
        payload = source.read(stored)
        if hashlib.sha256(payload).hexdigest() != CARD_ID_SHA256:
            raise ValueError("Unsupported card-ID member SHA256")
    return {index: resource for index, (resource,) in
            enumerate(struct.iter_unpack("<H", payload)) if resource != 0xFFFF}


def stat_input(row: dict, resource_ids: dict[int, int] | None) -> dict:
    value = decode_stats(row["outputHex"]).to_dict()
    return {
        "statsSeq": row["seq"], "statsCallId": row["callId"],
        "rawArgs": row["rawArgs"], **value,
        "resource_id": None if resource_ids is None else resource_ids.get(value["internal_card_index"]),
    }


def build_corpus(records: list[dict], source_name: str, source_sha256: str,
                 resource_ids: dict[int, int] | None = None) -> dict:
    # Reuse the stricter existing pairing/sequence/hand audit before extraction.
    audited = audit(records)
    if audited["readFailures"]:
        raise ValueError("Read failures prevent a reliable differential corpus")
    rows = records[1:]
    statistics = [r for r in rows if r.get("function") == "computed_stats"
                  and r.get("phase") == "leave"]
    events = []
    for event in rows:
        if event.get("function") != "combat_event" or event.get("phase") != "leave":
            continue
        if event.get("readError"):
            raise ValueError("Unreadable combat event")
        side, attacker_slot, target_slot = event["rawArgs"]
        if side not in (0, 1) or attacker_slot not in range(5) or target_slot not in range(5):
            raise ValueError("Unsupported combat-event argument domain")
        windows = [c for c in audited["calls"] if c["threadId"] == event["threadId"]
                   and c["enterSeq"] < event["seq"] < c["leaveSeq"]]
        if len(windows) != 1:
            raise ValueError("Combat output is not in one unambiguous selector window")
        window = windows[0]
        # The instrument's callId is allocated on entry, including unrecorded
        # output-function entries. A child returns before its parent. Match the
        # two expected argument pairs from 005B0F40's explicit ADD60/ADDC0 calls.
        children = [r for r in statistics if r["threadId"] == event["threadId"]
                    and r["callId"] > event["callId"]
                    and window["enterSeq"] < r["seq"] < event["seq"]]
        own = [r for r in children if r["rawArgs"] == [side, attacker_slot]]
        rival = [r for r in children if r["rawArgs"] == [1 - side, target_slot]]
        pairing = "paired_computed_stats" if len(children) == 2 and len(own) == len(rival) == 1 else "incomplete_computed_stats"
        # Retain a sole own buffer as observed data; never substitute a guessed
        # rival buffer or retrieve a preceding unrelated rival getter result.
        own_row = own[0] if len(own) == 1 else None
        rival_row = rival[0] if pairing == "paired_computed_stats" else None
        decoded = decode_event(event["outputHex"])
        unknown = []
        if rival_row is None:
            unknown.append("defender evaluation stats; absent DD30 may be hidden-card heuristic but does not prove it")
        if own_row is None:
            unknown.append("unambiguous attacker evaluation stats")
        events.append({
            "eventSeq": event["seq"], "eventCallId": event["callId"],
            "threadId": event["threadId"], "selectorCallId": window["callId"],
            "side": side, "attackerSlot": attacker_slot, "targetSlot": target_slot,
            "pairing": pairing,
            "attacker": None if own_row is None else stat_input(own_row, resource_ids),
            "defender": None if rival_row is None else stat_input(rival_row, resource_ids),
            "defenderPosition": None,
            "initialDescriptorLowWord": None,
            "fullyObservedContext": False,
            "additionalUnknownContext": unknown,
            "expectedBufferHex": event["outputHex"].upper(),
            "expectedKnownLowWord": decoded.low_word & 0x0FFA,
            "knownLowWordComparisonMask": 0x0FFA,
            "expectedSignedScore": decoded.score_signed_i16,
        })
    paired = sum(e["pairing"] == "paired_computed_stats" for e in events)
    return {
        "schema": "joey-ai-combat-differential-v1",
        "sourceCapture": source_name, "sourceCaptureSha256": source_sha256,
        "executableSha256": EXE_SHA256,
        "executableSha256Provenance": "associated evidence contract; raw metadata hash is reported separately",
        "metadataExeSha256": audited["metadataExeSha256"],
        "datSha256": DAT_SHA256 if resource_ids is not None else None,
        "scope": "observed computed inputs and independent native output; conditional differential tests only, not complete context replay",
        "associationEvidence": "same thread, child callId allocated after parent, return before parent, exactly matching DD30 arguments",
        "sharedUnknownContext": UNKNOWN_CONTEXT,
        "expectationProvenance": "native observed buffer; decoded expectations do not supply missing evaluator context",
        "normalizationContract": {
            "description": "A consumer may supply an explicit effects-free context and position as a documented scenario. Such a comparison is conditional compatibility, not observation of those assumptions.",
            "knownBitsOnly": "Compare lowWord with mask 0x0FFA and all 16 highWord bits; F40 preserves initial bits 0xF005 which were not captured on entry.",
            "forbiddenInference": "Do not choose position, flags or effect counts by searching for the expected output and then call the context observed.",
        },
        "coverage": {"nativeCombatBuffers": len(events), "pairedStats": paired,
                     "missingOrAmbiguousStats": len(events) - paired, "fullyObservedContexts": 0},
        "events": events,
    }


def build_file(path: Path, expected_sha256: str | None, dat: Path | None = None) -> dict:
    expected = expected_sha256 or KNOWN_CAPTURE.get(path.name)
    if expected is None:
        raise ValueError("Unknown capture name: provide --expected-sha256")
    raw = path.read_bytes()
    actual = hashlib.sha256(raw).hexdigest()
    if actual.lower() != expected.lower():
        raise ValueError("Capture SHA256 mismatch")
    records = [json.loads(line) for line in raw.decode("utf-8-sig").splitlines() if line.strip()]
    return build_corpus(records, path.name, actual, None if dat is None else read_card_ids(dat))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture", type=Path)
    parser.add_argument("--expected-sha256")
    parser.add_argument("--dat", type=Path)
    arguments = parser.parse_args()
    try:
        print(json.dumps(build_file(arguments.capture, arguments.expected_sha256, arguments.dat), indent=2))
    except (OSError, ValueError, KeyError, TypeError, struct.error) as error:
        parser.error(str(error))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
