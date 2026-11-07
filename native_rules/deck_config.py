"""Validate experimental demo deck files before sending them to Frida.

Each array contains Joey *internal 12-bit card indices*, not card resource IDs.
The experimental source memory layout is 80/15/15 ushort entries and three
ushort counts at 0xDC/0xDE/0xE0. This module never mutates the executable.
"""
from __future__ import annotations

import json
from pathlib import Path


def validate_deck(document: object) -> dict[str, list[int]]:
    if not isinstance(document, dict):
        raise ValueError("Deck JSON must be an object with main, second, third")
    if set(document) - {"main", "second", "third"}:
        raise ValueError("Unknown deck fields: " +
                         ", ".join(sorted(set(document) - {"main", "second", "third"})))
    if "main" not in document:
        raise ValueError("Deck JSON requires a main array")
    result: dict[str, list[int]] = {}
    for name, minimum, maximum in (("main", 40, 80),
                                    ("second", 0, 15),
                                    ("third", 0, 15)):
        values = document.get(name, [])
        if not isinstance(values, list) or not minimum <= len(values) <= maximum:
            raise ValueError(f"{name} must contain {minimum}..{maximum} cards")
        if any(type(value) is not int or not 1 <= value <= 0xFFF for value in values):
            raise ValueError(f"{name} must contain internal card indices 1..4095")
        result[name] = values.copy()
    return result


def read_deck_file(path: Path) -> dict[str, list[int]]:
    try:
        with path.expanduser().open(encoding="utf-8") as source:
            document = json.load(source)
    except (OSError, UnicodeError, json.JSONDecodeError) as exc:
        raise ValueError(f"Cannot load deck JSON {path}: {exc}") from exc
    try:
        return validate_deck(document)
    except ValueError as exc:
        raise ValueError(f"Invalid deck {path}: {exc}") from exc
