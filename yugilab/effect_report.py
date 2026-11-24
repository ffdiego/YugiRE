"""Offline audit of native demo command traces near *candidate* effect IDs.

Conservative: CF and CE identify command-associated resource candidates, not
effect activation or causality. A subsequent LP command is only co-occurrence.
This tool never launches Joey/Frida and never modifies captures.
"""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import hashlib
import json
from pathlib import Path

# Audited P045 branch resource IDs. This is a question list, not native proof.
P045_IDS = {342, 343, 344, 345, 1007}


def _opcode(event: dict) -> int | None:
    if event.get("kind") != "command":
        return None
    args = event.get("args")
    if not isinstance(args, list) or not args or type(args[0]) is not int:
        return None
    return (args[0] & 0xFFFF) & 0x7FFF


def _candidate(event: dict, opcode: int) -> tuple[str, int] | None:
    args = event.get("args")
    if opcode == 0xCF and str(event.get("caller", "")).lower() == "0x57977f":
        rid = event.get("cfResourceIdCandidate")
        if type(rid) is not int and isinstance(args, list) and len(args) > 1:
            rid = args[1] & 0xFFFF
        return ("CF", rid) if type(rid) is int else None
    if opcode == 0xCE and str(event.get("caller", "")).lower() == "0x5abeab":
        rid = event.get("ceResourceIdLow16")
        if type(rid) is not int and isinstance(args, list) and len(args) > 1:
            rid = args[1] & 0xFFFF
        return ("CE", rid) if type(rid) is int else None
    return None



# Confirmed from bounded static P045 dispatch; full activation remains open.
P045_AMOUNTS = {342: 200, 343: 500, 344: 600, 345: 800, 1007: 300}


def link_fixed_damage_deltas(path: Path, ids: set[int] = P045_IDS) -> list[dict]:
    """Conservative, observed CF -> +3 LP command -> +1 LP snapshot signature.

    Does not infer entire card activation, targeting rules, or native parity.
    Only preserves cases with explicit source ID, expected fixed damage,
    thread continuity, and independently observed two-sided LP readings.
    """
    cases: list[dict] = []
    pending_cf: dict | None = None
    pending_damage: dict | None = None
    duel_index = 0
    with path.open(encoding="utf-8") as source:
        for raw in source:
            if not raw.strip():
                continue
            event = json.loads(raw)
            if event.get("kind") != "command":
                continue
            seq = event.get("seq")
            opcode = _opcode(event)
            if type(seq) is not int or opcode is None:
                continue
            if pending_damage is not None and seq > pending_damage["damageSeq"]:
                # The native capture currently snapshots post-command LP on
                # the *next* command producer. Do not invent later state.
                observed = event.get("lifePointsObserved")
                after = observed.get("lifePoints") if isinstance(observed, dict) else None
                if (seq == pending_damage["damageSeq"] + 1 and
                    event.get("threadId") == pending_damage["threadId"] and
                    isinstance(after, list) and len(after) == 2 and
                    all(type(value) is int for value in after)):
                    before = pending_damage["lifeBefore"]
                    loss = [before[i] - after[i] for i in (0, 1)]
                    target = pending_damage["targetLifeIndex"]
                    expected = [0, 0]
                    expected[target] = pending_damage["damageAmount"]
                    cases.append({
                        **pending_damage, "afterSeq": seq,
                        "lifeAfter": after, "observedLoss": loss,
                        "exactLpDelta": loss == expected,
                        "oppositeSourceSide": target != pending_damage["sourceSideBit15"],
                        "evidenceScope": "Native command + subsequent LP observation; "
                                         "not full card activation or legal target proof"
                    })
                pending_damage = None

            candidate = _candidate(event, opcode)
            if candidate is not None and candidate[0] == "CF" and candidate[1] in ids:
                pending_cf = {
                    "duelIndex": duel_index,
                    "cfSeq": seq,
                    "sourceResourceId": candidate[1],
                    "sourceSideBit15": (event["args"][0] >> 15) & 1,
                    "threadId": event.get("threadId"),
                }

            if (opcode == 0x43 and event.get("caller", "").lower() == "0x578513"):
                if (pending_cf is not None and seq == pending_cf["cfSeq"] + 3 and
                    event.get("threadId") == pending_cf["threadId"] and
                    event["args"][1] == P045_AMOUNTS.get(pending_cf["sourceResourceId"])):
                    observation = event.get("lifePointsObserved")
                    before = observation.get("lifePoints") if isinstance(observation, dict) else None
                    if (isinstance(before, list) and len(before) == 2 and
                        all(type(value) is int for value in before)):
                        pending_damage = {
                            **pending_cf,
                            "damageSeq": seq,
                            "damageAmount": event["args"][1],
                            "targetLifeIndex": (event["args"][0] >> 15) & 1,
                            "lifeBefore": before,
                        }
                pending_cf = None
            if opcode == 4:
                duel_index += 1
                pending_cf = None
                pending_damage = None
    return cases


