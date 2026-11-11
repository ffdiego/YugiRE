#!/usr/bin/env python3
"""Audit recorded passive AI selector traces without executing Joey.

Produces temporal correlations, not a replay of the native decision algorithm.
The original JSONL is opened read-only. Stats/events may be hypothetical.
"""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import hashlib
import json
from pathlib import Path

from decode_ai_combat_event import decode_hex as decode_event
from decode_ai_combat_stats import decode_hex as decode_stats

SELECTORS = {"select_normal", "select_priority"}


def audit(records: list[dict]) -> dict:
    if not records or records[0].get("kind") != "metadata":
        raise ValueError("Missing metadata header")
    metadata, events = records[0], records[1:]
    if metadata.get("overwritten") != 0:
        raise ValueError("Overwritten events cannot establish complete windows")
    if metadata.get("seen") != len(events) or metadata.get("retained") != len(events):
        raise ValueError("Metadata counts disagree with retained events")
    if [event.get("seq") for event in events] != list(range(1, len(events) + 1)):
        raise ValueError("Noncontiguous event sequence")
    stacks: dict[int, list[dict]] = defaultdict(list)
    calls, ce_links, failures, orphan_outputs = [], [], [], []
    last_valid: dict[tuple[int, int], dict] = {}
    for event in events:
        if event.get("kind") == "ai_probe":
            thread = event["threadId"]
            function = event.get("function")
            if function in SELECTORS:
                if event.get("phase") == "enter":
                    if event.get("handReadError"):
                        failures.append({"seq": event["seq"], "reason": "handReadError"})
                    hand = event.get("handIndices")
                    words = event.get("handWords")
                    if hand is not None and words is not None:
                        if len(hand) != event.get("handCount") or hand != [word & 0xFFF for word in words]:
                            raise ValueError("Inconsistent hand snapshot at seq " + str(event["seq"]))
                    stacks[thread].append({"entry": event, "stats": [], "combatEvents": []})
                elif event.get("phase") == "leave":
                    stack = stacks[thread]
                    if not stack or stack[-1]["entry"]["callId"] != event["callId"]:
                        raise ValueError("Unpaired/cross-thread selector leave at seq " + str(event["seq"]))
                    call = stack.pop()
                    entry = call["entry"]
                    if entry["function"] != function or entry["side"] != event["side"]:
                        raise ValueError("Selector entry/leave identity mismatch")
                    index = event["selectedHandIndex"]
                    hand = entry.get("handIndices")
                    selected = None
                    if index >= 0 and hand is not None:
                        if index >= len(hand):
                            raise ValueError("Selector index exceeds captured hand")
                        selected = hand[index]
                        if event.get("selectedCardIndexAtEntry") != selected:
                            raise ValueError("Selected card disagrees with entry hand")
                    summary = {
                        "enterSeq": entry["seq"], "leaveSeq": event["seq"],
                        "callId": event["callId"], "threadId": thread,
                        "side": entry["side"], "function": function,
                        "caller": entry.get("caller"), "pass": entry.get("pass"),
                        "handIndices": hand, "selectedHandIndex": index,
                        "selectedCardIndex": selected,
                        "stats": call["stats"], "combatEvents": call["combatEvents"],
                    }
                    calls.append(summary)
                    if selected is not None:
                        last_valid[thread, entry["side"]] = summary
            elif event.get("withinSelector"):
                stack = stacks[thread]
                if not stack:
                    orphan_outputs.append(event["seq"])
                    continue
                if event.get("readError"):
                    failures.append({"seq": event["seq"], "reason": "output readError"})
                    continue
                if function in {"computed_stats", "combat_event"}:
                    decoded = (decode_stats if function == "computed_stats" else decode_event)(event["outputHex"]).to_dict()
                    stack[-1]["stats" if function == "computed_stats" else "combatEvents"].append({
                        "seq": event["seq"], "side": event.get("side"),
                        "rawArgs": event.get("rawArgs"), "decoded": decoded,
                    })
        elif event.get("kind") == "command" and event.get("args"):
            if event["args"][0] & 0x7FFF != 0xCE:
                continue
            state = event.get("ceNativeState")
            if state is None:
                continue
            side = (event["args"][0] >> 15) & 1
            prior = last_valid.get((event["threadId"], side))
            ce_links.append({
                "commandSeq": event["seq"], "side": side,
                "source": state.get("source"),
                "selectionSeq": None if prior is None else prior["leaveSeq"],
                "selectedCardIndex": None if prior is None else prior["selectedCardIndex"],
                "sourceMatchesLastValid": None if prior is None else state.get("source") == prior["selectedCardIndex"],
                "scope": "temporal_same_thread_side_not_decision_reproduction",
            })
    if any(stacks.values()):
        raise ValueError("Unclosed selector window")
    if orphan_outputs:
        raise ValueError("Output outside recorded selector window: " + str(orphan_outputs))
    normal = [call for call in calls if call["function"] == "select_normal"]
    priority = [call for call in calls if call["function"] == "select_priority"]
    return {
        "scope": "read-only historical capture audit; no decision reproduced",
        "metadataExeSha256": metadata.get("exeSha256"),
        "eventCount": len(events), "countsByKind": dict(Counter(event.get("kind") for event in events)),
        "normalCount": len(normal), "normalNegative": sum(c["selectedHandIndex"] < 0 for c in normal),
        "normalPasses": dict(Counter(str(c["pass"]) for c in normal)),
        "priorityCount": len(priority), "readFailures": failures,
        "calls": calls, "ceTemporalLinks": ce_links,
    }


def audit_file(path: Path, expected_sha256: str | None = None) -> dict:
    raw = path.read_bytes()
    digest = hashlib.sha256(raw).hexdigest()
    if expected_sha256 is not None and digest.lower() != expected_sha256.lower():
        raise ValueError("Capture SHA256 mismatch")
    records = [json.loads(line) for line in raw.decode("utf-8-sig").splitlines() if line.strip()]
    return {"captureSha256": digest, **audit(records)}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture", type=Path)
    parser.add_argument("--expected-sha256")
    arguments = parser.parse_args()
    try:
        print(json.dumps(audit_file(arguments.capture, arguments.expected_sha256), indent=2))
    except (OSError, ValueError, KeyError, TypeError) as error:
        parser.error(str(error))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
