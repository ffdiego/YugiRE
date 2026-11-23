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


def analyze(native: Image.Image, yugire: Image.Image, threshold: int = 0) -> tuple[dict, Image.Image, Image.Image, Image.Image]:
    """Return JSON-friendly summary, raw abs-RGB diff, binary mask and 50/50 overlay."""
    if not 0 <= threshold <= 255:
        raise ValueError("threshold must be in [0,255]")
    if native.size != CANVAS or yugire.size != CANVAS:
        raise ValueError("Both frames must be exactly 800x600")
    if native.mode != "RGB" or yugire.mode != "RGB":
        raise ValueError("Pass RGB images; load_image validates alpha and converts inputs")
    difference = ImageChops.difference(native, yugire)
    mask = Image.new("RGB", CANVAS, (0, 0, 0))
    # One traversal for counts, bounds and region metrics; no image scaling.
    mismatch = 0
    max_channel_delta = 0
    bounds = [800, 600, -1, -1]
    region = {k: {"mismatchedPixels": 0, "totalPixels": (x1-x0)*(y1-y0)}
              for k, (x0, y0, x1, y1) in REGIONS.items()}
    mask_pixels: list[tuple[int, int, int]] = []
    pixel_bytes = difference.tobytes()
    for i in range(0, len(pixel_bytes), 3):
        r, g, b = pixel_bytes[i], pixel_bytes[i + 1], pixel_bytes[i + 2]
        delta = max(r, g, b)
        if delta > max_channel_delta:
            max_channel_delta = delta
        if delta > threshold:
            x, y = (i // 3) % 800, (i // 3) // 800
            mismatch += 1
            bounds[0] = min(bounds[0], x)
            bounds[1] = min(bounds[1], y)
            bounds[2] = max(bounds[2], x)
            bounds[3] = max(bounds[3], y)
            region["sidebar" if x < 230 else "board"]["mismatchedPixels"] += 1
            mask_pixels.append((255, 0, 0))
        else:
            mask_pixels.append((0, 0, 0))
    mask.putdata(mask_pixels)
    for data in region.values():
        data["mismatchPercent"] = round(100 * data["mismatchedPixels"] / data["totalPixels"], 6)
    pixel_total = CANVAS[0] * CANVAS[1]
    summary = {
        "schemaVersion": 1,
        "width": 800, "height": 600,
        "threshold": threshold,
        "totalPixels": pixel_total,
        "mismatchedPixels": mismatch,
        "mismatchPercent": round(100 * mismatch / pixel_total, 6),
        # Half-open rectangle convenient for deterministic crops.
        "mismatchBoundsExclusive": [bounds[0], bounds[1], bounds[2]+1, bounds[3]+1] if mismatch else None,
        "meanAbsoluteRgbChannelError": round(sum(ImageStat.Stat(difference).mean) / 3, 6),
        "maxChannelDelta": max_channel_delta,
        "regions": region,
        "limitations": "Pixel comparison only; assumes matching scene, perspective, update and 800x600 crop. The F12 YugiRE canvas excludes its window cursor.",
    }
    overlay = Image.blend(native, yugire, 0.5)
    return summary, difference, mask, overlay


def compare_files(native_path: Path, yugire_path: Path, output_dir: Path, threshold: int = 0) -> dict:
    src = [native_path.resolve(), yugire_path.resolve()]
    outputs = [(output_dir / filename).resolve() for filename in OUTPUT_FILES]
    if any(path in src for path in outputs):
        raise ValueError("Output would overwrite an input image")
    native = load_image(native_path)
    yugire = load_image(yugire_path)
    result, difference, mask, overlay = analyze(native, yugire, threshold)
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
    args = parser.parse_args(argv)
    try:
        result = compare_files(args.native, args.yugire, args.output, args.threshold)
    except (ValueError, OSError) as error:
        parser.error(str(error))
    print(f"{result['mismatchedPixels']}/{result['totalPixels']} pixels differ "
          f"({result['mismatchPercent']:.4f}%), max channel delta "
          f"{result['maxChannelDelta']}; {args.output / 'report.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

