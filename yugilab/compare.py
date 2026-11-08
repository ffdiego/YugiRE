"""Compare seeded YugiLab captures across independent runs.

Accept ZIP, run directory, or a single JSONL on each side. Compare by the
*global duel index* from rngSeedOverride, not Python PID or worker order.
No opaque native fields are silently masked: low/high 16-bit command results
and full raw differences are each reported separately.

    python -m yugilab.compare first.zip second.zip --output comparison.json

Exit 0: all observable checkpoints match; 1: differences; 2: invalid capture.
A status of 'observed_match_with_raw_differences' is NOT byte-identical replay.
"""
from __future__ import annotations

from collections import Counter
from contextlib import contextmanager
from dataclasses import dataclass
import argparse
import hashlib
import io
from itertools import zip_longest
import json
from pathlib import Path
import sys
from typing import Iterator
from zipfile import ZipFile

from .seed_math import derive_duel_seed
from .command_words import classify_high_word_change


@dataclass(frozen=True)
class Capture:
    file: Path
    member: str | None
    metadata: dict

    @property
    def label(self) -> str:
        return f"{self.file}!{self.member}" if self.member else str(self.file)

    @property
    def start(self) -> int:
        return self.metadata["rngSeedOverride"]["globalDuelStartIndex"]

    @property
    def count(self) -> int:
        return self.metadata["requestedDuels"]


@contextmanager
def _open_capture(file: Path, member: str | None) -> Iterator[object]:
    if member is None:
        with file.open(encoding="utf-8") as stream:
            yield stream
    else:
        with ZipFile(file) as archive, archive.open(member) as binary:
            with io.TextIOWrapper(binary, encoding="utf-8") as stream:
                yield stream


def _header(file: Path, member: str | None) -> Capture:
    with _open_capture(file, member) as stream:
        line = stream.readline()
    try:
        meta = json.loads(line)
    except (ValueError, UnicodeError) as exc:
        raise ValueError(f"Invalid metadata header in {file}: {exc}") from exc
    if not isinstance(meta, dict) or meta.get("kind") != "metadata":
        raise ValueError(f"No metadata header in {file}")
    seed = meta.get("rngSeedOverride")
    if not isinstance(seed, dict) or seed.get("algorithm") != "mix32-v1":
        raise ValueError(f"This comparator requires --seed and mix32-v1: {file}")
    start = seed.get("globalDuelStartIndex")
    base = seed.get("baseSeed")
    count = meta.get("requestedDuels")
    if (type(start) is not int or type(base) is not int or
        type(count) is not int or count < 1 or
        not 0 <= start <= 0xFFFFFFFF or not 0 <= base <= 0xFFFFFFFF or
        start + count > 0x100000000):
        raise ValueError(f"Invalid global seed range in {file}")
    return Capture(file, member, meta)


def _sources(path: Path) -> list[Capture]:
    path = path.expanduser().resolve(strict=True)
    if path.is_dir():
        sources = [_header(p, None) for p in sorted(path.rglob("*.jsonl"))]
    elif path.suffix.casefold() == ".zip":
        with ZipFile(path) as archive:
            names = [n for n in archive.namelist()
                     if n.casefold().endswith(".jsonl") and not n.endswith("/")]
        sources = [_header(path, n) for n in sorted(names)]
    elif path.suffix.casefold() == ".jsonl":
        sources = [_header(path, None)]
    else:
        raise ValueError(f"Expected a ZIP, JSONL, or run directory: {path}")
    if not sources:
        raise ValueError(f"No JSONL captures in {path}")
    sources.sort(key=lambda src: src.start)
    for before, after in zip(sources, sources[1:]):
        if before.start + before.count != after.start:
            raise ValueError("Overlapping/missing global duel indices between "
                             f"{before.label} and {after.label}")
    return sources


def _terminal(event: dict) -> bool:
    return (event.get("kind") == "command" and
            str(event.get("opcodeMasked", "")).lower() == "0x0004")


