"""Detect structural signatures in already-validated native state-diff reports.

Pure offline analysis; never asserts that an individual card caused an effect.
Field snapshots are non-atomic and may miss transient changes. The result
records same-sample cross-side transfers, shared *raw* associations, and
alternating LP losses while cards have an observed field-residency interval.
"""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import json
from pathlib import Path

SCHEMA = "yugire-native-state-patterns-v1"
INPUT_SCHEMA = "yugire-native-state-interactions-report-v1"


def _identity(record: dict) -> tuple[int, int]:
    return record.get("cardIndex", 0), record.get("occurrenceWord", 0)


def _field_changes(changes: list[dict]) -> list[dict]:
    return [t for t in changes if t.get("kind") == "fieldSlot"
            and type(t.get("slot")) is int and 0 <= t["slot"] <= 10]


def _transfers(changes: list[dict]) -> list[dict]:
    """Pair only same-sample, unambiguous monster exits and entries."""
    exiting: dict[tuple, list[dict]] = defaultdict(list)
    entering: dict[tuple, list[dict]] = defaultdict(list)
    for t in _field_changes(changes):
        if t["slot"] >= 5:
            continue
        before, after = _identity(t["before"]), _identity(t["after"])
        sample = (t["seq"], t.get("previousSampleSeq"))
        if before[0] > 0 and before[1] > 0 and before != after:
            exiting[(sample, before)].append(t)
        if after[0] > 0 and after[1] > 0 and before != after:
            entering[(sample, after)].append(t)
    result = []
    for key, sources in exiting.items():
        targets = entering.get(key, [])
        if len(sources) != 1 or len(targets) != 1:
            continue  # no guessing for duplicate identities in one sample
        source, target = sources[0], targets[0]
        if source["side"] == target["side"]:
            continue
        seq_info, (card, occurrence) = key
        result.append(dict(
            seq=seq_info[0], previousSampleSeq=seq_info[1],
            cardIndex=card, occurrenceWord=occurrence,
            fromSide=source["side"], fromSlot=source["slot"],
            toSide=target["side"], toSlot=target["slot"],
            destinationAssociations=target["after"].get("associations", []),
            signature="CrossSideSameOccurrenceMonsterMove",
            conclusion="ObservedTransitionNotEffectAttribution"))
    return sorted(result, key=lambda x: (x["seq"], x["fromSide"], x["fromSlot"]))


def _shared_associations(changes: list[dict]) -> list[dict]:
    """Only report same-sample additions of the same raw reference to both sides."""
    groups: dict[tuple, list[dict]] = defaultdict(list)
    for t in _field_changes(changes):
        if t["slot"] >= 5 or _identity(t["before"]) != _identity(t["after"]):
            continue
        card, occurrence = _identity(t["after"])
        if card <= 0 or not t["after"].get("associationCountValid", False):
            continue
        if not t["before"].get("associationCountValid", False):
            continue
        old = Counter((a["referenceWord"], a["metadataWord"])
                      for a in t["before"].get("associations", []))
        new = Counter((a["referenceWord"], a["metadataWord"])
                      for a in t["after"].get("associations", []))
        for (ref, metadata), copies in (new - old).items():
            if ref <= 0:
                continue
            groups[(t["seq"], t.get("previousSampleSeq"), ref, metadata)].append(
                dict(side=t["side"], slot=t["slot"],
                     cardIndex=card, occurrenceWord=occurrence,
                     addedCopies=copies))
    found = []
    for (seq, previous, ref, metadata), monsters in groups.items():
        if {m["side"] for m in monsters} != {0, 1}:
            continue
        found.append(dict(
            seq=seq, previousSampleSeq=previous,
            referenceWord=ref, metadataWord=metadata,
            monsters=sorted(monsters, key=lambda m: (m["side"], m["slot"])),
            signature="SharedRawAssociationAddedAcrossSides",
            conclusion="ObservedAssociationNotModifierOrSourceProof"))
    return sorted(found, key=lambda x: (x["seq"], x["referenceWord"]))


