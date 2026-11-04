"""Compare two Joey YDR files by initial card lists and raw 8-byte commands.

Purpose: A/B regression check after an experimental native callback replacement.
An exact match is strong evidence about *recorded command streams only*.
Different AI decisions, RNG, or player decisions can cause differences even if
a substitute is correct. A replay played back from disk can bypass card logic.
The parser is exploratory and tied to the known YugiRE YDR layout.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys

_SCRIPT_DIR = Path(__file__).resolve().parents[2] / "scripts"
sys.path.insert(0, str(_SCRIPT_DIR))
from inspect_joey_replay import read_file  # noqa: E402

_FIELDS = ("raw_opcode_hex", "argument_1", "argument_2", "argument_3")


def compare_ydr(before: Path, after: Path) -> dict:
    a, meta_a = read_file(before)
    b, meta_b = read_file(after)
    decks_a = [
        [g["resource_ids"] for g in side["groups"]]
        for side in meta_a["deck_groups"]
    ]
    decks_b = [
        [g["resource_ids"] for g in side["groups"]]
        for side in meta_b["deck_groups"]
    ]
    same_decks = decks_a == decks_b
    mismatch = None
    prefix = 0
    for x, y in zip(a, b):
        if any(x[field] != y[field] for field in _FIELDS):
            mismatch = {"record_index": prefix,
                        "before": {k: x[k] for k in _FIELDS},
                        "after": {k: y[k] for k in _FIELDS}}
            break
        prefix += 1
    if mismatch is None and len(a) != len(b):
        mismatch = {"record_index": prefix,
                    "before": {k: a[prefix][k] for k in _FIELDS} if prefix < len(a) else None,
                    "after": {k: b[prefix][k] for k in _FIELDS} if prefix < len(b) else None}
    return {
        "schema": "joey-ydr-command-compare-v1",
        "same_initial_card_lists": same_decks,
        "same_command_stream": mismatch is None,
        "before_commands": len(a),
        "after_commands": len(b),
        "matching_prefix_records": prefix,
        "first_difference": mismatch,
        "before_opcode_counts": meta_a["opcode_counts"],
        "after_opcode_counts": meta_b["opcode_counts"],
        "caveat": "YDR command equivalence is not callback equivalence or full-state equivalence. "
                  "For A/B comparisons, play live duels with controlled inputs; "
                  "playing an existing YDR may bypass the effects being tested.",
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("before", type=Path, help="YDR saved from an original live duel")
    parser.add_argument("after", type=Path, help="YDR saved from a patched live duel")
    parser.add_argument("--json", type=Path, help="Optional report path")
    args = parser.parse_args()
    try:
        report = compare_ydr(args.before, args.after)
    except (ValueError, OSError, OverflowError) as exc:
        parser.exit(2, f"YDR comparison failed: {exc}\n")
    output = json.dumps(report, ensure_ascii=False, indent=2)
    print(output)
    if args.json:
        args.json.write_text(output + "\n", encoding="utf-8")
    return 0 if report["same_initial_card_lists"] and report["same_command_stream"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
