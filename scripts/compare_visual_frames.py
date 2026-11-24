#!/usr/bin/env python3
"""Compare two ALIGNED 800x600 duel-canvas PNGs without altering either source.

The output is a diagnostic, not evidence that the native game and YugiRE were
at identical game states / presentation updates. Screenshots must already be
cropped to the *same* original canvas; this script does not resize or align.

Requires Pillow: python -m pip install Pillow
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageChops, ImageStat

CANVAS = (800, 600)
# DuelSurfaceLayout.Board: (230, 0, 570, 600); sidebar occupies the rest.
REGIONS = {
    "sidebar": (0, 0, 230, 600),
    "board": (230, 0, 800, 600),
}
OUTPUT_FILES = ("difference.png", "mismatch-mask.png", "overlay.png", "report.json")


def load_image(path: Path) -> Image.Image:
    with Image.open(path) as source:
        if source.format != "PNG":
            raise ValueError(f"Input must be PNG: {path}")
        if source.size != CANVAS:
            raise ValueError(f"Expected 800x600 canvas, got {source.size}: {path}")
        rgba = source.convert("RGBA")
        if rgba.getchannel("A").getextrema() != (255, 255):
            raise ValueError(f"Nonopaque pixels require explicit compositing first: {path}")
        return rgba.convert("RGB")


def mismatch_metrics(mask: Image.Image, x: int = 0, y: int = 0) -> dict:
    total = mask.width * mask.height
    mismatch = mask.histogram()[255]
    bounds = mask.getbbox()
    return {
        "totalPixels": total,
        "mismatchedPixels": mismatch,
        "mismatchPercent": round(100 * mismatch / total, 6),
        "mismatchBoundsExclusive": [bounds[0] + x, bounds[1] + y, bounds[2] + x, bounds[3] + y]
        if bounds else None,
    }


def analyze(native: Image.Image, yugire: Image.Image, threshold: int = 0, *,
            regions: dict[str, tuple[int, int, int, int]] | None = None
            ) -> tuple[dict, Image.Image, Image.Image, Image.Image]:
    """Return JSON-friendly summary, raw abs-RGB diff, binary mask and 50/50 overlay."""
    if not 0 <= threshold <= 255:
        raise ValueError("threshold must be in [0,255]")
    if native.size != CANVAS or yugire.size != CANVAS:
        raise ValueError("Both frames must be exactly 800x600")
    if native.mode != "RGB" or yugire.mode != "RGB":
        raise ValueError("Pass RGB images; load_image validates alpha and converts inputs")
    regions = {} if regions is None else regions
    for name, (x0, y0, x1, y1) in regions.items():
        if not name.strip() or name in REGIONS:
            raise ValueError(f"Invalid or reserved region name: {name!r}")
        if not (0 <= x0 < x1 <= CANVAS[0] and 0 <= y0 < y1 <= CANVAS[1]):
            raise ValueError(f"Region {name!r} must be a nonempty rectangle within the canvas")
    difference = ImageChops.difference(native, yugire)
    red, green, blue = difference.split()
    max_delta = ImageChops.lighter(ImageChops.lighter(red, green), blue)
    mismatches = max_delta.point([0] * (threshold + 1) + [255] * (255 - threshold))
    region = {}
    for name, box in (REGIONS | regions).items():
        region[name] = {"boundsExclusive": list(box), **mismatch_metrics(mismatches.crop(box), *box[:2])}
    summary = {
        "schemaVersion": 1,
        "width": CANVAS[0], "height": CANVAS[1],
        "threshold": threshold,
        **mismatch_metrics(mismatches),
        "meanAbsoluteRgbChannelError": round(sum(ImageStat.Stat(difference).mean) / 3, 6),
        "maxChannelDelta": max_delta.getextrema()[1],
        "regions": region,
        "limitations": "Pixel comparison only; assumes matching scene, perspective, update and 800x600 crop. The F12 YugiRE canvas excludes its window cursor.",
    }
    black = Image.new("L", CANVAS)
    mask = Image.merge("RGB", (mismatches, black, black))
    overlay = Image.blend(native, yugire, 0.5)
    return summary, difference, mask, overlay


def validate_output_paths(inputs: list[Path], outputs: list[Path]) -> None:
    # Resolve symlinks and compare existing inodes to catch hard links too.
    source_paths = {path.resolve() for path in inputs}
    source_inodes = set()
    for path in source_paths:
        if path.exists():
            stat = path.stat()
            source_inodes.add((stat.st_dev, stat.st_ino))
    output_paths = set()
    output_inodes = set()
    for output in outputs:
        resolved = output.resolve()
        if resolved in source_paths:
            raise ValueError(f"Output would overwrite an input: {output}")
        if resolved in output_paths:
            raise ValueError(f"Output would overwrite another output: {output}")
        output_paths.add(resolved)
        if output.exists():
            stat = output.stat()
            identity = (stat.st_dev, stat.st_ino)
            if identity in source_inodes:
                raise ValueError(f"Output would overwrite an input: {output}")
            if identity in output_inodes:
                raise ValueError(f"Output would overwrite another output: {output}")
            output_inodes.add(identity)


def compare_files(native_path: Path, yugire_path: Path, output_dir: Path, threshold: int = 0, *,
                  regions: dict[str, tuple[int, int, int, int]] | None = None) -> dict:
    validate_output_paths([native_path, yugire_path], [output_dir / name for name in OUTPUT_FILES])
    native = load_image(native_path)
    yugire = load_image(yugire_path)
    result, difference, mask, overlay = analyze(native, yugire, threshold, regions=regions)
    result["nativeFrame"] = str(native_path)
    result["yugireFrame"] = str(yugire_path)
    output_dir.mkdir(parents=True, exist_ok=True)
    difference.save(output_dir / "difference.png")
    mask.save(output_dir / "mismatch-mask.png")
    overlay.save(output_dir / "overlay.png")
    (output_dir / "report.json").write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return result


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("native", type=Path, help="Cropped original Joey 800x600 PNG")
    parser.add_argument("yugire", type=Path, help="YugiRE F12 canvas 800x600 PNG")
    parser.add_argument("--output", type=Path, default=Path("scratch/visual-review/comparison"))
    parser.add_argument("--threshold", type=int, default=0,
                        help="Compare max per-channel difference > threshold (0..255)")
    parser.add_argument("--region", action="append", nargs=5, metavar=("NAME", "X0", "Y0", "X1", "Y1"),
                        default=[], help="Include a named region with exclusive right/bottom edges (repeatable)")
    parser.add_argument("--max-mismatched-pixels", type=int,
                        help="Return exit code 1 when the reported mismatch count exceeds this limit")
    args = parser.parse_args(argv)
    try:
        if args.max_mismatched_pixels is not None and args.max_mismatched_pixels < 0:
            raise ValueError("Mismatch limit must be nonnegative")
        regions = {}
        for name, *coords in args.region:
            if name in regions:
                raise ValueError(f"Duplicate region name: {name!r}")
            regions[name] = tuple(map(int, coords))
        result = compare_files(args.native, args.yugire, args.output, args.threshold, regions=regions)
    except (ValueError, OSError) as error:
        parser.error(str(error))
    print(f"{result['mismatchedPixels']}/{result['totalPixels']} pixels differ "
          f"({result['mismatchPercent']:.4f}%), max channel delta "
          f"{result['maxChannelDelta']}; {args.output / 'report.json'}")
    if args.max_mismatched_pixels is not None and result["mismatchedPixels"] > args.max_mismatched_pixels:
        print(f"Mismatch count exceeds limit of {args.max_mismatched_pixels} pixels")
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

