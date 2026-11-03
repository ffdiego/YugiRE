"""JoeyTrace: group read-only observations around operator markers.

These windows are time proximity, not proof of causality. YDR commands cannot
be aligned by millisecond because the YDR records have no event timestamps.
"""
from __future__ import annotations

from collections import Counter, defaultdict
import json
from pathlib import Path


def _lines(path: Path) -> list[dict]:
    if not path.is_file():
        return []
    return [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines()
            if line.strip()]


def _card_indexes(changes: list[dict]) -> list[int]:
    indexes: set[int] = set()
    for change in changes:
        if change.get("type") in ("monster_identity_changed", "hand_cards_changed"):
            for key in ("before", "after"):
                value = change.get(key)
                if isinstance(value, dict) and isinstance(value.get("card_index"), int):
                    indexes.add(value["card_index"])
            for key in ("added", "removed"):
                for item in change.get(key, []):
                    if isinstance(item.get("card_index"), int):
                        indexes.add(item["card_index"])
        for key in ("before_card_index", "after_card_index", "card_index"):
            if isinstance(change.get(key), int):
                indexes.add(change[key])
        if change.get("type", "").startswith("field_slot_"):
            for key in ("before", "after"):
                if change.get("type") == "field_slot_card_index_changed" and \
                        isinstance(change.get(key), int):
                    indexes.add(change[key])
    return sorted(x for x in indexes if x > 0)


def generate_effect_windows(folder: Path, *, before_ms: int = 3000,
                            after_ms: int = 20000,
                            max_events: int = 80) -> Path:
    """Summarize observed state changes near every marker.

    Never infer that the marked card caused these changes; the operator may
    mark an event late and multiple unrelated actions can share the window.
    """
    if before_ms < 0 or after_ms < 0 or max_events < 1:
        raise ValueError("Intervalos devem ser >= 0 e max_events >= 1")
    folder = Path(folder)
    if not (folder / "manifest.json").is_file():
        raise ValueError(f"Sessao invalida: {folder}")
    changes = _lines(folder / "state-changes.jsonl")
    markers = [x for x in _lines(folder / "markers.jsonl")
               if isinstance(x.get("marker_index"), int)]
    notes: dict[int, list[str]] = defaultdict(list)
    for item in _lines(folder / "marker-annotations.jsonl"):
        index = item.get("marker_index")
        if isinstance(index, int) and isinstance(item.get("note"), str):
            notes[index].append(item["note"])
    windows = []
    for marker in markers:
        point = int(marker["elapsed_ms"])
        nearby = [e for e in changes
                  if point - before_ms <= int(e["elapsed_ms"]) <= point + after_ms]
        kinds = Counter(c.get("type", "untyped")
                        for e in nearby for c in e.get("changes", []))
        indexes = _card_indexes([c for e in nearby for c in e.get("changes", [])])
        windows.append({
            "marker_index": marker["marker_index"],
            "marker_elapsed_ms": point,
            "segment_id": marker.get("segment_id"),
            "operator_notes": ([marker["note"]] if marker.get("note") else []) +
                              notes[marker["marker_index"]],
            "source": marker.get("source"),
            "window_start_ms": max(0, point - before_ms),
            "window_end_ms": point + after_ms,
            "total_batches": len(nearby),
            "shown_batches": min(len(nearby), max_events),
            "truncated": len(nearby) > max_events,
            "change_type_counts": dict(sorted(kinds.items())),
            "card_indexes_observed": indexes,
            "observed_batches": [{
                "seq": e.get("seq"),
                "elapsed_ms": e["elapsed_ms"],
                "relative_to_marker_ms": int(e["elapsed_ms"]) - point,
                "segment_id": e.get("segment_id"),
                "changes": e.get("changes", []),
            } for e in nearby[:max_events]],
        })
    payload = {
        "schema": "joeytrace-effect-windows-v1",
        "interpretation_limit": "Time-adjacent observations; no card causality proven.",
        "replay_alignment": "No per-command YDR timestamps. No millisecond alignment.",
        "before_ms": before_ms,
        "after_ms": after_ms,
        "max_events_per_marker": max_events,
        "marker_count": len(windows),
        "windows": windows,
    }
    output = folder / "effect-windows.json"
    output.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
                      encoding="utf-8")
    return output
