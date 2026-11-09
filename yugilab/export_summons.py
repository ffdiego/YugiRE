"""Export native-observed normal summon transitions as C# differential fixtures.

Usage:
  python -m yugilab.export_summons parallel-xxx.zip \
      --output-dir scratch/differential-fixtures

Requires a COMPLETE, seeded YugiLab capture with new summon_transition
events. Older captures do not contain reliable before/after field+hand
observations and MUST NOT be automatically converted into parity fixtures.

All output stays local. No game process is opened and no data is mutated.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys

from .compare import _sources, _stream_duels

SCHEMA = "yugire.partial-normal-summon.v1"
METHOD = "executor_enter_ce_producer_update_leave"


def _int(value: object, label: str, low: int = 0, high: int = 0xFFFFFFFF) -> int:
    if type(value) is not int or not low <= value <= high:
        raise ValueError(f"{label} must be an integer in [{low}, {high}]")
    return value


def _indices(snapshot: dict, key: str, *, size: int | None = None) -> list[int]:
    data = snapshot.get(key)
    if not isinstance(data, list) or (size is not None and len(data) != size):
        raise ValueError(f"{key} must be a list" +
                         (f" of {size} slots" if size is not None else ""))
    return [_int(v, key, 0, 4095) for v in data]


def _snapshot(value: object, label: str, side: int) -> dict:
    if not isinstance(value, dict) or value.get("side") != side:
        raise ValueError(f"{label} has wrong/missing side")
    hand = _indices(value, "handIndices")
    if not 0 <= len(hand) <= 80 or any(index == 0 for index in hand):
        raise ValueError(f"{label}: invalid hand identities")
    words = value.get("handWords")
    if (not isinstance(words, list) or len(words) != len(hand) or
        any(type(v) is not int or v < 0 or v > 0xFFFFFFFF or
            (v & 0xFFF) != hand[i] for i, v in enumerate(words))):
        raise ValueError(f"{label}: handWords do not match card indices")
    _indices(value, "monsters", size=5)
    _indices(value, "opponentMonsters", size=5)
    _int(value.get("lifePoints"), f"{label}.lifePoints", 1, 65535)
    _int(value.get("playerFlags"), f"{label}.playerFlags")
    _int(value.get("duelPhaseRaw"), f"{label}.duelPhaseRaw", 0, 255)
    return value


def _validate_event_links(event: dict, event_by_seq: dict[int, dict]) -> None:
    """Confirm that the declaration points to actual executor and CE records."""
    enter_seq = _int(event.get("executorEnterSeq"), "executorEnterSeq", 1)
    ce_seq = _int(event.get("ceProducerSeq"), "ceProducerSeq", 1)
    end_seq = _int(event.get("seq"), "transition.seq", 1)
    if not enter_seq < ce_seq < end_seq:
        raise ValueError("Executor, CE and transition sequence ordering invalid")
    entry = event_by_seq.get(enter_seq)
    ce = event_by_seq.get(ce_seq)
    observed_before = event.get("before")
    if not isinstance(observed_before, dict):
        raise ValueError("Missing before snapshot on native transition")
    if entry is None or ce is None:
        raise ValueError("Referenced executor or CE event not in capture")
    if (entry.get("kind") != "demo_executor" or
        entry.get("function") != "normal_placement" or
        entry.get("phase") != "enter" or
        entry.get("duelRunning") is not True or
        entry.get("callId") != event.get("executorCallId") or
        entry.get("side") != event.get("side") or
        entry.get("handIndex") != event.get("handIndex") or
        entry.get("handInternalIndex") != event.get("sourceCardIndex") or
        entry.get("handCount") != len(observed_before.get("handIndices", []))):
        raise ValueError("Native executor entry does not support transition")
    if (ce.get("kind") != "command" or
        str(ce.get("opcodeMasked", "")).lower() != "0x00ce" or
        str(ce.get("caller", "")).lower() != "0x5abeab"):
        raise ValueError("Referenced native CE command not found")
    native = ce.get("ceNativeState")
    args = ce.get("args")
    if (not isinstance(native, dict) or
        native.get("context") != event.get("ceContext") or
        native.get("stage") != event.get("ceStage") or
        type(native.get("source")) is not int or
        (native["source"] & 0xFFF) != event.get("sourceCardIndex") or
        not isinstance(args, list) or len(args) < 2 or
        type(args[0]) is not int or type(args[1]) is not int or
        (args[0] & 0x7FFF) != 0x00CE or
        (args[1] & 0xFFFF) != event.get("ceResourceId")):
        raise ValueError("Native CE context/resource/source contradict transition")
    thread_id = _int(event.get("threadId"), "threadId", 1)
    if entry.get("threadId") != thread_id or ce.get("threadId") != thread_id:
        raise ValueError("Executor, CE and transition must use same game thread")


def fixture_from_transition(event: dict, global_index: int) -> dict:
    """Validate a source observation independently, then project known facts.

    A partial observation does not establish the absence of intermediate
    events, card effect behavior, actual card properties or native legality.
    """
    if event.get("kind") != "summon_transition" or event.get("phase") != "confirmed":
        raise ValueError("Expected confirmed summon_transition event")
    if event.get("evidenceMethod") != METHOD:
        raise ValueError("Unknown/unverified summon observation method")
    seq = _int(event.get("seq"), "seq", 1)
    call_id = _int(event.get("executorCallId"), "executorCallId", 1)
    ce_seq = _int(event.get("ceProducerSeq"), "ceProducerSeq", 1)
    enter_seq = _int(event.get("executorEnterSeq"), "executorEnterSeq", 1)
    if not enter_seq < ce_seq or ce_seq >= seq:
        raise ValueError("CE producer event must precede confirmed state transition")
    side = _int(event.get("side"), "side", 0, 1)
    hand_index = _int(event.get("handIndex"), "handIndex", 0, 79)
    slot = _int(event.get("fieldSlot"), "fieldSlot", 0, 4)
    source = _int(event.get("sourceCardIndex"), "sourceCardIndex", 1, 4095)
    resource_id = _int(event.get("ceResourceId"), "ceResourceId", 0, 65535)
    context = _int(event.get("ceContext"), "ceContext")
    stage = _int(event.get("ceStage"), "ceStage", 0, 65535)
    if ((context & 1) != side or ((context >> 6) & 255) != hand_index or
        ((context >> 1) & 31) != slot or (context & 0x4000) == 0 or
        (context & 0x8000) != 0 or (context & 0x02000000) != 0 or
        stage != 37):
        raise ValueError("CE descriptor is not an eligible ordinary face-up summon")
    before = _snapshot(event.get("before"), "before", side)
    after = _snapshot(event.get("after"), "after", side)
    hand = _indices(before, "handIndices")
    next_hand = _indices(after, "handIndices")
    if hand_index >= len(hand) or hand[hand_index] != source:
        raise ValueError("CE source card not at observed hand index")
    expected = hand[:hand_index] + hand[hand_index+1:]
    if next_hand != expected:
        raise ValueError("After-hand differs beyond single selected card removal")
    initial_monsters = _indices(before, "monsters", size=5)
    final_monsters = _indices(after, "monsters", size=5)
    if initial_monsters[slot] != 0:
        raise ValueError("Destination was not empty before summon")
    new_monsters = initial_monsters.copy()
    new_monsters[slot] = source
    if final_monsters != new_monsters:
        raise ValueError("Other field changes or wrong summoned card")
    if (_indices(after, "opponentMonsters", size=5) !=
            _indices(before, "opponentMonsters", size=5) or
        after["lifePoints"] != before["lifePoints"] or
        (before["playerFlags"] & 0x20000) != 0 or
        (after["playerFlags"] & 0x20000) == 0):
        raise ValueError("LP, opponent field, or normal summon flag inconsistent")
    _int(event.get("ticksAfterExecutor"), "ticksAfterExecutor", 1, 1500)

    nullable = lambda values: [value if value else None for value in values]
    case_id = f"auto-g{global_index:06d}-seq{seq:07d}"
    return {
        "schema": SCHEMA,
        "id": case_id,
        "source": f"YugiLab completed seed capture: global duel {global_index}, event {seq}",
        "evidence": ("Passive, game-thread executor entry + CE native producer "
                     "+ later duel-update snapshot; not an atomic transaction."),
        "before": {
            "activeSide": side,
            "activeLifePoints": before["lifePoints"],
            "activeHandIndices": hand,
            "knownActiveMonsters": nullable(initial_monsters),
            "knownOpponentMonsters": nullable(before["opponentMonsters"])
        },
        "action": {
            "kind": "normalSummon", "side": side, "handIndex": hand_index,
            "fieldSlot": slot, "sourceCardIndex": source,
            "resourceId": resource_id, "ceContext": context,
            "ceStage": stage, "nativeLevelClass": None,
            "nativeEligibility": None
        },
        "after": {
            "activeHandCount": len(next_hand),
            "activeHandIndices": next_hand,
            "activeLifePoints": after["lifePoints"],
            "knownActiveMonsters": nullable(final_monsters),
            "fieldSlot": slot,
            "cardIndex": source
        },
        "provenance": {
            "kind": "native_observation", "globalDuelIndex": global_index,
            "transitionSeq": seq, "executorEnterSeq": enter_seq,
            "ceProducerSeq": ce_seq, "executorCallId": call_id,
            "ticksAfterExecutor": event["ticksAfterExecutor"],
            "method": METHOD
        },
        "limitations": [
            "Snapshots are at different native execution boundaries, not atomic before/after.",
            "No full native effects, chain resolution, deck, graveyard, or hidden game flags are replayed.",
            "Selected card is a level-four ordinary-monster SURROGATE in the C# pilot; "
            "its original properties and full summon legality are not validated.",
            "Only known hand indices, monster slots and active LP are compared.",
            "Fixture generated only when the observed transition matched a single-card move "
            "and the native normal-summon-used flag changed."
        ]
    }


def collect_fixtures(path: Path) -> tuple[list[dict], dict]:
    sources = _sources(path)
    cases: list[dict] = []
    total_events = 0
    excluded = []
    seen = set()
    for global_index, events in _stream_duels(sources):
        event_by_seq = {event.get("seq"): event for event in events}
        for event in events:
            if event.get("kind") != "summon_transition":
                continue
            total_events += 1
            try:
                _validate_event_links(event, event_by_seq)
                fixture = fixture_from_transition(event, global_index)
            except ValueError as exc:
                excluded.append({"globalDuelIndex": global_index,
                                 "seq": event.get("seq"), "reason": str(exc)})
                continue
            if fixture["id"] in seen:
                raise ValueError(f"Duplicate fixture ID {fixture['id']}")
            seen.add(fixture["id"])
            cases.append(fixture)
    cases.sort(key=lambda f: (f["provenance"]["globalDuelIndex"],
                              f["provenance"]["transitionSeq"]))
    return cases, {"schema": "yugire.fixture-export-summary.v1",
                   "input": str(path), "completeSeedCapture": True,
                   "duels": sum(cap.count for cap in sources),
                   "observedTransitions": total_events,
                   "exportedFixtures": len(cases), "excluded": excluded,
                   "warning": "Fixtures are projections of observed actions, not native game savestates"}


def export(path: Path, output_dir: Path, *, allow_empty: bool = False) -> dict:
    fixtures, summary = collect_fixtures(path)
    if not fixtures and not allow_empty:
        raise ValueError("No verified summon_transition events found. "
                         "Old YugiLab captures cannot supply before/after "
                         "snapshots. Re-run with the updated passive agent.")
    output_dir.mkdir(parents=True, exist_ok=True)
    # Validate all destination names before writing anything; old captures
    # never authorize modifying hand-authored fixtures in the repository.
    serialized = [(output_dir / (f["id"] + ".json"),
                   json.dumps(f, indent=2, ensure_ascii=False) + "\n")
                  for f in fixtures]
    for target, data in serialized:
        if target.exists() and target.read_text(encoding="utf-8") != data:
            raise ValueError(f"Refusing to overwrite different fixture: {target}")
    for target, data in serialized:
        if not target.exists():
            with target.open("x", encoding="utf-8") as stream:
                stream.write(data)
    report_path = output_dir / "export-summary.json"
    report = json.dumps(summary, indent=2, ensure_ascii=False) + "\n"
    if report_path.exists() and report_path.read_text(encoding="utf-8") != report:
        raise ValueError(f"Refusing to overwrite different export summary: {report_path}")
    if not report_path.exists():
        with report_path.open("x", encoding="utf-8") as stream:
            stream.write(report)
    return summary


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture", type=Path, help="Completed seeded YugiLab ZIP, directory or JSONL")
    parser.add_argument("--output-dir", type=Path, required=True,
                        help="Local ignored scratch directory for auto fixtures")
    parser.add_argument("--allow-empty", action="store_true",
                        help="Allow diagnostic run with no new snapshots")
    args = parser.parse_args(argv)
    try:
        report = export(args.capture, args.output_dir, allow_empty=args.allow_empty)
    except (OSError, ValueError, UnicodeError) as exc:
        print(f"[yugilab.export_summons] ERROR: {exc}", file=sys.stderr)
        return 2
    print(f"[yugilab.export_summons] {report['duels']} complete duels, "
          f"{report['observedTransitions']} observed transitions, "
          f"{report['exportedFixtures']} exported fixtures, "
          f"{len(report['excluded'])} rejected; output={args.output_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
