"""Analyze read-only command-entry state deltas, never infer card-effect causality.

A state delta is observed at the NEXT command producer entry, not as an
atomic mutation triggered by the command that carries it. Graveyard counts
do not identify individual cards. Raw face/position flags remain raw.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
from pathlib import Path

from .discovery import _capture_summary
from .effect_report import _candidate, _opcode, gather

SCALARS = ("lifePoints", "handCount", "deckCount", "graveyardCount",
           "terminalFlags", "rawPlayerStatusByte8")
STATE_SCHEMA = "joey-command-entry-state-delta-v1"
RAIN_RID = 1315
SKULL_INDEX = 414
RAIN_INDEX = 969


def _field_baseline(sides: list[dict]) -> list[dict]:
    if not isinstance(sides, list) or len(sides) != 2:
        raise ValueError("State baseline needs two players")
    result = copy.deepcopy(sides)
    for side, player in enumerate(result):
        if player.get("side") != side or len(player.get("fieldSlots", [])) != 11:
            raise ValueError("State baseline side or slot layout invalid")
        if any(slot.get("slot") != i for i, slot in enumerate(player["fieldSlots"])):
            raise ValueError("Field slot numbering invalid")
        if not isinstance(player.get("handIndices"), list):
            raise ValueError("State baseline lacks hand indices")
    return result


def _apply_delta(sides: list[dict], changes: list[dict]) -> list[dict]:
    if not isinstance(changes, list) or len(changes) > 2:
        raise ValueError("State delta needs at most two sides")
    next_state = copy.deepcopy(sides)
    seen = set()
    for change in changes:
        side = change.get("side")
        if type(side) is not int or side not in (0, 1) or side in seen:
            raise ValueError("Duplicate or unknown state-delta side")
        seen.add(side)
        target = next_state[side]
        for key in SCALARS + ("handIndices",):
            if key in change:
                pair = change[key]
                if not isinstance(pair, dict) or pair.get("before") != target.get(key):
                    raise ValueError(f"State delta stale/mismatched {key} side {side}")
                target[key] = copy.deepcopy(pair["after"])
        for slot_change in change.get("fieldSlots", []):
            index = slot_change.get("slot")
            if type(index) is not int or index not in range(11):
                raise ValueError("Invalid state-delta field slot")
            if slot_change.get("before") != target["fieldSlots"][index]:
                raise ValueError(f"Field state delta stale/mismatched side {side} slot {index}")
            replacement = copy.deepcopy(slot_change.get("after"))
            if not isinstance(replacement, dict) or replacement.get("slot") != index:
                raise ValueError("State-delta replacement slot invalid")
            target["fieldSlots"][index] = replacement
    return next_state


def _state_changes(event: dict, duel_index: int) -> list[dict]:
    observation = event["stateObservation"]
    if observation.get("kind") != "delta":
        return []
    results = []
    for side in observation["changes"]:
        n = side["side"]
        for key in SCALARS:
            if key in side:
                before = side[key]["before"]
                after = side[key]["after"]
                results.append(dict(duelIndex=duel_index, seq=event["seq"],
                                    previousSampleSeq=observation.get("previousSampleSeq"),
                                    side=n, kind=key, before=before, after=after,
                                    **({"delta": after - before}
                                       if type(before) is int and type(after) is int
                                       else {})))
        for field in side.get("fieldSlots", []):
            a, b = field["before"], field["after"]
            results.append(dict(duelIndex=duel_index, seq=event["seq"],
                                previousSampleSeq=observation.get("previousSampleSeq"),
                                side=n, kind="fieldSlot", slot=field["slot"],
                                cardBefore=a["cardIndex"], cardAfter=b["cardIndex"],
                                before=a, after=b))
        if "handIndices" in side:
            results.append(dict(duelIndex=duel_index, seq=event["seq"],
                                previousSampleSeq=observation.get("previousSampleSeq"),
                                side=n, kind="handIndices", **side["handIndices"]))
    return results


def analyze_capture(path: Path, *, span: int = 48) -> dict:
    if not 8 <= span <= 300:
        raise ValueError("span must be 8..300 seq IDs")
    summary, _, _, _, _ = _capture_summary(path)
    if not summary["integrityOk"]:
        raise ValueError(f"Native capture integrity failed {path}: {summary['issues']}")
    sha = hashlib.sha256(path.read_bytes()).hexdigest()
    state, last_sample_seq, duel, counts = None, None, 0, {
        "samples":0, "baselines":0, "deltas":0, "readErrors":0,
        "unsampledCommandEntries":0
    }
    transitions, cases, pending = [], [], []
    header, footer = None, None
    previous_seq = 0
    with path.open(encoding="utf-8") as stream:
        for raw in stream:
            if not raw.strip():
                continue
            event = json.loads(raw)
            kind = event.get("kind")
            if kind == "metadata":
                if header is not None:
                    raise ValueError("Repeated native metadata")
                header = event
                trace = event.get("stateTrace")
                if not isinstance(trace, dict) or trace.get("enabled") is not True or (
                    trace.get("schema") != STATE_SCHEMA
                ):
                    raise ValueError("Not a command-entry state-trace capture")
                continue
            if kind == "capture_summary":
                footer = event
                continue
            seq = event.get("seq")
            if type(seq) is not int or seq != previous_seq + 1:
                raise ValueError("Non-contiguous native seq during state scan")
            previous_seq = seq
            if kind != "command":
                continue
            opcode = _opcode(event)
            observation = event.get("stateObservation")
            if event.get("stateReadError") is not None:
                counts["readErrors"] += 1
                state, last_sample_seq = None, None
            elif event.get("stateSampled") is True:
                counts["samples"] += 1
                if isinstance(observation, dict):
                    if (observation.get("samplePoint") != "command_producer_onEnter"
                        or observation.get("nonAtomic") is not True):
                        raise ValueError("Unexpected state observation sampling point")
                    if observation.get("kind") == "baseline":
                        state = _field_baseline(observation.get("sides"))
                        counts["baselines"] += 1
                    elif observation.get("kind") == "delta":
                        if state is None or observation.get("previousSampleSeq") != last_sample_seq:
                            raise ValueError("State delta missing contiguous previous sample")
                        state = _apply_delta(state, observation.get("changes"))
                        counts["deltas"] += 1
                        transitions.extend(_state_changes(event,duel))
                    else:
                        raise ValueError("Unexpected state observation kind")
                elif state is None:
                    raise ValueError("State sample missing initial baseline")
                last_sample_seq = seq
            else:
                counts["unsampledCommandEntries"] += 1
                state, last_sample_seq = None, None

            # A candidate is only a native CF link, not proof of card
            # resolution. The current snapshot is BEFORE this command.
            candidate = _candidate(event, opcode)
            if candidate == ("CF", RAIN_RID):
                skull_slots = []
                if state is not None:
                    for player in state:
                        for slot in player["fieldSlots"]:
                            if 5 <= slot["slot"] <= 9 and slot["cardIndex"] == SKULL_INDEX:
                                skull_slots.append(dict(
                                    side=player["side"], slot=slot["slot"],
                                    positionWord=slot["positionWord"],
                                    flagsWord=slot["flagsWord"],
                                    positionBit1Set=bool(slot["positionWord"] & 2)))
                pending.append(dict(
                    capture=path.name, sourceSha256=sha, duelIndex=duel,
                    rainCfSeq=seq, skullSlotsPresentAtRainCf=skull_slots,
                    stateAvailableAtCf=state is not None, fieldExits=[],
                    graveCountIncreases=[], lpLoss300=[], damageCommands300=[],
                    conclusion="Unverified"))
            if pending:
                for case in pending:
                    if case["duelIndex"] != duel or seq < case["rainCfSeq"] or (
                        seq - case["rainCfSeq"] > span
                    ):
                        continue
                    if opcode == 0x0043 and len(event.get("args",[])) >= 2 and event["args"][1] == 300:
                        case["damageCommands300"].append(dict(
                            seq=seq, targetSide=(event["args"][0] >> 15) & 1))
                    for transition in _state_changes(event,duel):
                        if transition["kind"] == "fieldSlot" and (
                            transition["cardBefore"] == RAIN_INDEX
                            and transition["cardAfter"] != RAIN_INDEX
                        ):
                            case["fieldExits"].append(dict(
                                seq=seq, side=transition["side"],
                                slot=transition["slot"], newCardIndex=transition["cardAfter"]))
                        if transition["kind"] == "graveyardCount" and transition.get("delta",0) > 0:
                            case["graveCountIncreases"].append(dict(
                                seq=seq, side=transition["side"], countDelta=transition["delta"]))
                        if transition["kind"] == "lifePoints" and transition.get("delta") == -300:
                            case["lpLoss300"].append(dict(seq=seq, side=transition["side"]))
            if opcode == 0x0004:
                cases.extend(pending)
                pending, state, last_sample_seq = [], None, None
                duel += 1

    if footer is None:
        raise ValueError("State capture missing footer")
    expected = footer.get("stateTrace")
    if not isinstance(expected, dict) or any(expected.get(key) != counts[key]
                                            for key in ("samples","baselines","deltas","readErrors")):
        raise ValueError("State sample counts do not match capture footer")
    if counts["baselines"] < duel:
        raise ValueError("Missing state baseline for a completed duel")
    return dict(file=path.name, sha256=sha, integrityOk=summary["integrityOk"],
                terminalCommands=duel, stateTrace=counts,
                transitions=transitions, rainSkullCases=cases,
                interpretationLimit="Command-entry snapshots are non-atomic and may miss "
                                    "transient states between commands. Field exits and "
                                    "graveyard count increases are separate observations; "
                                    "they do NOT prove a specific card reached the Graveyard. "
                                    "An apparent face flag cannot prove continuous-effect "
                                    "activation or assign the source of a 300-LP loss.")


def summarize(inputs: list[Path], *, span: int = 48) -> dict:
    files = gather(inputs)
    captures = [analyze_capture(path, span=span) for path in files]
    return dict(schema="yugire-native-state-interactions-report-v1",
                captures=[{k:v for k,v in c.items() if k not in ("transitions","rainSkullCases")}
                          for c in captures],
                transitions=[dict(file=c["file"], sourceSha256=c["sha256"], **t)
                             for c in captures for t in c["transitions"]],
                rainSkullCases=[case for c in captures for case in c["rainSkullCases"]],
                interpretationLimit="Temporal association is not causal validation of "
                                    "a card's effect or true graveyard card identity.")


def _markdown(result: dict) -> str:
    lines = ["# Estado diferencial nativo — sumário", "",
             f"Capturas: {len(result['captures'])}; mudanças: "
             f"{len(result['transitions'])}; Rain/Skull candidatos: "
             f"{len(result['rainSkullCases'])}.", "",
             "Snapshots na entrada do produtor (não atômicos): não são "
             "consequências atribuídas automaticamente ao comando.", ""]
    for case in result["rainSkullCases"]:
        lines.extend([
            f"## Rain CF seq {case['rainCfSeq']} — duelo {case['duelIndex']+1}", "",
            f"- Skull em posição física 5–9 na amostra: {case['skullSlotsPresentAtRainCf']}",
            f"- Rain saiu de posição observada: {case['fieldExits']}",
            f"- Incrementos de contador de cemitério: {case['graveCountIncreases']}",
            f"- Comandos de 300 LP: {case['damageCommands300']}",
            f"- Quedas de 300 LP observadas: {case['lpLoss300']}",
            "- **Conclusão: Unverified.** Falta validar a identidade do destino "
            "e a atividade da armadilha.",
            ""
        ])
    return "\n".join(lines)


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--input", nargs="+", required=True, type=Path)
    p.add_argument("--output-dir", required=True, type=Path)
    p.add_argument("--span", type=int, default=48,
                   help="Maximum number of native seq IDs after Rain CF (8..300)")
    a = p.parse_args(argv)
    try:
        if a.output_dir.exists():
            raise ValueError(f"Output directory exists: {a.output_dir}")
        report = summarize(a.input, span=a.span)
        a.output_dir.mkdir(parents=True)
        (a.output_dir / "state-report.json").write_text(
            json.dumps(report,ensure_ascii=False,indent=2)+"\n",
            encoding="utf-8", newline="\n")
        (a.output_dir / "state-summary.md").write_text(
            _markdown(report),encoding="utf-8",newline="\n")
        print(f"Native state captures: {len(report['captures'])}; "
              f"transitions: {len(report['transitions'])}; "
              f"Rain/Skull candidates: {len(report['rainSkullCases'])}")
        print(f"Output: {a.output_dir}. Interactions remain unverified.")
        return 0
    except (ValueError, OSError, KeyError, TypeError, json.JSONDecodeError) as exc:
        p.exit(2, f"State report failed: {exc}\n")


if __name__ == "__main__":
    raise SystemExit(main())
