"""Audit repeated initial deck shuffles across Joey/YugiLab captures.

Uses only existing passive demo_init/deck_shuffle events (NO RNG hooking).
A matching fingerprint means the two *observed initial deck permutations*
are equal. It is not proof of an identical RNG seed or complete duel.
The reader streams JSONL and ZIP members: no multi-GB files in memory.

Usage:
    python -m yugilab.rng_audit scratch/labs/parallel-xxxx/
    python -m yugilab.rng_audit parallel-i79ol65z.zip parallel-u405p7z4.zip
"""
from __future__ import annotations

from collections import defaultdict
from contextlib import contextmanager
import hashlib
import io
import json
from pathlib import Path
from typing import Iterator
from zipfile import ZipFile


@contextmanager
def _sources(inputs: list[Path]) -> Iterator[list[tuple[str, object]]]:
    """Yield labeled text streams, close all streams before returning."""
    from contextlib import ExitStack
    with ExitStack() as stack:
        result: list[tuple[str, object]] = []
        for path in inputs:
            path = Path(path)
            if path.is_dir():
                for child in sorted(path.rglob("*.jsonl")):
                    result.append((str(child), stack.enter_context(
                        child.open(encoding="utf-8"))))
            elif path.suffix.casefold() == ".zip":
                archive = stack.enter_context(ZipFile(path))
                for name in sorted(archive.namelist()):
                    if not name.casefold().endswith(".jsonl"):
                        continue
                    stream = stack.enter_context(archive.open(name))
                    result.append((f"{path}!{name}", stack.enter_context(
                        io.TextIOWrapper(stream, encoding="utf-8"))))
            elif path.is_file() and path.suffix.casefold() == ".jsonl":
                result.append((str(path), stack.enter_context(
                    path.open(encoding="utf-8"))))
            else:
                raise ValueError(f"Not a JSONL, directory, or ZIP: {path}")
        yield result


def _digest(indices: dict[int, list[int]]) -> str:
    packed = json.dumps(
        {"0": indices[0], "1": indices[1]}, separators=(",", ":"),
        ensure_ascii=True).encode("ascii")
    return hashlib.sha256(packed).hexdigest()


def _scan_stream(lines, source: str) -> dict:
    pid = None
    started_utc = None
    samples: list[dict] = []
    candidate_calls: list[dict] = []
    latest_seed_candidate = None
    scene = None
    scenes = 0
    complete = False
    footer_found = False
    for number, raw in enumerate(lines, 1):
        if not raw.strip():
            continue
        try:
            obj = json.loads(raw)
        except (UnicodeError, ValueError) as exc:
            raise ValueError(f"Invalid JSONL {source}:{number}: {exc}") from exc
        kind = obj.get("kind")
        if kind == "metadata":
            pid = obj.get("processId")
            started_utc = obj.get("startedUtc")
        elif (kind == "demo_init" and
              obj.get("function") == "rng_seed_candidate" and
              obj.get("phase") == "enter"):
            raw_seed = obj.get("candidateSeedRaw")
            if type(raw_seed) is int:
                latest_seed_candidate = {
                    "valueRaw": raw_seed,
                    "caller": obj.get("caller"),
                    "seq": obj.get("seq"),
                }
                latest_seed_candidate["duelEntrySeedCall"] = (
                    obj.get("duelEntrySeedCall") is True)
                latest_seed_candidate["overridden"] = (
                    obj.get("seedOverrideApplied") is True)
                latest_seed_candidate["effectiveSeedRaw"] = (
                    obj.get("effectiveSeedRaw"))
                latest_seed_candidate["globalDuelIndex"] = (
                    obj.get("globalDuelIndex"))
                candidate_calls.append(latest_seed_candidate)
                # The verified FUN_005BFEB0 seed call occurs AFTER
                # scene_config/enter and BEFORE deck_shuffle. The previous
                # 'beforeScene' field is historical context, not this seed.
                if scene is not None and obj.get("duelEntrySeedCall") is True:
                    scene["duelEntrySeed"] = dict(latest_seed_candidate)
        elif kind == "capture_summary":
            footer_found = True
            complete = obj.get("complete") is True
        elif kind == "demo_init":
            fn = obj.get("function")
            phase = obj.get("phase")
            if (fn == "scene_config" and phase == "enter" and
                    obj.get("requestedSceneMode") == 7):
                scenes += 1
                scene = {"duel": scenes, "seen": {}, "sceneSeq": obj.get("seq"),
                         "sceneTimestampMs": obj.get("timestampMs"),
                         "duelEntrySeed": None,
                         "seedCandidateBeforeScene": (
                             dict(latest_seed_candidate)
                             if latest_seed_candidate is not None else None)}
            elif (fn == "deck_shuffle" and phase == "leave" and
                  scene is not None):
                side = obj.get("side")
                deck = obj.get("deck")
                if side not in (0, 1) or not isinstance(deck, dict):
                    continue
                indices = deck.get("indices")
                if (not isinstance(indices, list) or
                    not indices or
                    any(type(idx) is not int for idx in indices)):
                    continue
                if side in scene["seen"]:
                    continue  # Only the first shuffle for each side per duel.
                scene["seen"][side] = list(indices)
                if len(scene["seen"]) == 2:
                    samples.append({
                        "source": source,
                        "processId": pid,
                        "startedUtc": started_utc,
                        "duel": scenes,
                        "sceneSeq": scene["sceneSeq"],
                        "sceneTimestampMs": scene["sceneTimestampMs"],
                        "seedCandidateBeforeScene": (
                            scene["seedCandidateBeforeScene"]),
                        "duelEntrySeed": scene["duelEntrySeed"],
                        "fingerprint": _digest(scene["seen"]),
                        "deckSizes": {"0": len(scene["seen"][0]),
                                      "1": len(scene["seen"][1])},
                    })
                    scene = None  # Ignore extra shuffles within this duel.
    return {
        "source": source, "processId": pid,
        "capturedScenes": scenes,
        "fingerprintedScenes": len(samples),
        "footerFound": footer_found, "captureComplete": complete,
        "rngSeedCandidateCalls": candidate_calls,
        "samples": samples,
    }


