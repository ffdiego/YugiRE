#!/usr/bin/env python3
"""Inspect native LP digit manifests/atlases from Joey.App --inspect-visual-dat.

Produces only the original number.txt text and BMP dimensions: no game pixels.
A decoded resource's identity and the native on-screen draw scale require
independent verification; these are *not* implied by an atlas name.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

MANIFEST = r"j\duel\font\number.txt"
TARGETS = ("duel_fontl.bmp", "duel_fontm_y.bmp", "damage00.bmp", "damage01.bmp")


def normalize(name: str) -> str:
    return name.replace("/", "\\").lower()


def descriptor_rows(text: str) -> list[dict]:
    rows = []
    for n, raw in enumerate(text.splitlines(), start=1):
        before_comment = raw.split("//", 1)[0].strip()
        words = before_comment.split()
        if not words:
            continue
        columns = None
        if len(words) > 1 and words[1].isdigit():
            candidate = int(words[1])
            if 10 <= candidate <= 12:
                columns = candidate
        rows.append({
            "line": n,
            "resource": words[0],
            "arguments": words[1:],
            # Matches the existing NativeLifeHud.TryFontColumns heuristic.
            "clientParsedColumns": columns,
        })
    return rows


def report_for(data: dict) -> dict:
    if data.get("schemaVersion") != 1:
        raise ValueError("Expected VisualManifestProbe schemaVersion=1")
    manifests, images = data.get("manifests"), data.get("images")
    if not isinstance(manifests, list) or not isinstance(images, list):
        raise ValueError("Expected manifests and images arrays")
    found = [m for m in manifests if isinstance(m, dict) and
             isinstance(m.get("Name"), str) and normalize(m["Name"]) == MANIFEST]
    if len(found) != 1 or not isinstance(found[0].get("Latin1"), str):
        raise ValueError(
            "number.txt not found: use the FULL dat-manifests.json "
            "from --inspect-visual-dat (not selection-manifests.json)")
    text = found[0]["Latin1"]
    rows = descriptor_rows(text)
    bitmaps = {
        normalize(m["Name"]): m for m in images if isinstance(m, dict) and
        isinstance(m.get("Name"), str)
    }
    fonts = []
    for name in TARGETS:
        full = normalize(r"j\duel\font" + "\\" + name)
        matching = [r for r in rows if normalize(r["resource"]).split("\\")[-1] == name]
        meta = bitmaps.get(full)
        columns = matching[0]["clientParsedColumns"] if matching else None
        width = meta.get("Width") if meta else None
        height = meta.get("Height") if meta else None
        valid = isinstance(width, int) and width > 0 and isinstance(height, int) and height > 0
        divisible = valid and columns is not None and width % columns == 0
        fonts.append({
            "resource": name,
            "presentInManifest": bool(matching),
            "descriptorArguments": matching[0]["arguments"] if matching else None,
            "clientParsedColumns": columns,
            "width": width,
            "height": height,
            "imageError": meta.get("Error") if meta else "missing metadata",
            "sourceGlyphWidth": width // columns if divisible else None,
            "glyphSizeAt32pxHeight": [
                round((width // columns) * 32 / height, 3), 32
            ] if divisible else None,
            "clientWouldDrawAsBitmap": bool(divisible),
        })
    # Exact current DatGraphics.Open policy: large atlas, then medium-yellow.
    chosen = next((f for f in fonts[:2] if f["clientParsedColumns"] is not None), None)
    return {
        "schemaVersion": 1,
        "source": "VisualManifestProbe metadata from user's original data.dat",
        "manifestName": found[0]["Name"],
        "numberTxt": text,
        "descriptors": rows,
        "atlases": fonts,
        "currentClientSelection": chosen["resource"] if chosen else None,
        "currentClientCanDrawBitmap": chosen["clientWouldDrawAsBitmap"] if chosen else False,
        "notes": [
            "clientParsedColumns reproduces the current heuristic, not a proven native descriptor grammar",
            "32px display height is a client estimate, not native draw evidence",
            "floating damage uses a separate animation and is not sized by the LP counter",
        ],
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path,
                        default=Path("scratch/visual-review/dat-manifests.json"))
    parser.add_argument("--output", type=Path,
                        default=Path("scratch/visual-review/life-font-evidence.json"))
    args = parser.parse_args(argv)
    if args.input.resolve() == args.output.resolve():
        parser.error("--input and --output must differ")
    try:
        report = report_for(json.loads(args.input.read_text(encoding="utf-8")))
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n",
                               encoding="utf-8")
    except (ValueError, OSError, TypeError) as exc:
        parser.error(str(exc))
    print(f"LP manifest: {report['manifestName']}")
    for entry in report["atlases"]:
        print(f"  {entry['resource']}: {entry['width']}x{entry['height']} "
              f"columns={entry['clientParsedColumns']} "
              f"sourceGlyphWidth={entry['sourceGlyphWidth']}")
    print(f"Current client selects: {report['currentClientSelection']} "
          f"(bitmap valid={report['currentClientCanDrawBitmap']})")
    print(f"Report: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
