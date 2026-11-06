"""Compare timing of passive YugiRE demo traces without changing Joey.

Usage: python scripts/analyze_demo_timing.py commands-*.jsonl [...]

Timestamps are wall-clock milliseconds recorded by Frida, not game ticks.
Only compare 0x0001 -> 0x0050 pairs within the same turn. The first pair
is kept separate because both observed traces had a different startup path.
"""
from __future__ import annotations

import argparse
from collections import Counter
import json
from pathlib import Path
from statistics import median


def summarize(path: Path) -> dict:
    with path.open(encoding="utf-8") as source:
        rows = [json.loads(line) for line in source if line.strip()]
    if not rows or rows[0].get("kind") != "metadata":
        raise ValueError(f"{path}: first line must be trace metadata")
    metadata = rows[0]
    events = rows[1:]
    commands = [event for event in events if event.get("kind") == "command"]
    starts = [event for event in commands if event.get("opcodeMasked") == "0x0001"]
    fifties = [event for event in commands if event.get("opcodeMasked") == "0x0050"]
    pairs = []
    for i, start in enumerate(starts):
        limit = starts[i + 1]["seq"] if i + 1 < len(starts) else float("inf")
        match = next((event for event in fifties if
                      start["seq"] < event["seq"] < limit), None)
        if match is None:
            continue
        pairs.append({"startSeq": start["seq"],
                      "nextSeq": match["seq"],
                      "elapsedMs": match["timestampMs"] - start["timestampMs"]})
    elapsed = [pair["elapsedMs"] for pair in pairs]
    warm = elapsed[1:]
    initiations = [event for event in events if event.get("kind") == "demo_init"
                   and event.get("function") == "scene_config"
                   and event.get("phase") == "enter"
                   and event.get("requestedSceneMode") == 7]
    t0 = events[0].get("timestampMs") if events else None
    return {
        "file": str(path), "events": len(events),
        "overwritten": metadata.get("overwritten"),
        "opcodeCounts": dict(Counter(event.get("opcodeMasked") for event in commands)),
        "firstToLastEventMs": events[-1]["timestampMs"] - t0 if t0 is not None else None,
        "firstDemoConfigSinceTraceMs": (
            initiations[0]["timestampMs"] - t0 if t0 is not None and initiations else None),
        "turnStarts": len(starts), "pairedStarts": len(pairs),
        "turnStartTo50Ms": elapsed,
        "firstPairMs": elapsed[0] if elapsed else None,
        "warmPairMedianMs": median(warm) if warm else None,
        "warmPairMinMs": min(warm) if warm else None,
        "warmPairMaxMs": max(warm) if warm else None,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("files", nargs="+", type=Path)
    parser.add_argument("--json", action="store_true", help="Print structured JSON")
    args = parser.parse_args(argv)
    try:
        summaries = [summarize(path) for path in args.files]
    except (OSError, ValueError, KeyError, json.JSONDecodeError) as exc:
        parser.error(str(exc))
    if args.json:
        print(json.dumps(summaries, ensure_ascii=False, indent=2))
        return 0
    for s in summaries:
        print(f"{s['file']}: {s['events']} events, {s['turnStarts']} turn starts, "
              f"{s['pairedStarts']} start->0x0050 pairs, "
              f"overwritten={s['overwritten']}")
        print(f"  first pair: {s['firstPairMs']} ms; "
              f"subsequent median: {s['warmPairMedianMs']} ms "
              f"(min={s['warmPairMinMs']} max={s['warmPairMaxMs']})")
        print(f"  intervals (ms): {s['turnStartTo50Ms']}")
    if len(summaries) == 2 and all(x["warmPairMedianMs"] for x in summaries):
        a, b = summaries
        print(f"Relative median ({Path(b['file']).name} / "
              f"{Path(a['file']).name}): "
              f"{b['warmPairMedianMs'] / a['warmPairMedianMs']:.3f}x")
        print("Note: differing demos are not a controlled speed benchmark.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
