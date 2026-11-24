#!/usr/bin/env python3
"""Publish ACTIONS roadmap milestones without game files."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
PLAN = ROOT / "PLANS.md"
BADGE = ROOT / "docs/architecture/badges/actions.json"
TASK = re.compile(r"^- \[([ xX])\] \*\*(A\d{2,}) — .+?\*\*.*?Status: `([^`]+)`", re.MULTILINE)
CANDIDATE = re.compile(r"^- \[.\] \*\*A\d+", re.MULTILINE)


def milestones(text: str) -> tuple[int, int]:
    """Count unique numbered ACTIONS items; never count Partial as complete."""
    start = "### Itens da trilha ACTIONS"
    end = "## Trilha CARDS"
    if start not in text or end not in text:
        raise ValueError("ACTIONS section missing")
    section = text.split(start, 1)[1].split(end, 1)[0]
    rows = TASK.findall(section)
    if not rows or len(rows) != len(CANDIDATE.findall(section)):
        raise ValueError("Missing or malformed ACTIONS milestone")
    seen = set()
    done = 0
    for checked, item_id, status in rows:
        if item_id in seen:
            raise ValueError(f"Duplicate milestone: {item_id}")
        seen.add(item_id)
        if (checked.lower() == "x") != (status == "Concluído"):
            raise ValueError(f"Checkbox/status disagreement: {item_id}")
        done += checked.lower() == "x"
    return done, len(rows)


def generated(text: str) -> str:
    done, total = milestones(text)
    pct = 100 * done / total
    badge = {"schemaVersion": 1, "label": "ACTIONS",
             "message": f"{done}/{total} marcos ({pct:.2f}%)",
             "color": "brightgreen" if pct >= 70 else "blue" if pct >= 20 else "orange"}
    return json.dumps(badge, indent=2, ensure_ascii=False) + "\n"


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args(argv)
    expected = generated(PLAN.read_text(encoding="utf-8"))
    if args.check:
        if not BADGE.is_file() or BADGE.read_text(encoding="utf-8") != expected:
            parser.error("Generated ACTIONS badge out of date")
    else:
        BADGE.parent.mkdir(parents=True, exist_ok=True)
        BADGE.write_text(expected, encoding="utf-8")
    print(json.loads(expected)["message"])
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