def audit_captures(sources: list[Path]) -> dict:
    """Compare a set of captures, returning concise JSON-serializable data.

    The duplicate groups can contain both within-worker and cross-worker
    matches. Incomplete captures are included but reported explicitly.
    """
    if not sources:
        raise ValueError("At least one JSONL, directory, or ZIP is required")
    scanned = []
    with _sources(sources) as streams:
        for label, stream in streams:
            scanned.append(_scan_stream(stream, label))
    groups = defaultdict(list)
    for capture in scanned:
        for sample in capture["samples"]:
            groups[sample["fingerprint"]].append(sample)
    duplicates = []
    for fingerprint, matches in sorted(groups.items()):
        if len(matches) < 2:
            continue
        duplicates.append({
            "fingerprint": fingerprint,
            "count": len(matches),
            "crossProcess": len({(m["source"], m["processId"])
                                 for m in matches}) > 1,
            "matches": [{
                "source": m["source"],
                "processId": m["processId"], "duel": m["duel"],
                "sceneTimestampMs": m["sceneTimestampMs"],
                "seedCandidateBeforeScene": m["seedCandidateBeforeScene"],
                "duelEntrySeed": m["duelEntrySeed"],
            } for m in matches],
        })
    duplicates.sort(key=lambda d: (-d["count"], d["fingerprint"]))
    all_samples = [s for f in scanned for s in f["samples"]]
    return {
        "formatVersion": 1, "method": "first deck_shuffle/leave per side "
        "after scene_config/enter mode 7, SHA256 of both index arrays",
        "captureCount": len(scanned),
        "totalScenesObserved": sum(c["capturedScenes"] for c in scanned),
        "fingerprintedScenes": len(all_samples),
        "uniqueFingerprints": len(groups),
        "duplicateObservations": len(all_samples) - len(groups),
        "crossProcessDuplicateGroups": sum(d["crossProcess"] for d in duplicates),
        "duplicateGroups": duplicates,
        "incompleteSources": [c["source"] for c in scanned
                             if not c["footerFound"] or not c["captureComplete"]],
        "sources": [{
            "source": c["source"], "processId": c["processId"],
            "capturedScenes": c["capturedScenes"],
            "fingerprintedScenes": c["fingerprintedScenes"],
            "captureComplete": c["captureComplete"],
            "rngSeedCandidateCalls": c["rngSeedCandidateCalls"],
            "fingerprints": [{"duel": s["duel"], "fingerprint": s["fingerprint"],
                              "sceneTimestampMs": s["sceneTimestampMs"],
                              "seedCandidateBeforeScene": s["seedCandidateBeforeScene"],
                              "duelEntrySeed": s["duelEntrySeed"]}
                             for s in c["samples"]]
        } for c in scanned],
        "interpretation": "Identical initial permutations may indicate correlated "
        "RNG initialization; this does not prove seeds or full duel equivalence. "
        "The 0x005C3BF5 setter is confirmed by Ghidra; the logged "
        "candidateSeedRaw is the ORIGINAL argument before any YugiLab override.",
    }


def main(argv: list[str] | None = None) -> int:
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("sources", nargs="+", type=Path)
    parser.add_argument("--output", type=Path, help="Write audit as JSON")
    args = parser.parse_args(argv)
    report = audit_captures(args.sources)
    print(f"[rng-audit] {report['fingerprintedScenes']} duels fingerprinted; "
          f"{report['uniqueFingerprints']} unique initial shuffle pairs; "
          f"{report['crossProcessDuplicateGroups']} cross-process "
          "duplicate groups", flush=True)
    for group in report["duplicateGroups"][:10]:
        print(f"[rng-audit] DUPLICATE {group['fingerprint'][:12]} "
              f"({group['count']} matches): " +
              ", ".join(f"pid={m['processId']}/duel={m['duel']}"
                        for m in group["matches"]), flush=True)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(
            json.dumps(report, indent=2, ensure_ascii=False) + "\n",
            encoding="utf-8")
        print(f"[rng-audit] JSON: {args.output}", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