def _stream_duels(sources: list[Capture]) -> Iterator[tuple[int, list[dict]]]:
    for source in sources:
        events: list[dict] = []
        observed = 0
        seq = 0
        terminals = 0
        finished = False
        with _open_capture(source.file, source.member) as stream:
            next(stream)  # metadata previously validated
            for line_number, line in enumerate(stream, 2):
                try:
                    row = json.loads(line)
                except (ValueError, UnicodeError) as exc:
                    raise ValueError(f"Bad JSONL at {source.label}:{line_number}: {exc}") from exc
                if not isinstance(row, dict):
                    raise ValueError(f"Unexpected JSON value at {source.label}:{line_number}")
                if row.get("kind") == "capture_summary":
                    if (row.get("complete") is not True or
                        row.get("overwritten") != 0 or
                        row.get("capturedDuels") != source.count or
                        row.get("eventsWritten") != observed or
                        row.get("rngSeedOverridesApplied") != source.count or
                        terminals != source.count or events):
                        raise ValueError(f"Incomplete or inconsistent capture footer: "
                                         f"{source.label}")
                    if next(stream, None) is not None:
                        raise ValueError(f"Trailing content after footer: {source.label}")
                    finished = True
                    break
                seq += 1
                observed += 1
                if row.get("seq") != seq:
                    raise ValueError(f"Noncontiguous seq in {source.label}: "
                                     f"expected {seq}, got {row.get('seq')}")
                events.append(row)
                if _terminal(row):
                    index = source.start + terminals
                    seed_events = [e for e in events if e.get("kind") == "demo_init"
                                   and e.get("function") == "rng_seed_candidate"
                                   and e.get("phase") == "enter"
                                   and e.get("seedOverrideApplied") is True]
                    expected_seed = derive_duel_seed(
                        source.metadata["rngSeedOverride"]["baseSeed"], index)
                    if (len(seed_events) != 1 or
                        seed_events[0].get("globalDuelIndex") != index or
                        seed_events[0].get("effectiveSeedRaw") != expected_seed):
                        raise ValueError(f"Missing/incorrect applied seed for "
                                         f"global duel {index}: {source.label}")
                    terminals += 1
                    if terminals > source.count:
                        raise ValueError(f"Extra terminal in {source.label}")
                    yield index, events
                    events = []
        if not finished:
            raise ValueError(f"Missing complete footer in {source.label}")


def _stable_hash(value: object) -> str:
    data = json.dumps(value, sort_keys=True, ensure_ascii=True,
                      separators=(",", ":")).encode("ascii")
    return hashlib.sha256(data).hexdigest()


def _strip_observation_fields(event: dict) -> dict:
    return {key: value for key, value in event.items()
            if key not in {"seq", "callId", "timestampMs", "threadId"}}


def _shuffles(events: list[dict]) -> list[dict]:
    return [{"side": event.get("side"), "deck": event.get("deck"),
             "shufflePasses": event.get("shufflePasses")}
            for event in events if event.get("kind") == "demo_init"
            and event.get("function") == "deck_shuffle"
            and event.get("phase") == "leave"]


def _reseeds(events: list[dict]) -> list[dict]:
    return [{"caller": e.get("caller"), "rawSeed": e.get("candidateSeedRaw")}
            for e in events if e.get("kind") == "demo_init"
            and e.get("function") == "rng_seed_candidate"
            and e.get("phase") == "enter"
            and e.get("duelEntrySeedCall") is not True]


