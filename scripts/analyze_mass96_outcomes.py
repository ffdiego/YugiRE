"""Classify terminal outcomes from raw 96-card YugiLab JSONL captures.

Read-only: no native execution, no mutation of original ZIP. An activated
Gravity Bind is identified by opcode 0x0072 and resourceId 1323, NOT by
mere inclusion in a deck or by setting the Trap. The state sampler is
non-atomic: the report says "deck exhausted, LP positive", not that the
native loss reason was proved by the terminal flags.
"""
from __future__ import annotations

import argparse
from collections import Counter
import csv
import hashlib
import io
import json
from pathlib import Path
import re
import zipfile

GRAVITY_BIND_RESOURCE = 1323
GRAVITY_BIND_INDEX = 244
OBSERVED_FIELDS = ("lifePoints", "deckCount", "graveyardCount", "terminalFlags")
COHORT_RE = re.compile(r"(?:^|/)cohort-(\d{2})-rep-\d+/.+\.jsonl$")


def analyze_events(rows: list[dict], cohort: int) -> list[dict]:
    if not rows or rows[0].get("kind") != "metadata":
        raise ValueError(f"Cohort {cohort}: no metadata header")
    if rows[-1].get("kind") != "capture_summary":
        raise ValueError(f"Cohort {cohort}: no capture summary")
    footer = rows[-1]
    if footer.get("complete") is not True or footer.get("overwritten") != 0:
        raise ValueError(f"Cohort {cohort}: incomplete or lossy capture")
    if footer.get("stateTrace", {}).get("readErrors", 1):
        raise ValueError(f"Cohort {cohort}: state read errors")
    expected = footer.get("capturedDuels")
    if type(expected) is not int or expected < 1:
        raise ValueError(f"Cohort {cohort}: invalid terminal count")
    snapshots = None
    gravity = []
    result = []
    for event in rows:
        if event.get("kind") != "command":
            continue
        observation = event.get("stateObservation") or {}
        kind = observation.get("kind")
        if kind == "baseline":
            sides = observation.get("sides") or []
            if len(sides) != 2:
                raise ValueError(f"Cohort {cohort}: invalid baseline")
            snapshots = {p["side"]: {k: p.get(k) for k in OBSERVED_FIELDS}
                         for p in sides}
            if set(snapshots) != {0, 1}:
                raise ValueError(f"Cohort {cohort}: missing baseline side")
        elif kind == "delta" and snapshots is not None:
            for change in observation.get("changes", []):
                side = change.get("side")
                if side not in snapshots:
                    raise ValueError(f"Cohort {cohort}: unknown state side")
                for key in OBSERVED_FIELDS:
                    if key in change:
                        snapshots[side][key] = change[key]["after"]
        if event.get("opcodeMasked") == "0x0072" and (
            (event.get("args") or [0, 0])[1] & 0xFFFF == GRAVITY_BIND_RESOURCE
        ):
            gravity.append(event["seq"])
        if event.get("opcodeMasked") != "0x0004":
            continue
        if snapshots is None or any(
            snapshots[p].get("deckCount") is None or
            snapshots[p].get("lifePoints") is None for p in (0, 1)
        ):
            raise ValueError(f"Cohort {cohort}: terminal without state")
        p0, p1 = (snapshots[p].copy() for p in (0, 1))
        if p0["lifePoints"] == 0 or p1["lifePoints"] == 0:
            classification = "life-points-zero"
        elif p0["deckCount"] == 0 and p1["deckCount"] == 0:
            classification = "both-decks-empty-positive-lp"
        else:
            classification = "unclassified"
        result.append({
            "cohort": cohort, "duel": len(result) + 1, "terminalSeq": event["seq"],
            "outcomeEvidence": classification,
            "side0": p0, "side1": p1,
            "gravityBindEffectSequences": gravity.copy(),
            "gravityBindEffectCommands": len(gravity),
        })
        snapshots = None
        gravity = []
    if len(result) != expected or len(result) != rows[0].get("requestedDuels"):
        raise ValueError(f"Cohort {cohort}: terminal count mismatch")
    return result


def analyze_zip(path: Path) -> dict:
    path = Path(path)
    if not zipfile.is_zipfile(path):
        raise ValueError("Expected a readable campaign ZIP")
    with zipfile.ZipFile(path) as archive:
        matches = {}
        for item in archive.namelist():
            found = COHORT_RE.search(item)
            if found:
                number = int(found.group(1))
                if number in matches:
                    raise ValueError(f"Duplicate cohort capture {number}")
                matches[number] = item
        if not matches:
            raise ValueError("ZIP contains no cohort JSONL captures")
        duels = []
        for number, entry in sorted(matches.items()):
            with archive.open(entry) as stream:
                rows = [json.loads(raw) for raw in stream if raw.strip()]
            duels.extend(analyze_events(rows, number))
    categories = Counter(d["outcomeEvidence"] for d in duels)
    gravity = [d for d in duels if d["gravityBindEffectCommands"] > 0]
    return {
        "schema": "mass96-native-duel-outcome-evidence-v1",
        "sourceFilename": path.name,
        "sourceSha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        "cohortsCount": len(matches),
        "duelsCount": len(duels),
        "outcomeEvidenceCounts": dict(categories),
        "gravityBindActivationDuels": len(gravity),
        "gravityBindAndBothDecksEmpty": sum(
            d["outcomeEvidence"] == "both-decks-empty-positive-lp" for d in gravity
        ),
        "limits": [
            "A command 0x0072 with resource 1323 marks a Gravity Bind effect command, not complete chain causation.",
            "Deck count/LP are sampled non-atomically; draw-loss reason is not decoded from native terminal flags.",
            "Observation of a trapped duel is not proof that Gravity Bind alone caused its duration.",
        ],
        "duels": duels,
    }


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("zip", type=Path)
    parser.add_argument("--json", type=Path)
    parser.add_argument("--csv", type=Path)
    args = parser.parse_args(argv)
    report = analyze_zip(args.zip)
    data = json.dumps(report, ensure_ascii=False, indent=2) + "\n"
    if args.json:
        args.json.write_text(data, encoding="utf-8")
    else:
        print(data)
    if args.csv:
        with args.csv.open("w", newline="", encoding="utf-8") as f:
            w = csv.DictWriter(f, fieldnames=[
                "cohort", "duel", "terminalSeq", "outcomeEvidence",
                "gravityBindEffectCommands", "gravityBindEffectSequences",
                "lp0", "lp1", "deck0", "deck1", "terminalFlags0", "terminalFlags1",
            ])
            w.writeheader()
            for d in report["duels"]:
                w.writerow({
                    **{key: d[key] for key in (
                        "cohort", "duel", "terminalSeq", "outcomeEvidence",
                        "gravityBindEffectCommands")},
                    "gravityBindEffectSequences": ",".join(
                        map(str, d["gravityBindEffectSequences"])),
                    "lp0": d["side0"]["lifePoints"],
                    "lp1": d["side1"]["lifePoints"],
                    "deck0": d["side0"]["deckCount"],
                    "deck1": d["side1"]["deckCount"],
                    "terminalFlags0": d["side0"]["terminalFlags"],
                    "terminalFlags1": d["side1"]["terminalFlags"],
                })
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
