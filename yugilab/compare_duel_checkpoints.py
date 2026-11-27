"""Compare explicitly paired script boundaries and native command-entry observations."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from .state_interactions import analyze_capture

Pair = tuple[int, int | None]
COUNTER_FIELDS = ("lifePoints", "handIndices", "deckCount", "graveyardCount")
SLOT_FIELDS = ("monsters", "spells")
FIELDS = (*COUNTER_FIELDS, *SLOT_FIELDS, "fieldSpell")


def _pair(raw: str) -> Pair:
    try:
        seq_text, line_text = raw.split(":")
        seq = int(seq_text)
        line = None if line_text == "initial" else int(line_text)
        if seq > 0 and (line is None or line > 0):
            return seq, line
    except ValueError:
        pass
    raise argparse.ArgumentTypeError("Use N:initial or N:LINE with positive integers")


def _native_slot(slot: dict, *, monster: bool = False) -> dict | None:
    index = slot["cardIndex"]
    if index == 0:
        return None
    # The native field record stores defense in bit 0 and face-up in bit 1.
    position = slot["positionWord"]
    result = {"internalIndex": index, "faceDown": not bool(position & 2)}
    if monster:
        result["defense"] = bool(position & 1)
    return result


def _csharp_slot(slot: dict | None, *, monster: bool = False) -> dict | None:
    if slot is None:
        return None
    result = {"internalIndex": slot["card"]["internalIndex"], "faceDown": slot["faceDown"]}
    if monster:
        result["defense"] = slot["defense"]
    return result


def _native_fields(side: dict) -> dict:
    slots = side["fieldSlots"]
    return {
        **{key: side[key] for key in COUNTER_FIELDS},
        "monsters": [_native_slot(slot, monster=True) for slot in slots[:5]],
        "spells": [_native_slot(slot) for slot in slots[5:10]],
        "fieldSpell": _native_slot(slots[10]),
    }


def _csharp_fields(side: dict) -> dict:
    if len(side["monsters"]) != 5 or len(side["spells"]) != 5:
        raise ValueError("C# checkpoint needs five monster and five spell/trap slots per side")
    return {
        "lifePoints": side["life"],
        "handIndices": [card["internalIndex"] for card in side["hand"]],
        "deckCount": len(side["deck"]),
        "graveyardCount": len(side["graveyard"]),
        "monsters": [_csharp_slot(slot, monster=True) for slot in side["monsters"]],
        "spells": [_csharp_slot(slot) for slot in side["spells"]],
        "fieldSpell": _csharp_slot(side["fieldSpell"]),
    }


def _checkpoints(document: dict) -> dict:
    if document.get("schema") != "yugire-duel-checkpoints-v1" or type(document.get("complete")) is not bool:
        raise ValueError("Expected a yugire-duel-checkpoints-v1 document")
    checkpoints = {}
    for checkpoint in document["checkpoints"]:
        line = checkpoint["boundary"]["line"]
        if (line is not None and (type(line) is not int or line < 1)) or line in checkpoints:
            raise ValueError("Invalid or repeated script line in C# checkpoints")
        checkpoints[line] = checkpoint
    if None not in checkpoints:
        raise ValueError("Missing initial C# checkpoint")
    return checkpoints


def inventory(capture: Path, document: dict, window: tuple[int, int] | None = None) -> dict:
    """Expose pairing inputs in source order without choosing equivalent states."""
    checkpoints = _checkpoints(document)
    if window is not None and (window[0] < 1 or window[1] < window[0]):
        raise ValueError("Native window must have positive, increasing bounds")
    entries = []

    def observe(duel: int, event: dict, sides: list[dict] | None) -> None:
        seq = event["seq"]
        if window is not None and not window[0] <= seq <= window[1]:
            return
        entries.append(dict(nativeSeq=seq, duelIndex=duel, args=event.get("args"),
                            caller=event.get("caller"),
                            stateReadError=event.get("stateReadError"),
                            sides=None if sides is None else [_native_fields(side) for side in sides]))

    source = analyze_capture(capture, on_entry=observe)
    boundaries = []
    for line, checkpoint in sorted(checkpoints.items(), key=lambda item: item[0] or 0):
        sides = checkpoint["sides"]
        if len(sides) != 2:
            raise ValueError("C# checkpoint needs exactly two sides")
        boundaries.append(dict(scriptLine=line, boundary=checkpoint["boundary"],
                               turn=checkpoint.get("turn"), activePlayer=checkpoint.get("activePlayer"),
                               phase=checkpoint.get("phase"), mainPhase2=checkpoint.get("mainPhase2"),
                               sides=[_csharp_fields(side) for side in sides]))
    return dict(schema="yugire-duel-checkpoint-inventory-v1",
                nativeCaptureSha256=source["sha256"],
                nativeSamplePoint="command_producer_onEnter", nativeSamplesAreAtomic=False,
                projectedFields=FIELDS, csharpComplete=document["complete"],
                nativeWindow=window, nativeEntries=entries, csharpBoundaries=boundaries,
                interpretationLimit="Unpaired inputs. Matching state alone does not establish equivalent decisions, RNG or timing.")


def _window(raw: str) -> tuple[int, int]:
    try:
        first, last = (int(part) for part in raw.split(":"))
        if 1 <= first <= last:
            return first, last
    except ValueError:
        pass
    raise argparse.ArgumentTypeError("Use FIRST:LAST with positive, increasing bounds")


def compare(capture: Path, document: dict, pairs: list[Pair]) -> dict:
    if not pairs or pairs[0][1] is not None:
        raise ValueError("The first pair must identify the initial C# checkpoint")
    for (previous_seq, previous_line), (seq, line) in zip(pairs, pairs[1:]):
        if seq <= previous_seq or line is None or line <= (previous_line or 0):
            raise ValueError("Native sequences and script lines must increase strictly")

    checkpoints = _checkpoints(document)

    wanted = {seq for seq, _ in pairs}
    samples = {}

    def observe(duel: int, seq: int, sides: list[dict]) -> None:
        if seq in wanted:
            samples[seq] = (duel, [_native_fields(side) for side in sides])

    source = analyze_capture(capture, on_sample=observe)
    if len({duel for duel, _ in samples.values()}) > 1:
        raise ValueError("Paired observations span more than one native duel")

    gaps = []
    matched_prefix = 0
    first_difference = None
    for seq, line in pairs:
        reference = {"nativeSeq": seq, "scriptLine": line}
        if seq not in samples or line not in checkpoints:
            gaps.append({
                **reference,
                "missingNativeSample": seq not in samples,
                "missingScriptCheckpoint": line not in checkpoints,
            })
            continue
        checkpoint = checkpoints[line]
        sides = checkpoint["sides"]
        if len(sides) != 2:
            raise ValueError("C# checkpoint needs exactly two sides")
        _, native_sides = samples[seq]
        differences = []
        for side in range(2):
            sandbox = _csharp_fields(sides[side])
            for field, native_value in native_sides[side].items():
                csharp_value = sandbox[field]
                if native_value == csharp_value:
                    continue
                if field in SLOT_FIELDS:
                    for slot, (native_slot, csharp_slot) in enumerate(zip(native_value, csharp_value)):
                        if native_slot != csharp_slot:
                            differences.append(dict(side=side, field=field, slot=slot,
                                                    native=native_slot, csharp=csharp_slot))
                else:
                    differences.append(dict(side=side, field=field, native=native_value, csharp=csharp_value))
        if differences and first_difference is None:
            first_difference = {
                **reference,
                "lastExecutedSequence": checkpoint["boundary"]["lastExecutedSequence"],
                "afterGap": bool(gaps),
                "fields": differences,
            }
        if not gaps and first_difference is None:
            matched_prefix += 1

    incomplete = not document["complete"] or bool(gaps)
    if first_difference is not None and not first_difference["afterGap"]:
        verdict = "mismatch"
    elif incomplete:
        verdict = "inconclusive"
    else:
        verdict = "projected_match"

    return {
        "schema": "yugire-duel-checkpoint-comparison-v1",
        "verdict": verdict,
        "nativeCaptureSha256": source["sha256"],
        "nativeSamplePoint": "command_producer_onEnter",
        "nativeSamplesAreAtomic": False,
        "projectedFields": FIELDS,
        "csharpComplete": document["complete"],
        "pairedBy": "caller",
        "requestedPairs": len(pairs),
        "matchedPrefix": matched_prefix,
        "gaps": gaps,
        "firstObservedDifference": first_difference,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("native_capture", type=Path)
    parser.add_argument("checkpoints", type=Path)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--pair", action="append", type=_pair, metavar="N:LINE")
    mode.add_argument("--inventory", action="store_true", help="List unpaired native entries and C# boundaries")
    parser.add_argument("--native-window", type=_window, metavar="FIRST:LAST",
                        help="Restrict inventory to this inclusive native sequence range")
    args = parser.parse_args()
    if args.native_window is not None and not args.inventory:
        parser.error("--native-window requires --inventory")
    try:
        document = json.loads(args.checkpoints.read_text(encoding="utf-8"))
        report = (inventory(args.native_capture, document, args.native_window) if args.inventory
                  else compare(args.native_capture, document, args.pair))
    except (OSError, ValueError, KeyError, TypeError, AttributeError) as error:
        parser.exit(2, f"Invalid checkpoint comparison: {error}\n")
    print(json.dumps(report, indent=2))
    return 0 if args.inventory else {"projected_match": 0, "mismatch": 1, "inconclusive": 3}[report["verdict"]]


if __name__ == "__main__":
    raise SystemExit(main())