def _compare_duel(index: int, left: list[dict], right: list[dict],
                  max_examples: int) -> dict:
    ai_a = [_strip_observation_fields(e) for e in left if e.get("kind") == "ai_probe"]
    ai_b = [_strip_observation_fields(e) for e in right if e.get("kind") == "ai_probe"]
    ex_a = [_strip_observation_fields(e) for e in left if e.get("kind") == "demo_executor"]
    ex_b = [_strip_observation_fields(e) for e in right if e.get("kind") == "demo_executor"]
    sh_a, sh_b = _shuffles(left), _shuffles(right)
    commands_a = [e for e in left if e.get("kind") == "command"]
    commands_b = [e for e in right if e.get("kind") == "command"]
    ce_a = [e.get("ceNativeState") for e in commands_a
            if e.get("opcodeMasked") == "0x00CE"]
    ce_b = [e.get("ceNativeState") for e in commands_b
            if e.get("opcodeMasked") == "0x00CE"]
    wrapper86_a = [e.get("wrapper86Caller") for e in commands_a
                   if e.get("opcodeMasked") == "0x0086" and
                   e.get("caller") == "0x574804"]
    wrapper86_b = [e.get("wrapper86Caller") for e in commands_b
                   if e.get("opcodeMasked") == "0x0086" and
                   e.get("caller") == "0x574804"]
    shape = lambda e: (e.get("kind"), e.get("function"), e.get("phase"))
    same_layout = [shape(e) for e in left] == [shape(e) for e in right]
    same_structure = (len(commands_a) == len(commands_b) and all(
        (a.get("opcodeRaw"), a.get("opcodeMasked"), a.get("sideBit15"), a.get("caller")) ==
        (b.get("opcodeRaw"), b.get("opcodeMasked"), b.get("sideBit15"), b.get("caller"))
        for a, b in zip(commands_a, commands_b)))
    low_equal, raw_equal = same_structure, same_structure
    deltas: Counter = Counter()
    difference_origins: Counter = Counter()
    changed_commands = 0
    high_only_commands = 0
    examples: list[dict] = []
    for a, b in zip(commands_a, commands_b):
        av, bv = a.get("args"), b.get("args")
        if (not isinstance(av, list) or not isinstance(bv, list) or
            len(av) != len(bv) or not all(type(x) is int for x in av + bv)):
            raise ValueError(f"Malformed command args in global duel {index}")
        changed = [(n, v, w) for n, (v, w) in enumerate(zip(av, bv)) if v != w]
        if changed:
            changed_commands += 1
            raw_equal = False
            only_high = all((v & 0xFFFF) == (w & 0xFFFF) for _, v, w in changed)
            if only_high:
                high_only_commands += 1
            else:
                low_equal = False
            # Classify the origin of each changed upper word by specific
            # Ghidra-proven callsite; do not normalize any raw data away.
            origins = [classify_high_word_change(
                a.get("opcodeMasked"), a.get("caller"), n, v, w)
                for n, v, w in changed]
            if len(origins) == 1:
                difference_origins[origins[0]] += 1
            else:
                difference_origins["multiple:" + "|".join(sorted(origins))] += 1
            for (n, v, w), origin in zip(changed, origins):
                deltas[(a.get("opcodeMasked"), a.get("caller"), n,
                        (v >> 16) & 0xFFFF, (w >> 16) & 0xFFFF,
                        origin)] += 1
            if len(examples) < max_examples:
                examples.append({"seqLeft": a.get("seq"), "seqRight": b.get("seq"),
                                 "opcode": a.get("opcodeMasked"),
                                 "caller": a.get("caller"),
                                 "argsLeft": av, "argsRight": bv,
                                 "wrapper86CallerLeft": a.get("wrapper86Caller"),
                                 "wrapper86CallerRight": b.get("wrapper86Caller")})
    if len(commands_a) != len(commands_b):
        raw_equal = low_equal = False
    term_a = [e.get("terminalNativeState") for e in commands_a if _terminal(e)]
    term_b = [e.get("terminalNativeState") for e in commands_b if _terminal(e)]
    terminal_match = bool(term_a and term_b and term_a == term_b and
                          all(isinstance(x, dict) for x in term_a + term_b))
    checks = {
        "sameEventLayout": same_layout,
        "sameDeckShuffles": bool(sh_a and sh_b and sh_a == sh_b),
        "sameAiProbeEvents": bool(ai_a and ai_b and ai_a == ai_b),
        "sameExecutorEvents": ex_a == ex_b,
        "sameCommandStructure": same_structure,
        "sameCommandLow16": low_equal,
        "sameRawCommandArgs": raw_equal,
        "sameTerminalNativeState": terminal_match,
        "sameCeNativeStates": ce_a == ce_b,
        "same86WrapperCallers": wrapper86_a == wrapper86_b,
        "sameNonEntryReseeds": _reseeds(left) == _reseeds(right),
    }
    observed = all(v for k, v in checks.items() if k != "sameRawCommandArgs")
    return {
        "globalDuelIndex": index,
        "observedMatch": observed, "rawCommandArgsMatch": raw_equal,
        "status": ("diverged" if not observed else
                   "byte_equivalent_commands" if raw_equal else
                   "observed_match_with_raw_differences"),
        "checks": checks,
        "counts": {"eventsLeft": len(left), "eventsRight": len(right),
                   "aiProbeLeft": len(ai_a), "aiProbeRight": len(ai_b),
                   "commandsLeft": len(commands_a), "commandsRight": len(commands_b),
                   "shufflesLeft": len(sh_a), "shufflesRight": len(sh_b),
                   "ceSnapshotsLeft": sum(x is not None for x in ce_a),
                   "ceSnapshotsRight": sum(x is not None for x in ce_b),
                   "wrapper86CallersLeft": wrapper86_a,
                   "wrapper86CallersRight": wrapper86_b,
                   "changedRawCommands": changed_commands,
                   "high16OnlyChangedCommands": high_only_commands,
                   "highWordOrigins": dict(sorted(difference_origins.items())),
                   "verifiedRegisterResidueCommands": sum(
                       count for origin, count in difference_origins.items()
                       if origin.startswith("verified_")),
                   "unverifiedRawDifferenceCommands": sum(
                       count for origin, count in difference_origins.items()
                       if not origin.startswith("verified_"))},
        "fingerprints": {"leftShuffles": _stable_hash(sh_a),
                         "rightShuffles": _stable_hash(sh_b),
                         "leftAi": _stable_hash(ai_a),
                         "rightAi": _stable_hash(ai_b)},
        "rawHighWordDifferences": [
            {"opcode": opcode, "caller": caller, "argIndex": arg,
             "high16Left": high_a, "high16Right": high_b,
             "origin": origin, "count": count}
            for (opcode, caller, arg, high_a, high_b, origin), count in
            deltas.most_common(30)],
        "rawDifferenceExamples": examples,
        "note": ("Some changed high words are provably leftover pointer/register "
                 "bits at specific native callsites; others are unresolved. "
                 "Raw values remain intact. Matching observed checkpoints "
                 "does not establish identical full engine state or winner.")
    }


