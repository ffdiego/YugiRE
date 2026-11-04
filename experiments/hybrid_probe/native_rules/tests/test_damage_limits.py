"""Checks the Python side of high-damage argument validation."""
from __future__ import annotations

import argparse
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
from patch_dual_rules import damage_type, MODES  # noqa: E402


class DamageLimitsTests(unittest.TestCase):
    def test_effect_mode_constants(self):
        self.assertEqual(MODES, {"damage": 1, "draw": 2, "damage-draw": 3})
        self.assertEqual(len(set(MODES.values())), len(MODES))

    def test_valid_values(self):
        for raw in ("1", "200", "300", "400", "8000", "9999"):
            with self.subTest(raw=raw):
                self.assertEqual(damage_type(raw), int(raw))

    def test_invalid_values(self):
        for raw in ("-5", "0", "10000", "99999", "2.5", "abc", ""):
            with self.subTest(raw=raw):
                with self.assertRaises(argparse.ArgumentTypeError):
                    damage_type(raw)


if __name__ == "__main__":
    unittest.main()
