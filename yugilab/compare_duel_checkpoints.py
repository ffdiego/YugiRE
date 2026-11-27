"""Compare explicitly paired script boundaries and native command-entry observations."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from .state_interactions import analyze_capture

Pair = tuple[int, int | None]
FIELDS = ("lifePoints", "handIndices", "deckCount", "graveyardCount")


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


def _observed_fields(side: dict, native: bool) -> dict:
    if native:
        return {key: side[key] for key in FIELDS}
    return {
        "lifePoints": side["life"],
        "handIndices": [card["internalIndex"] for card in side["hand"]],
        "deckCount": len(side["deck"]),
        "graveyardCount": len(side["graveyard"]),
    }


def compare(capture: Path, document: dict, pairs: list[Pair]) -> dict:
    if document.get("schema") != "yugire-duel-checkpoints-v1" or type(document.get("complete")) is not bool:
        raise ValueError("Expected a yugire-duel-checkpoints-v1 document")
    if not pairs or pairs[0][1] is not None:
        raise ValueError("The first pair must identify the initial C# checkpoint")
    for (previous_seq, previous_line), (seq, line) in zip(pairs, pairs[1:]):
        if seq <= previous_seq or line is None or line <= (previous_line or 0):
            raise ValueError("Native sequences and script lines must increase strictly")

    checkpoints = {}
    for checkpoint in document["checkpoints"]:
        line = checkpoint["boundary"]["line"]
        if (line is not None and (type(line) is not int or line < 1)) or line in checkpoints:
            raise ValueError("Invalid or repeated script line in C# checkpoints")
        checkpoints[line] = checkpoint
    if None not in checkpoints:
        raise ValueError("Missing initial C# checkpoint")

    wanted = {seq for seq, _ in pairs}
    samples = {}

    def observe(duel: int, seq: int, sides: list[dict]) -> None:
        if seq in wanted:
            samples[seq] = (duel, [_observed_fields(side, native=True) for side in sides])

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
            sandbox = _observed_fields(sides[side], native=False)
            for field, native_value in native_sides[side].items():
                if native_value != sandbox[field]:
                    differences.append({
                        "side": side,
                        "field": field,
                        "native": native_value,
                        "csharp": sandbox[field],
                    })
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
    parser.add_argument("--pair", action="append", required=True, type=_pair, metavar="N:LINE")
    args = parser.parse_args()
    try:
        document = json.loads(args.checkpoints.read_text(encoding="utf-8"))
        report = compare(args.native_capture, document, args.pair)
    except (OSError, ValueError, KeyError, TypeError, AttributeError) as error:
        parser.exit(2, f"Invalid checkpoint comparison: {error}\n")
    print(json.dumps(report, indent=2))
    return {"projected_match": 0, "mismatch": 1, "inconclusive": 3}[report["verdict"]]


if __name__ == "__main__":
    raise SystemExit(main())