def analyze_capture(path: Path, ids: set[int] = P045_IDS) -> dict:
    sha = hashlib.sha256()
    header, footer = None, None
    expected_seq = 1
    count, terminals, lp_commands = 0, 0, 0
    hits = Counter()
    examples = defaultdict(list)
    by_duel = defaultdict(Counter)
    integrity = []
    with path.open("rb") as source:
        for line_number, raw in enumerate(source, 1):
            sha.update(raw)
            if not raw.strip():
                continue
            record = json.loads(raw)
            kind = record.get("kind")
            if header is None:
                if kind != "metadata":
                    integrity.append("First record is not metadata")
                header = record
            elif kind == "capture_summary":
                if footer is not None:
                    integrity.append("Multiple capture_summary records")
                footer = record
            else:
                if footer is not None:
                    integrity.append("Event after capture_summary")
                seq = record.get("seq")
                if seq != expected_seq:
                    integrity.append(f"Noncontiguous seq at line {line_number}: expected "
                                     f"{expected_seq}, got {seq}")
                    expected_seq = seq + 1 if type(seq) is int else expected_seq
                else:
                    expected_seq += 1
                count += 1
                opcode = _opcode(record)
                if opcode is None:
                    continue
                if opcode == 0x43:
                    lp_commands += 1
                    by_duel[terminals]["LPCommand"] += 1
                candidate = _candidate(record, opcode)
                if candidate and candidate[1] in ids:
                    kind_name, rid = candidate
                    label = f"{kind_name}:{rid}"
                    hits[label] += 1
                    by_duel[terminals][label] += 1
                    if len(examples[label]) < 12:
                        examples[label].append(dict(duelIndex=terminals, seq=seq,
                                                    opcode=f"0x{opcode:04X}",
                                                    caller=record.get("caller")))
                if opcode == 4:
                    terminals += 1
    if header is None or footer is None:
        integrity.append("Missing metadata or capture_summary")
    else:
        if footer.get("complete") is not True:
            integrity.append("Capture footer complete is not true")
        if footer.get("overwritten") != 0:
            integrity.append("Overwritten events are nonzero or unknown")
        if footer.get("eventsWritten") != count:
            integrity.append("Footer eventsWritten disagrees with parsed events")
        if footer.get("capturedDuels") != terminals:
            integrity.append("Footer capturedDuels disagrees with terminal opcode 4")
        if header.get("mode") != "live_duels":
            integrity.append("Not a live_duels capture")
    linked = link_fixed_damage_deltas(path, ids)
    return dict(file=path.name, sha256=sha.hexdigest(), integrityOk=not integrity,
                issues=integrity, events=count, terminalCommands=terminals,
                lpCommandCount=lp_commands,
                linkedLpCases=linked,
                linkedLpCasesExact=sum(case["exactLpDelta"] for case in linked),
                commandCandidates={k: hits[k] for k in sorted(hits)},
                examples={k: examples[k] for k in sorted(examples)},
                perDuel=[dict(duelIndex=i, lpCommands=by_duel[i].get("LPCommand", 0),
                              candidateCommands={key: value for key, value in
                                                 sorted(by_duel[i].items()) if key != "LPCommand"})
                         for i in range(terminals)])


def gather(inputs: list[Path]) -> list[Path]:
    result = []
    for path in inputs:
        if path.is_file() and path.suffix.lower() == ".jsonl":
            result.append(path)
        elif path.is_dir():
            result.extend(path.rglob("duels-*.jsonl"))
        else:
            raise ValueError(f"Not a JSONL capture or directory: {path}")
    unique = list(dict.fromkeys(p.resolve() for p in sorted(result)))
    if not unique:
        raise ValueError("No duels-*.jsonl in input")
    return unique


def report(inputs: list[Path], ids: set[int] = P045_IDS) -> dict:
    sources = [analyze_capture(path, ids) for path in gather(inputs)]
    totals = Counter()
    for entry in sources:
        totals.update(entry["commandCandidates"])
    return dict(schema="yugire-native-effect-command-audit-v1",
                source="Observed native demo commands, not simulated C#",
                interpretationLimit="CF/CE-associated IDs and LP commands are NOT proven "
                                    "effect activations, effect outcomes or causal links. "
                                    "No LP delta can be attributed to a card without "
                                    "native state and timing/context validation.",
                integrityOk=all(x["integrityOk"] for x in sources),
                captureCount=len(sources),
                terminalCommands=sum(x["terminalCommands"] for x in sources),
                events=sum(x["events"] for x in sources),
                lpCommandCount=sum(x["lpCommandCount"] for x in sources),
                linkedLpCases=sum(len(x["linkedLpCases"]) for x in sources),
                linkedLpCasesExact=sum(x["linkedLpCasesExact"] for x in sources),
                inspectedResourceIds=sorted(ids),
                candidateCommands={k: totals[k] for k in sorted(totals)},
                sources=sources)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, nargs="+", required=True,
                        help="Single native duels JSONL or YugiLab output directories")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args(argv)
    try:
        if args.output.exists():
            raise ValueError("Output exists, refusing to overwrite")
        audit = report(args.input)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(audit, ensure_ascii=False, indent=2) + "\n",
                               encoding="utf-8", newline="\n")
        print(f"Native captures {audit['captureCount']}; terminals "
              f"{audit['terminalCommands']}; effect-related command candidates "
              f"{sum(audit['candidateCommands'].values())}; integrity={audit['integrityOk']}")
        print(f"Linked native CF->LP->snapshot cases: "
              f"{audit['linkedLpCasesExact']}/{audit['linkedLpCases']} exact LP deltas")
        print("Linked commands/LP changes are observations; full card activation is not proven.")
        print(f"Report: {args.output}")
        return 0 if audit["integrityOk"] else 2
    except (OSError, ValueError, TypeError, KeyError, json.JSONDecodeError) as exc:
        parser.exit(2, f"Native effect command audit failed: {exc}\n")


if __name__ == "__main__":
    raise SystemExit(main())
