#!/usr/bin/env python3
"""Summarize original duel/card/card.txt sprite-bank indices from DAT metadata.

Input: Joey.App --inspect-visual-dat JSON (private, user-owned). This script
never reads, exports, or renders any original image pixels. The produced
report contains only manifest resource names, indices and image dimensions.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

MANIFEST = r"j\duel\card\card.txt"
PREFIX = "j\\duel\\card\\"
INDEX_ROLES = {
    0: "card-focus-small", 1: "card-focus-large",
    2: "card-focus-small-alternate", 3: "card-focus-large-alternate",
    4: "modified-attribute-icon", 5: "modified-type-icon",
    6: "field-status-A", 7: "field-status-A-transition-tail",
    8: "field-status-B", 9: "field-status-B-transition-tail",
    10: "attack-defense-increase-flash-A", 11: "attack-defense-increase-flash-B",
    12: "attack-defense-decrease-flash-A", 13: "attack-defense-decrease-flash-B",
}
FIRST_FOUR = (
    "card_waku#s.bmp", "card_waku#l.bmp",
    "card_waku#s2.bmp", "card_waku#l2.bmp",
)


def canonical(value: str) -> str:
    return value.replace("/", "\\").lower()


def manifest_records(text: str) -> list[tuple[str, list[str]]]:
    result: list[tuple[str, list[str]]] = []
    for line in text.splitlines():
        words = line.split("//", 1)[0].split()
        if not words:
            continue
        name = words[0].replace("/", "\\")
        # Native resource lists have one bitmap filename per non-comment
        # record, optionally followed by coordinates or other numeric data.
        if name.lower().endswith((".bmp", ".yga")):
            result.append((name, words[1:]))
    return result


def report_for(data: dict) -> dict:
    if data.get("schemaVersion") != 1:
        raise ValueError("Expected VisualManifestProbe schemaVersion=1")
    manifests = data.get("manifests")
    images = data.get("images")
    if not isinstance(manifests, list) or not isinstance(images, list):
        raise ValueError("Missing manifests/images arrays")

    hits = [entry for entry in manifests if isinstance(entry, dict) and
            isinstance(entry.get("Name"), str) and
            canonical(entry["Name"]) == MANIFEST]
    if len(hits) != 1 or not isinstance(hits[0].get("Latin1"), str):
        raise ValueError("Expected exactly one native j/duel/card/card.txt manifest")
    records = manifest_records(hits[0]["Latin1"])
    names = [name for name, _ in records]
    if len(names) < len(INDEX_ROLES):
        raise ValueError(f"Card sprite manifest has {len(names)} images, expected >=14")
    # These four positions were previously confirmed by card Draw
    # 00418EF0. If misaligned, stop rather than mislabel sprites #4..#13.
    if tuple(n.split("\\")[-1].lower() for n in names[:4]) != FIRST_FOUR:
        raise ValueError("Card sprite manifest index anchors 0..3 disagree with 00418EF0")

    image_by_name: dict[str, dict] = {}
    for image in images:
        if not isinstance(image, dict) or not isinstance(image.get("Name"), str):
            continue
        key = canonical(image["Name"])
        if key in image_by_name:
            raise ValueError(f"Duplicate image metadata path {key}")
        image_by_name[key] = image

    items = []
    missing = []
    for index, (name, auxiliary) in enumerate(records):
        relative = canonical(name)
        path = relative if relative.startswith(PREFIX) else PREFIX + relative.split("\\")[-1]
        entry = image_by_name.get(path)
        if entry is None or entry.get("Error") is not None or not all(
                isinstance(entry.get(k), int) and entry[k] > 0 for k in ("Width", "Height")):
            missing.append({"index": index, "resource": path})
        items.append({"index": index, "role": INDEX_ROLES.get(index, "unknown"),
                      "resource": path,
                      "auxiliaryFields": auxiliary,
                      "width": entry.get("Width") if entry else None,
                      "height": entry.get("Height") if entry else None})

    return {"schemaVersion": 1,
            "source": "VisualManifestProbe / original DAT (card status metadata only)",
            "manifest": MANIFEST,
            "entries": items,
            "missingImageMetadata": missing,
            "indexAnchorsVerified": True}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path,
                        default=Path("scratch/visual-review/dat-manifests.json"))
    parser.add_argument("--output", type=Path,
                        default=Path("scratch/visual-review/card-status-manifests.json"))
    args = parser.parse_args(argv)
    if args.input.resolve() == args.output.resolve():
        parser.error("--output must differ from --input")
    try:
        report = report_for(json.loads(args.input.read_text(encoding="utf-8")))
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n",
                               encoding="utf-8")
    except (ValueError, TypeError, OSError) as error:
        parser.error(str(error))
    print(f"{len(report['entries'])} card bank entries, "
          f"{len(report['missingImageMetadata'])} missing image metadata; {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