def _residencies(changes: list[dict]) -> list[dict]:
    """Collect only slot occupancy intervals with a directly observed entry."""
    fields: list[dict] = []
    open_slots: dict[tuple[int, int], dict] = {}
    for t in sorted(_field_changes(changes),
                    key=lambda t: (t["seq"], t["side"], t["slot"])):
        if t["slot"] < 5:
            continue
        key = (t["side"], t["slot"])
        before, after = _identity(t["before"]), _identity(t["after"])
        if before == after:
            continue
        if key in open_slots:
            entry = open_slots.pop(key)
            entry["endSeq"] = t["seq"]
            fields.append(entry)
        # If entry was in a baseline and no entrance observed, don't invent it.
        if after[0] > 0 and after[1] > 0:
            open_slots[key] = dict(
                cardIndex=after[0], occurrenceWord=after[1],
                side=t["side"], slot=t["slot"],
                startSeq=t["seq"], endSeq=None)
    fields.extend(open_slots.values())
    return fields


def _alternating_lp_losses(changes: list[dict], *, min_occurrences: int) -> list[dict]:
    by_delta: dict[int, list[dict]] = defaultdict(list)
    for t in changes:
        if t.get("kind") == "lifePoints" and type(t.get("delta")) is int:
            if t["delta"] < 0:
                by_delta[t["delta"]].append(t)
    occupancies = _residencies(changes)
    results = []
    for delta, changes_for_delta in sorted(by_delta.items()):
        events = sorted(changes_for_delta, key=lambda t: (t["seq"], t["side"]))
        if len(events) < min_occurrences or any(
                events[i]["side"] == events[i - 1]["side"]
                for i in range(1, len(events))):
            continue
        if {e["side"] for e in events} != {0, 1}:
            continue
        first_seq, last_seq = events[0]["seq"], events[-1]["seq"]
        candidates = [
            dict(cardIndex=r["cardIndex"], occurrenceWord=r["occurrenceWord"],
                 side=r["side"], slot=r["slot"],
                 observedEntrySeq=r["startSeq"], observedExitSeq=r["endSeq"])
            for r in occupancies
            if r["startSeq"] <= first_seq and
            (r["endSeq"] is None or r["endSeq"] > last_seq)
        ]
        results.append(dict(
            delta=delta, occurrences=len(events),
            firstSeq=first_seq, lastSeq=last_seq,
            events=[dict(seq=e["seq"], side=e["side"], delta=e["delta"])
                    for e in events],
            candidateObservedFieldResidencies=sorted(
                candidates, key=lambda x: (x["side"], x["slot"])),
            signature="AlternatingEqualLpLosses",
            conclusion="ObservedPatternNotCardOrPhaseAttribution"))
    return results


def analyze_report(report: dict, *, min_lp_occurrences: int = 4) -> dict:
    if type(min_lp_occurrences) is not int or not 4 <= min_lp_occurrences <= 64:
        raise ValueError("min_lp_occurrences must be 4..64")
    if report.get("schema") != INPUT_SCHEMA:
        raise ValueError("Expected validated state-interactions report schema")
    captures = report.get("captures")
    if not isinstance(captures, list) or not captures:
        raise ValueError("Missing validated captures")
    by_source: dict[tuple, dict] = {}
    for capture in captures:
        key = (capture["file"], capture["sha256"])
        if key in by_source or capture.get("integrityOk") is not True:
            raise ValueError("Duplicate source or non-integral capture")
        by_source[key] = capture
    groups: dict[tuple, list[dict]] = defaultdict(list)
    for t in report.get("transitions", []):
        key = (t.get("file"), t.get("sourceSha256"))
        if key not in by_source:
            raise ValueError("Transition has mismatched/unknown capture fingerprint")
        duel = t.get("duelIndex")
        if type(duel) is not int or duel < 0 or duel >= by_source[key]["terminalCommands"]:
            raise ValueError("Transition has invalid duelIndex")
        groups[(key, duel)].append(t)
    runs = []
    for key, capture in by_source.items():
        for duel in range(capture["terminalCommands"]):
            rows = groups[(key, duel)]
            transfers = _transfers(rows)
            associations = _shared_associations(rows)
            losses = _alternating_lp_losses(rows,
                                            min_occurrences=min_lp_occurrences)
            runs.append(dict(
                file=key[0], sourceSha256=key[1], duelIndex=duel,
                crossSideTransfers=transfers,
                sharedAssociations=associations,
                alternatingLpLosses=losses,
                counts=dict(crossSideTransfers=len(transfers),
                            sharedAssociations=len(associations),
                            alternatingLpLosses=len(losses))))
    return dict(
        schema=SCHEMA,
        inputSchema=INPUT_SCHEMA,
        minLpOccurrences=min_lp_occurrences,
        runs=runs,
        limitations=[
            "All deltas are non-atomic command-entry observations, not after-effects.",
            "Same cardIndex+occurrenceWord on different sides suggests a physical control transfer, but does not prove its cause or original owner.",
            "Raw association referenceWord/metadataWord does not prove attribute modification or identify its source encoding.",
            "Repeated LP losses and observed field residency have temporal overlap only; field presence is not proof of an active continuous effect.",
            "Report contains deltas only: baseline occupancy, hidden transitions, turn phases, exact Graveyard identities and ATK/DEF are not recoverable.",
        ])


