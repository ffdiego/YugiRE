#!/usr/bin/env python3
"""Export a bounded native YDR slice to the existing YugiRE console script format.

Strict: only known ordinary draw and LP records; no skipping unknown commands.
The setup script is an explicit checkpoint supplied by the operator, not inferred
from the deck inventory. Preserve source record indices; never export save ID.
"""
from __future__ import annotations
import argparse
import collections
import hashlib
import json
from pathlib import Path
from inspect_joey_replay import read_file


def project_record(record: dict) -> list[str]:
    raw = int(record["raw_opcode_hex"], 16)
    side = raw >> 15
    opcode = raw & 0x0FFF
    a, b, c = (record[f"argument_{i}"] for i in (1, 2, 3))
    # Do not collapse upper flags into side/opcode.
    if raw & 0x7000:
        raise ValueError("unmapped upper flags")
    if opcode in (0x42, 0x43) and a > 0 and b == 0 and c == 1:
        # 005787F0 and 005784E0: ordinary signed LP producers.
        return [f"{'heal' if opcode == 0x42 else 'damage'} {a} {side}"]
    if opcode == 0x61 and c == 0 and (a, b) in ((0, 5), (1, 1)):
        # 005B9F50 initial five; 00578860 common one-card draw.
        # Does not cover special draw routes, terminal behavior of a short deck.
        return [f"draw {b} {side}"]
    raise ValueError(f"unsupported record 0x{raw:04X} ({a}, {b}, {c})")


def project_slice(records: list[dict], start: int, count: int) -> list[str]:
    if start < 0 or count < 1 or start + count > len(records):
        raise ValueError("slice is outside replay")
    result = []
    for record in records[start:start + count]:
        try:
            commands = project_record(record)
        except ValueError as exc:
            raise ValueError(f"record {record['record_index']}: {exc}") from exc
        result.append(f"# native record {record['record_index']} {record['offset_hex']} {record['raw_opcode_hex']}")
        result.extend(commands)
    return result


def build_script(records, summary, source_hash: str, start: int, count: int,
                 checkpoint: str) -> str:
    if not checkpoint.strip():
        raise ValueError("explicit nonempty checkpoint script required")
    # Validate entire slice before producing any output.
    projected = project_slice(records, start, count)
    lines = [
        "# YugiRE scenario from native replay: partial command projection",
        f"# YDR SHA256 {source_hash}",
        f"# Native range [{start}, {start + count}) / {len(records)} records",
        "# Checkpoint supplied by operator; exact native state NOT inferred or verified.",
        "# Commands reproduce selected recorded operations, NOT the original rule decisions.",
        checkpoint.rstrip(), "state", *projected, "diff",
    ]
    return "\n".join(lines) + "\n"


def opening_checkpoint(summary: dict, observed: dict) -> str:
    """Build initial setup only from independently observed top-to-bottom deck IDs.

    Input keys 0 and 1 are explicit arrays. Group 0 is the candidate main
    inventory; require exact multiset agreement. Do not silently drop other
    groups: they may include a fusion deck, which the console cannot set yet.
    """
    if not isinstance(observed, dict) or set(observed) != {"0", "1"}:
        raise ValueError("observed order must contain exactly keys '0' and '1'")
    commands = []
    for side, entry in enumerate(summary["deck_groups"]):
        groups = entry["groups"]
        order = observed[str(side)]
        if not isinstance(order, list) or any(type(v) is not int or not 0 < v <= 65535 for v in order):
            raise ValueError(f"side {side}: invalid observed resource IDs")
        if len(order) > 80 or len(order) < 5:
            raise ValueError(f"side {side}: outside supported deck size")
        if any(g["count"] for g in groups[1:]):
            raise ValueError(f"side {side}: non-main lists are not supported by opening setup")
        if collections.Counter(order) != collections.Counter(groups[0]["resource_ids"]):
            raise ValueError(f"side {side}: observed order does not match YDR inventory")
        commands.extend(f"spawn {resource} {side} deck" for resource in order)
    return "\n".join(commands) + "\n"


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("replay", type=Path)
    parser.add_argument("--start", type=int, required=True, help="zero-based native record")
    parser.add_argument("--count", type=int, required=True)
    setup = parser.add_mutually_exclusive_group(required=True)
    setup.add_argument("--checkpoint", type=Path, help="explicit console setup script for pre-slice state")
    setup.add_argument("--opening-order", type=Path,
                       help="independently observed top-to-bottom resource IDs JSON; opening draw only")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args(argv)
    try:
        records, summary = read_file(args.replay)
        if args.opening_order:
            # The initial pair appears after the 0x14/0x10 opening commands.
            # Do not skip arbitrary stateful records to reach a later draw.
            if args.start != 2 or args.count != 2 or len(records) < 4:
                raise ValueError("opening-order supports exactly --start 2 --count 2")
            if [int(r["raw_opcode_hex"],16) for r in records[:2]] != [0x14, 0x10] or \
                {int(r["raw_opcode_hex"],16) for r in records[2:4]} != {0x61,0x8061} or \
                any((r["argument_1"],r["argument_2"],r["argument_3"]) != (0,5,0) for r in records[2:4]):
                raise ValueError("unrecognized opening prefix")
            checkpoint = opening_checkpoint(summary,json.loads(args.opening_order.read_text(encoding="utf-8")))
        else:
            checkpoint = args.checkpoint.read_text(encoding="utf-8")
        result = build_script(records,summary,hashlib.sha256(args.replay.read_bytes()).hexdigest(),
                              args.start,args.count,checkpoint)
        # Never overwrite replay, checkpoint, or previous output.
        with args.output.open("x",encoding="utf-8",newline="\n") as out:
            out.write(result)
    except (OSError, ValueError, KeyError, TypeError, OverflowError) as exc:
        parser.exit(2, f"replay scenario: {exc}\n")
    print(f"Exported {args.count} native records to {args.output}; checkpoint fidelity not verified.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