def compare_runs(left: Path, right: Path, max_examples: int = 8) -> dict:
    if max_examples < 0:
        raise ValueError("max_examples must be nonnegative")
    runs = [_sources(Path(left)), _sources(Path(right))]
    all_headers = [[cap.metadata for cap in run] for run in runs]
    first = [headers[0] for headers in all_headers]
    settings = lambda header: {
        "baseSeed": header["rngSeedOverride"]["baseSeed"],
        "algorithm": header["rngSeedOverride"]["algorithm"],
        "speedValue": header.get("speedValue"),
        "deckPlans": header.get("deckPlans")}
    if settings(first[0]) != settings(first[1]):
        raise ValueError("Incompatible base seed, algorithm, speed, or deck plans")
    for run in runs:
        if any(settings(source.metadata) != settings(run[0].metadata)
               for source in run):
            raise ValueError("Worker configurations differ within one run")
    if (runs[0][0].start != runs[1][0].start or
        runs[0][-1].start + runs[0][-1].count !=
        runs[1][-1].start + runs[1][-1].count):
        raise ValueError("Runs do not cover the same global duel index range")
    duel_results = []
    for a, b in zip_longest(_stream_duels(runs[0]), _stream_duels(runs[1])):
        if a is None or b is None or a[0] != b[0]:
            raise ValueError("Missing or misaligned global duel index")
        duel_results.append(_compare_duel(a[0], a[1], b[1], max_examples))
    good = bool(duel_results) and all(x["observedMatch"] for x in duel_results)
    raw = good and all(x["rawCommandArgsMatch"] for x in duel_results)
    unverified = sum(d["counts"]["unverifiedRawDifferenceCommands"]
                     for d in duel_results)
    return {"formatVersion": 2, "mode": "seeded-run-observed-comparison",
            "left": str(left), "right": str(right),
            "settings": {k: value for k, value in settings(first[0]).items()
                         if k != "deckPlans"},
            "totalDuelsCompared": len(duel_results),
            "observedMatch": good, "rawCommandArgsMatch": raw,
            "status": "diverged" if not good else
                      "raw_command_match" if raw else
                      "observed_match_with_raw_differences",
            "totalChangedRawCommands": sum(d["counts"]["changedRawCommands"]
                                           for d in duel_results),
            "totalUnverifiedRawDifferenceCommands": unverified,
            "highWordOrigins": dict(sorted(sum(
                (Counter(d["counts"]["highWordOrigins"]) for d in duel_results),
                Counter()).items())),
            "totalHigh16OnlyChangedCommands": sum(
                d["counts"]["high16OnlyChangedCommands"] for d in duel_results),
            "duels": duel_results,
            "warning": ("This compares observed hooks and explicit command fields, "
                        "not all game memory or decoded winners; high words "
                        "are kept as unknown raw differences.")}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("left", type=Path)
    parser.add_argument("right", type=Path)
    parser.add_argument("--output", type=Path, help="Write JSON comparison report")
    parser.add_argument("--max-examples", type=int, default=8)
    parser.add_argument("--strict-raw", action="store_true",
                        help="Fail when raw command words differ")
    parser.add_argument("--strict-unverified", action="store_true",
                        help="Fail if any changed command word has no proven native origin")
    args = parser.parse_args(argv)
    try:
        result = compare_runs(args.left, args.right, args.max_examples)
    except (OSError, ValueError, UnicodeError) as exc:
        print(f"[yugilab.compare] ERROR: {exc}", file=sys.stderr)
        return 2
    print(f"[yugilab.compare] {result['totalDuelsCompared']} duels; "
          f"observedMatch={result['observedMatch']}; "
          f"rawCommandArgsMatch={result['rawCommandArgsMatch']}; "
          f"{result['totalHigh16OnlyChangedCommands']} commands differ "
          "only in high words; "
          f"{result['totalUnverifiedRawDifferenceCommands']} unverified")
    for duel in result["duels"]:
        print(f"  globalIndex={duel['globalDuelIndex']}: {duel['status']}; "
              f"rawChanged={duel['counts']['changedRawCommands']}")
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False)
                               + "\n", encoding="utf-8")
        print(f"[yugilab.compare] Report: {args.output}")
    return 1 if (not result["observedMatch"] or
                 (args.strict_raw and not result["rawCommandArgsMatch"]) or
                 (args.strict_unverified and
                  result["totalUnverifiedRawDifferenceCommands"] > 0)) else 0


if __name__ == "__main__":
    raise SystemExit(main())