def markdown(report: dict) -> str:
    lines = ["# Padrões estruturais observados no estado nativo", "",
             "**Observação temporal, não validação causal de carta.**", ""]
    for run in report["runs"]:
        lines.extend([f"## {run['file']} — duelo {run['duelIndex'] + 1}", "",
                      f"- SHA256 da fonte: `{run['sourceSha256']}`"])
        lines.extend([
            f"- Transferências entre lados: **{run['counts']['crossSideTransfers']}**",
            f"- Associações brutas compartilhadas: **{run['counts']['sharedAssociations']}**",
            f"- Padrões de perdas alternadas de LP: **{run['counts']['alternatingLpLosses']}**", ""])
        for t in run["crossSideTransfers"]:
            lines.append(f"- Transferência seq {t['seq']}: índice {t['cardIndex']} "
                         f"ocorrência {t['occurrenceWord']}, "
                         f"lado {t['fromSide']}/slot {t['fromSlot']} → "
                         f"lado {t['toSide']}/slot {t['toSlot']}")
        for a in run["sharedAssociations"]:
            lines.append(f"- Associação seq {a['seq']}: ref {a['referenceWord']} "
                         f"(0x{a['referenceWord']:04X}), meta {a['metadataWord']}; "
                         f"{[(m['side'], m['slot'], m['cardIndex']) for m in a['monsters']]}")
        for loss in run["alternatingLpLosses"]:
            fields = [(c["cardIndex"], c["side"], c["slot"])
                      for c in loss["candidateObservedFieldResidencies"]]
            lines.append(f"- LP alternado: delta {loss['delta']}, "
                         f"{loss['occurrences']} ocorrências seq "
                         f"{loss['firstSeq']}–{loss['lastSeq']}; "
                         f"cartas observadas no campo durante todo o intervalo: {fields}")
        lines.append("")
    lines.extend(["## Limitações", ""])
    lines.extend("- " + item for item in report["limitations"])
    return "\n".join(lines) + "\n"


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--state-report", required=True, type=Path)
    p.add_argument("--output-dir", required=True, type=Path)
    p.add_argument("--min-lp-occurrences", type=int, default=4)
    a = p.parse_args(argv)
    try:
        if a.output_dir.exists():
            raise ValueError("Output directory already exists; refusing overwrite")
        report = analyze_report(json.loads(a.state_report.read_text(encoding="utf-8")),
                                min_lp_occurrences=a.min_lp_occurrences)
        a.output_dir.mkdir(parents=True)
        (a.output_dir / "patterns.json").write_text(
            json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        (a.output_dir / "patterns.md").write_text(markdown(report), encoding="utf-8")
        for run in report["runs"]:
            print(f"{run['file']} duel {run['duelIndex']+1}: "
                  f"{run['counts']['crossSideTransfers']} cross-side transfers; "
                  f"{run['counts']['sharedAssociations']} shared associations; "
                  f"{run['counts']['alternatingLpLosses']} alternating LP loss patterns")
        print("Conclusions are observation-only. See patterns.md.")
        return 0
    except (ValueError, KeyError, OSError, TypeError, json.JSONDecodeError) as exc:
        p.exit(2, f"State pattern detection failed: {exc}\n")


if __name__ == "__main__":
    raise SystemExit(main())
