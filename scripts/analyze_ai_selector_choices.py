"""Read YugiRE live JSONL and correlate AI choices with original hand snapshots.

Usage:
    python scripts/analyze_ai_selector_choices.py scratch/traces/duels-...jsonl

Creates *-selector-choices.csv and *-selector-summary.json alongside the input.
Works on Fedora/Windows, needs no external Python packages or live Joey instance.
The observed hand is not a set of legally playable or scored alternatives.
"""
from __future__ import annotations

import argparse
from collections import Counter
import csv
import json
from pathlib import Path


FIELDNAMES = [
    "duel", "enterSeq", "leaveSeq", "executorSeq", "side", "selector",
    "pass", "handCount", "handIndices", "handWords", "selectedHandIndex",
    "selectedCardIndexAtEntry", "selectedCardWordAtEntry",
    "executorHandInternalIndex", "executorHandIndex",
    "choiceForwardedToExecutor", "executorMatchesEntry", "readError",
]


def scan_capture(path: Path) -> tuple[list[dict], dict]:
    pending: dict[tuple[int, int], dict] = {}
    rows: list[dict] = []
    recent_leave: dict | None = None
    duel = 0
    with path.open(encoding="utf-8") as source:
        for lineno, raw in enumerate(source, 1):
            if not raw.strip():
                continue
            event = json.loads(raw)
            if (event.get("kind") == "demo_init" and
                event.get("function") == "scene_config" and
                event.get("phase") == "enter" and
                event.get("requestedSceneMode") == 7):
                duel += 1
            if event.get("kind") == "ai_probe" and event.get("function") in (
                    "select_normal", "select_priority"):
                key = (event.get("threadId"), event.get("callId"))
                if event.get("phase") == "enter":
                    if key in pending:
                        raise ValueError(f"Duplicated AI selector enter at line {lineno}")
                    pending[key] = dict(event, duel=duel)
                elif event.get("phase") == "leave":
                    entry = pending.pop(key, None)
                    if entry is None:
                        raise ValueError(f"AI selector leave without enter at line {lineno}")
                    hand = entry.get("handIndices")
                    hand_words = entry.get("handWords")
                    ix = event.get("selectedHandIndex")
                    selected = (hand[ix] if isinstance(hand, list) and
                                isinstance(ix, int) and not isinstance(ix, bool)
                                and 0 <= ix < len(hand) else None)
                    selected_word = (hand_words[ix] if isinstance(hand_words, list)
                                     and isinstance(ix, int) and
                                     0 <= ix < len(hand_words) else None)
                    raw_card = event.get("selectedCardIndexAtEntry")
                    if raw_card is not None and raw_card != selected:
                        raise ValueError(f"Selected card mismatch in trace at {lineno}")
                    row = {
                        "duel": entry["duel"],
                        "enterSeq": entry.get("seq"),
                        "leaveSeq": event.get("seq"),
                        "executorSeq": None,
                        "side": entry.get("side"),
                        "selector": event.get("function"),
                        "pass": entry.get("pass"),
                        "handCount": entry.get("handCount"),
                        "handIndices": json.dumps(hand) if isinstance(hand, list) else "",
                        "handWords": json.dumps(hand_words) if isinstance(hand_words, list) else "",
                        "selectedHandIndex": ix,
                        "selectedCardIndexAtEntry": selected,
                        "selectedCardWordAtEntry": selected_word,
                        "executorHandInternalIndex": None,
                        "executorHandIndex": None,
                        "choiceForwardedToExecutor": False,
                        "executorMatchesEntry": None,
                        "readError": entry.get("handReadError", ""),
                    }
                    rows.append(row)
                    recent_leave = {"seq": event.get("seq"), "threadId": event.get("threadId"),
                                    "side": event.get("side"), "selected": ix,
                                    "row": row}
                continue
            if event.get("kind") == "demo_executor" and event.get("phase") == "enter" and (
                    event.get("function") == "normal_placement"):
                if (recent_leave is not None and event.get("seq") ==
                    recent_leave["seq"] + 1 and
                    event.get("threadId") == recent_leave["threadId"] and
                    event.get("side") == recent_leave["side"] and
                    event.get("handIndex") == recent_leave["selected"]):
                    row = recent_leave["row"]
                    row["executorSeq"] = event["seq"]
                    row["executorHandIndex"] = event.get("handIndex")
                    row["executorHandInternalIndex"] = event.get("handInternalIndex")
                    row["choiceForwardedToExecutor"] = True
                    expected = row["selectedCardIndexAtEntry"]
                    actual = event.get("handInternalIndex")
                    if expected is not None and actual is not None:
                        row["executorMatchesEntry"] = expected == actual
                recent_leave = None
            elif (recent_leave is not None and isinstance(event.get("seq"), int) and
                  event["seq"] > recent_leave["seq"] + 1):
                recent_leave = None
    summary = {
        "source": path.name,
        "scenes": duel,
        "selectors": len(rows),
        "byFunction": dict(Counter(r["selector"] for r in rows)),
        "handSnapshots": sum(isinstance(r["handCount"], int) for r in rows),
        "identifiedChoices": sum(r["selectedCardIndexAtEntry"] is not None for r in rows),
        "forwardedToExecutor": sum(r["choiceForwardedToExecutor"] for r in rows),
        "matchedToExecutor": sum(r["executorMatchesEntry"] is True for r in rows),
        "mismatchedToExecutor": sum(r["executorMatchesEntry"] is False for r in rows),
        "entryReadErrors": sum(bool(r["readError"]) for r in rows),
        "unterminatedSelectors": len(pending),
        "note": "Hand snapshots are cards present, not necessarily legal or scored alternatives.",
    }
    return rows, summary


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("trace", type=Path, help="JSONL with --trace-ai and selector snapshots")
    args = parser.parse_args()
    trace = args.trace.resolve(strict=True)
    rows, summary = scan_capture(trace)
    prefix = trace.with_suffix("")
    csv_path = prefix.with_name(prefix.name + "-selector-choices.csv")
    json_path = prefix.with_name(prefix.name + "-selector-summary.json")
    with csv_path.open("w", encoding="utf-8", newline="") as output:
        writer = csv.DictWriter(output, fieldnames=FIELDNAMES)
        writer.writeheader()
        writer.writerows(rows)
    json_path.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n",
                         encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    print(f"CSV: {csv_path}")
    print(f"JSON: {json_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
