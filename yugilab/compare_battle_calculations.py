"""Compare caller-paired native 0x31 and executed C# battle calculations."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from .battle_actor_projection import project_battle_actors
from .effect_report import _opcode
from .state_interactions import analyze_capture

Pair = tuple[int, int]
IDENTITY_FIELDS = ("side", "slot", "internalIndex", "defense", "faceDown")
ATTRIBUTE_FIELDS = ("attack", "defenseValue")
CALCULATION_COMMANDS = ("DeclareDirectAttackCommand", "DeclareTargetedAttackCommand",
                        "ResolveAttackResponseCommand", "ResolveRevealedBattleCommand")


def _int(value, low, high):
    return type(value) is int and low <= value <= high


def _actor(actor: dict) -> dict:
    target = actor["target"]
    occurrence = target["occurrence"]["value"]
    if (not _int(target["side"], 0, 1) or target["zone"] != 2
            or not _int(occurrence, 1, 2**63-1)
            or not _int(actor["slot"], 0, 4)
            or not _int(actor["internalIndex"], 1, 4095)
            or type(actor["defense"]) is not bool or type(actor["faceDown"]) is not bool
            or any(not _int(actor[key], 0, 2**31-1) for key in ATTRIBUTE_FIELDS)):
        raise ValueError("Invalid C# battle actor")
    return dict(side=target["side"], slot=actor["slot"], internalIndex=actor["internalIndex"],
                defense=actor["defense"], faceDown=actor["faceDown"],
                attack=actor["attack"], defenseValue=actor["defenseValue"],
                occurrence=occurrence)


def _csharp_calculations(document: dict) -> dict[int, dict]:
    if (document.get("schema") != "yugire-action-projection-v1"
            or type(document.get("complete")) is not bool):
        raise ValueError("Expected a yugire-action-projection-v1 document")
    rows = document["rows"]
    if not isinstance(rows, list) or (type(document.get("rowCount")) is not int
                                     or document["rowCount"] != len(rows)):
        raise ValueError("C# trace rowCount required and must match rows")
    result = {}
    for seq, row in enumerate(rows, 1):
        if type(row.get("sequence")) is not int or row["sequence"] != seq:
            raise ValueError("C# trace sequence gap")
        calc = row.get("battleCalculation")
        if calc is None:
            continue
        if row.get("outcome") != "Applied" or row.get("command") not in CALCULATION_COMMANDS:
            raise ValueError("Unexpected C# battle calculation command/outcome")
        a = _actor(calc["attacker"])
        b = None if calc["defender"] is None else _actor(calc["defender"])
        if a["faceDown"] or a["defense"] or (b is not None and b["side"] == a["side"]):
            raise ValueError("Invalid C# combatant poses or sides")
        if b is None:
            if (calc["difference"] is not None
                    or not _int(calc["requestedDirectDamage"], 0, 65535)
                    or calc["requestedDirectDamage"] != min(a["attack"], 65535)):
                raise ValueError("Inconsistent C# direct calculation")
        elif (calc["requestedDirectDamage"] is not None
              or type(calc["difference"]) is not int
              or calc["difference"] != a["attack"] - b["defenseValue" if b["defense"] else "attack"]):
            raise ValueError("Inconsistent C# targeted calculation")
        if (row["command"] == "DeclareDirectAttackCommand" and b is not None
                or row["command"] in ("DeclareTargetedAttackCommand", "ResolveRevealedBattleCommand") and b is None):
            raise ValueError("C# command does not match battle calculation")
        result[seq] = dict(direct=b is None, attacker=a, target=b)
    return result


def _native_actor(actor: dict) -> dict:
    raw = actor["rawBlock"]
    if any(not _int(raw[key], 0, 2**31-1) for key in ("calculatedAttack", "calculatedDefense")):
        raise ValueError("Invalid native calculated ATK/DEF")
    return dict(side=actor["side"], slot=actor["slot"], internalIndex=raw["cardIndexWord"],
                defense=actor["defense"], faceDown=actor["faceDown"],
                attack=raw["calculatedAttack"], defenseValue=raw["calculatedDefense"],
                occurrenceWord=actor["occurrenceWord"])


def _native_calculations(capture: Path, wanted: set[int]) -> tuple[dict, dict]:
    entries = {}
    counts = dict(samples=0, readErrors=0, missing=0)

    def observe(duel, event, sides):
        seq = event["seq"]
        raw = event.get("battleAttributesObservation")
        error = event.get("battleAttributesReadError")
        if _opcode(event) != 0x31:
            if raw is not None or error is not None:
                raise ValueError("Battle observation outside native 0x31")
            if seq in wanted:
                entries[seq] = (duel, None, "not_battle_command")
            return
        if raw is not None and error is not None:
            raise ValueError("Native battle sample has both observation and error")
        if error is not None:
            counts["readErrors"] += 1
            projection, reason = None, "battle_read_error"
        elif raw is None:
            counts["missing"] += 1
            projection, reason = None, "missing_battle_sample"
        else:
            counts["samples"] += 1
            projection = project_battle_actors(raw, sides)
            a, b = projection["actors"]
            # Validate all present correlated blocks, even outside the selected pairs.
            for actor in (a, b):
                if actor["status"] == "field_correlated":
                    _native_actor(actor)
            if a["status"] != "field_correlated" or b["status"] != (
                    "direct_target_absent" if projection["direct"] else "field_correlated"):
                reason = "uncorrelated_actors"
            elif a["side"] == b["side"]:
                reason = "same_side_actors"
            else:
                reason = None
        if seq in wanted:
            entries[seq] = (duel, projection, reason)

    source = analyze_capture(capture, on_entry=observe)
    header = footer = None
    with capture.open(encoding="utf-8") as stream:
        for line in stream:
            if not line.strip():
                continue
            event = json.loads(line)
            if event.get("kind") == "metadata":
                header = event
            elif event.get("kind") == "capture_summary":
                footer = event
    if header.get("battleAttributesTrace", {}).get("enabled") is not True:
        raise ValueError("Native capture requires --battle-attributes")
    expected = footer.get("battleAttributesTrace", {})
    if any(type(expected.get(key)) is not int or expected[key] != counts[key]
           for key in ("samples", "readErrors")):
        raise ValueError("Native battle sample counts disagree with footer")
    return entries, dict(sha256=source["sha256"], **counts)


def _differences(native: dict, csharp: dict) -> list[dict]:
    if native["direct"] != csharp["direct"]:
        return [dict(role="battle", field="direct", native=native["direct"], csharp=csharp["direct"])]
    differences = []
    for actor in native["actors"]:
        role = actor["role"]
        if role == "target" and native["direct"]:
            continue
        observed = _native_actor(actor)
        actual = csharp[role]
        fields = [key for key in IDENTITY_FIELDS if observed[key] != actual[key]]
        # Different actors/poses are themselves differences; never compare
        # numerical attributes of a different actor as if it were the same.
        if not fields:
            fields = [key for key in ATTRIBUTE_FIELDS if observed[key] != actual[key]]
        differences.extend(dict(role=role, side=observed["side"], slot=observed["slot"],
                                internalIndex=observed["internalIndex"], field=key,
                                native=observed[key], csharp=actual[key])
                           for key in fields)
    return differences


def compare(capture: Path, document: dict, pairs: list[Pair]) -> dict:
    if (not pairs or any(not _int(n, 1, 2**63-1) or not _int(c, 1, 2**63-1) for n, c in pairs)
            or any(n <= pn or c <= pc for (pn, pc), (n, c) in zip(pairs, pairs[1:]))):
        raise ValueError("Native and C# sequences must increase strictly")
    csharp = _csharp_calculations(document)
    native, source = _native_calculations(capture, {n for n, _ in pairs})
    if len({duel for duel, _, _ in native.values()}) > 1:
        raise ValueError("Paired observations span more than one native duel")
    gaps, results = [], []
    prefix = 0
    first = None
    for n, c in pairs:
        reference = dict(nativeSeq=n, csharpSequence=c)
        _, projection, reason = native.get(n, (None, None, "missing_native_entry"))
        if reason is not None or c not in csharp:
            gap = dict(**reference, nativeReason=reason,
                       missingCsharpCalculation=c not in csharp)
            if projection is not None:
                gap["nativeActorStatuses"] = [actor["status"] for actor in projection["actors"]]
            gaps.append(gap)
            continue
        differences = _differences(projection, csharp[c])
        results.append(dict(**reference, differences=differences))
        if differences and first is None:
            first = dict(**reference, afterGap=bool(gaps), fields=differences)
        if not gaps and first is None:
            prefix += 1
    if first is not None and not first["afterGap"]:
        verdict = "mismatch"
    elif gaps or not document["complete"]:
        verdict = "inconclusive"
    else:
        verdict = "projected_match"
    return dict(schema="yugire-battle-calculation-comparison-v1", verdict=verdict,
                nativeCaptureSha256=source["sha256"], nativeBattleSamples=source["samples"],
                nativeBattleReadErrors=source["readErrors"], nativeMissingBattleSamples=source["missing"],
                csharpCalculationCount=len(csharp), csharpComplete=document["complete"],
                pairedBy="caller", requestedPairs=len(pairs), matchedPrefix=prefix,
                gaps=gaps, firstObservedDifference=first, comparedPairs=results,
                interpretationLimit="Only supplied pairs and projected actor attributes; "
                "no independent occurrence mapping, damage comparison or full-duel equivalence.")


def _pair(raw: str) -> Pair:
    try:
        n, c = (int(value) for value in raw.split(":"))
        if n > 0 and c > 0:
            return n, c
    except ValueError:
        pass
    raise argparse.ArgumentTypeError("Use NATIVE_SEQ:CSHARP_SEQUENCE with positive integers")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("native_capture", type=Path)
    parser.add_argument("csharp_trace", type=Path)
    parser.add_argument("--pair", action="append", required=True, type=_pair,
                        metavar="NATIVE_SEQ:CSHARP_SEQUENCE")
    args = parser.parse_args()
    try:
        payload = args.csharp_trace.read_bytes()
        report = compare(args.native_capture, json.loads(payload), args.pair)
        report["csharpTraceSha256"] = hashlib.sha256(payload).hexdigest()
    except (OSError, ValueError, KeyError, TypeError, AttributeError, IndexError) as error:
        parser.exit(2, f"Invalid battle calculation comparison: {error}\n")
    print(json.dumps(report, indent=2))
    return {"projected_match": 0, "mismatch": 1, "inconclusive": 3}[report["verdict"]]


if __name__ == "__main__":
    raise SystemExit(main())
