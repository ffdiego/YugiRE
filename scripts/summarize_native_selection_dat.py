#!/usr/bin/env python3
"""Emit a compact duel-selection subset from Joey.App --inspect-visual-dat JSON."""
from __future__ import annotations
import argparse
import json
from pathlib import Path

PREFIXES = ("j\\duel\\list\\", "j\\duel\\list_icon\\")
EXPECTED = [f"j\\duel\\list\\{family}{side}.txt"
            for family in ("selectlist", "gravelist", "fusionlist", "excludelist")
            for side in (1, 2)]


def in_scope(name: str) -> bool:
    return name.replace("/", "\\").lower().startswith(PREFIXES)


def report_for(data: dict) -> dict:
    if data.get("schemaVersion") != 1:
        raise ValueError("Expected VisualManifestProbe schemaVersion=1")
    if not isinstance(data.get("manifests"), list) or not isinstance(data.get("images"), list):
        raise ValueError("Missing manifests/images arrays")
    manifests = sorted(
        [m for m in data["manifests"] if isinstance(m, dict) and
         isinstance(m.get("Name"), str) and in_scope(m["Name"])],
        key=lambda m: m["Name"].lower())
    images = sorted(
        [m for m in data["images"] if isinstance(m, dict) and
         isinstance(m.get("Name"), str) and in_scope(m["Name"])],
        key=lambda m: m["Name"].lower())
    if not manifests and not images:
        raise ValueError("No native duel selection DAT resources found")
    present = {m["Name"].replace("/", "\\").lower() for m in manifests}
    return {
        "schemaVersion": 1,
        "source": "VisualManifestProbe / original DAT (selected metadata only)",
        "manifests": [
            {"name": m["Name"], "bytes": m.get("Bytes"), "latin1": m.get("Latin1")}
            for m in manifests
        ],
        "images": [
            {"name": m["Name"], "width": m.get("Width"), "height": m.get("Height"),
             "decodedBytes": m.get("DecodedBytes"), "error": m.get("Error")}
            for m in images
        ],
        "missingExpectedListManifests": [p for p in EXPECTED if p.lower() not in present],
    }


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--input", type=Path,
                   default=Path("scratch/visual-review/dat-manifests.json"))
    p.add_argument("--output", type=Path,
                   default=Path("scratch/visual-review/selection-manifests.json"))
    args = p.parse_args(argv)
    if args.input.resolve() == args.output.resolve():
        p.error("--output must differ from --input")
    try:
        source = json.loads(args.input.read_text(encoding="utf-8"))
        report = report_for(source)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n",
                               encoding="utf-8")
    except (OSError, ValueError, TypeError) as e:
        p.error(str(e))
    print(f"{len(report['manifests'])} manifests, {len(report['images'])} images; "
          f"{len(report['missingExpectedListManifests'])} expected manifests missing; "
          f"{args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
