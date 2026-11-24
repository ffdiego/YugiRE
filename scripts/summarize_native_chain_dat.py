#!/usr/bin/env python3
"""Emit chain-only original DAT manifest text and image metadata (never pixels).

Input is the JSON produced by Joey.App --inspect-visual-dat; the tool does not
read the DAT itself. Names and manifest content remain unverified until run on
an original local archive. Keep output in scratch/ and do not commit it.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

PREFIX = "j\\duel\\chain\\"
EXPECTED = ("j\\duel\\chain\\chain.txt", "j\\duel\\chain\\disable.txt")


def canonical(name: str) -> str:
    return name.replace("/", "\\").lower()


def report_for(data: dict) -> dict:
    if data.get("schemaVersion") != 1:
        raise ValueError("Expected VisualManifestProbe schemaVersion=1")
    if not isinstance(data.get("manifests"), list) or not isinstance(data.get("images"), list):
        raise ValueError("Missing manifests/images arrays")
    manifests = sorted(
        [m for m in data["manifests"] if isinstance(m, dict) and
         isinstance(m.get("Name"), str) and canonical(m["Name"]).startswith(PREFIX)],
        key=lambda m: canonical(m["Name"]))
    images = sorted(
        [m for m in data["images"] if isinstance(m, dict) and
         isinstance(m.get("Name"), str) and canonical(m["Name"]).startswith(PREFIX)],
        key=lambda m: canonical(m["Name"]))
    if not manifests and not images:
        raise ValueError("No native duel chain DAT resources found")
    present = {canonical(m["Name"]) for m in manifests}
    return {
        "schemaVersion": 1,
        "source": "VisualManifestProbe / original DAT (chain metadata only)",
        "manifests": [
            {"name": m["Name"], "bytes": m.get("Bytes"), "latin1": m.get("Latin1")}
            for m in manifests
        ],
        "images": [
            {"name": m["Name"], "width": m.get("Width"), "height": m.get("Height"),
             "decodedBytes": m.get("DecodedBytes"), "error": m.get("Error")}
            for m in images
        ],
        "missingExpectedManifests": [name for name in EXPECTED if name not in present],
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path,
                        default=Path("scratch/visual-review/dat-manifests.json"))
    parser.add_argument("--output", type=Path,
                        default=Path("scratch/visual-review/chain-manifests.json"))
    args = parser.parse_args(argv)
    if args.input.resolve() == args.output.resolve():
        parser.error("--output must differ from --input")
    try:
        result = report_for(json.loads(args.input.read_text(encoding="utf-8")))
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n",
                               encoding="utf-8")
    except (OSError, ValueError, TypeError) as error:
        parser.error(str(error))
    print(f"{len(result['manifests'])} chain manifests, {len(result['images'])} images; "
          f"{len(result['missingExpectedManifests'])} expected manifests missing; {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
