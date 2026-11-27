#!/usr/bin/env python3
"""Compare explicit pairs of aligned frames and summarize pixel and update deltas."""
from __future__ import annotations

import argparse
from dataclasses import dataclass
import json
from pathlib import Path
import re

from compare_visual_frames import CANVAS, OUTPUT_FILES, REGIONS, compare_files, validate_output_paths


@dataclass(frozen=True)
class FrameRegion:
    bounds: tuple[int, int, int, int]
    max_mismatched_pixels: int | None


@dataclass(frozen=True)
class FramePair:
    name: str
    reference: Path
    candidate: Path
    reference_update: int
    candidate_update: int
    threshold: int
    max_mismatched_pixels: int | None
    max_update_delta: int | None
    regions: dict[str, FrameRegion]


def nonnegative_integer(value: object, label: str, maximum: int | None = None) -> int:
    if type(value) is not int or value < 0 or (maximum is not None and value > maximum):
        limit = "a nonnegative integer" if maximum is None else f"an integer in [0,{maximum}]"
        raise ValueError(f"{label} must be {limit}")
    return value


def read_regions(value: object, pair_name: str) -> dict[str, FrameRegion]:
    if not isinstance(value, dict):
        raise ValueError(f"{pair_name}.regions must be an object")
    regions = {}
    for name, region in value.items():
        label = f"{pair_name}.regions.{name}"
        if not name.strip() or name in REGIONS:
            raise ValueError(f"{label}: invalid or reserved region name")
        if not isinstance(region, dict):
            raise ValueError(f"{label} must be an object")
        bounds = region.get("boundsExclusive")
        if not isinstance(bounds, list) or len(bounds) != 4 or any(type(n) is not int for n in bounds):
            raise ValueError(f"{label}.boundsExclusive must contain four integer coordinates")
        x0, y0, x1, y1 = bounds
        if not (0 <= x0 < x1 <= CANVAS[0] and 0 <= y0 < y1 <= CANVAS[1]):
            raise ValueError(f"{label}.boundsExclusive must be a nonempty rectangle within the canvas")
        limit = None
        if "maxMismatchedPixels" in region:
            limit = nonnegative_integer(region["maxMismatchedPixels"], f"{label}.maxMismatchedPixels")
        regions[name] = FrameRegion((x0, y0, x1, y1), limit)
    return regions


def read_pairs(manifest: Path) -> list[FramePair]:
    data = json.loads(manifest.read_text(encoding="utf-8"))
    if not isinstance(data, dict) or type(data.get("schemaVersion")) is not int or data["schemaVersion"] != 1:
        raise ValueError("Expected a matrix with schemaVersion 1")
    entries = data.get("pairs")
    if not isinstance(entries, list) or not entries:
        raise ValueError("The matrix must contain at least one frame pair")
    pairs = []
    names = set()
    for entry in entries:
        if not isinstance(entry, dict):
            raise ValueError("Each frame pair must be an object")
        name = entry.get("id")
        if not isinstance(name, str) or not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_-]*", name):
            raise ValueError("Frame pair IDs must use letters, digits, hyphens or underscores")
        if name.casefold() in names:
            raise ValueError(f"Duplicate frame pair ID: {name}")
        names.add(name.casefold())
        paths = []
        for key in ("reference", "candidate"):
            raw_path = entry.get(key)
            if not isinstance(raw_path, str) or not raw_path.strip():
                raise ValueError(f"{name}.{key} must be a PNG path")
            paths.append(manifest.parent / raw_path)
        pairs.append(FramePair(
            name, *paths,
            nonnegative_integer(entry.get("referenceUpdate"), f"{name}.referenceUpdate"),
            nonnegative_integer(entry.get("candidateUpdate"), f"{name}.candidateUpdate"),
            nonnegative_integer(entry.get("threshold", 0), f"{name}.threshold", 255),
            nonnegative_integer(entry["maxMismatchedPixels"], f"{name}.maxMismatchedPixels")
            if "maxMismatchedPixels" in entry else None,
            nonnegative_integer(entry["maxUpdateDelta"], f"{name}.maxUpdateDelta")
            if "maxUpdateDelta" in entry else None,
            read_regions(entry.get("regions", {}), name),
        ))
    return pairs


def compare_matrix(manifest: Path, output: Path) -> dict:
    pairs = read_pairs(manifest)
    report_path = output / "matrix-report.json"
    inputs = [manifest] + [frame for pair in pairs for frame in (pair.reference, pair.candidate)]
    outputs = [report_path] + [output / pair.name / name for pair in pairs for name in OUTPUT_FILES]
    validate_output_paths(inputs, outputs)
    results = []
    for pair in pairs:
        diff = compare_files(
            pair.reference, pair.candidate, output / pair.name, pair.threshold,
            regions={name: region.bounds for name, region in pair.regions.items()},
        )
        update_delta = pair.candidate_update - pair.reference_update
        failed_limits = []
        if pair.max_mismatched_pixels is not None and diff["mismatchedPixels"] > pair.max_mismatched_pixels:
            failed_limits.append("maxMismatchedPixels")
        if pair.max_update_delta is not None and abs(update_delta) > pair.max_update_delta:
            failed_limits.append("maxUpdateDelta")
        result = {
            "id": pair.name,
            "report": f"{pair.name}/report.json",
            "referenceUpdate": pair.reference_update,
            "candidateUpdate": pair.candidate_update,
            "updateDelta": update_delta,
            "threshold": pair.threshold,
            "mismatchedPixels": diff["mismatchedPixels"],
            "maxMismatchedPixels": pair.max_mismatched_pixels,
            "maxUpdateDelta": pair.max_update_delta,
            "failedLimits": failed_limits,
        }
        if pair.regions:
            result["regions"] = {}
            for name, region in pair.regions.items():
                metrics = diff["regions"][name]
                limit = region.max_mismatched_pixels
                if limit is not None and metrics["mismatchedPixels"] > limit:
                    failed_limits.append(f"regions.{name}.maxMismatchedPixels")
                result["regions"][name] = {**metrics, "maxMismatchedPixels": limit}
        results.append(result)
    result = {
        "schemaVersion": 1,
        "manifest": str(manifest),
        "pairsOverLimit": sum(bool(pair["failedLimits"]) for pair in results),
        "pairs": results,
    }
    report_path.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return result


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("manifest", type=Path)
    parser.add_argument("--output", type=Path, default=Path("scratch/visual-review/matrix"))
    args = parser.parse_args(argv)
    try:
        result = compare_matrix(args.manifest, args.output)
    except (ValueError, OSError) as error:
        parser.error(str(error))
    print(f"{result['pairsOverLimit']}/{len(result['pairs'])} pairs exceed a limit. "
          f"See {args.output / 'matrix-report.json'}")
    return int(result["pairsOverLimit"] > 0)


if __name__ == "__main__":
    raise SystemExit(main())
