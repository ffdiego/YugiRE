"""Offline tests for experimental demo deck validation (no Joey / Frida needed)."""
from __future__ import annotations

import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from deck_config import read_deck_file, validate_deck


class DeckConfigTests(unittest.TestCase):
    def test_bundled_presets_are_valid(self):
        decks = Path(__file__).resolve().parents[1] / "decks"
        for name, count in (("demo-A.json", 40), ("demo-B.json", 43),
                            ("demo-C.json", 41)):
            with self.subTest(preset=name):
                deck = read_deck_file(decks / name)
                self.assertEqual(len(deck["main"]), count)
                self.assertTrue(all(1 <= i <= 4095 for i in deck["main"]))

    def test_defaults_optional_sections_to_empty(self):
        result = validate_deck({"main": [42] * 40})
        self.assertEqual(result, {"main": [42] * 40, "second": [], "third": []})

    def test_rejects_invalid_indices_and_structure(self):
        good = [42] * 40
        invalid = [
            None, [], {"second": [3]},
            {"main": [42] * 39}, {"main": [42] * 81},
            {"main": [True] * 40}, {"main": [0] * 40},
            {"main": [4096] * 40}, {"main": ["42"] * 40},
            {"main": good, "second": [42] * 16},
            {"main": good, "third": [-1]},
            {"main": good, "extras": []},
        ]
        for deck in invalid:
            with self.subTest(deck=str(deck)[:60]), self.assertRaises(ValueError):
                validate_deck(deck)

    def test_rejects_malformed_json(self):
        with tempfile.TemporaryDirectory() as directory:
            filename = Path(directory) / "invalid.json"
            filename.write_text('{"main": [', encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Cannot load"):
                read_deck_file(filename)

    def test_round_trip_deck_file(self):
        with tempfile.TemporaryDirectory() as directory:
            filename = Path(directory) / "deck.json"
            expected = {"main": [17] * 40, "second": [20], "third": []}
            filename.write_text(json.dumps(expected), encoding="utf-8")
            self.assertEqual(read_deck_file(filename), expected)


if __name__ == "__main__":
    unittest.main()
