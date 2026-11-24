"""Paired no-Gravity-Bind control decks for the native cohort 11 experiment.

All seven OTHER target cards stay 3x on BOTH sides, at the exact same
positions. The three Gravity Bind occurrences are replaced one-for-one
with previously tested historical controls, whose counts rise from 2 to 3.
These tests check card inclusion, not proof of native causality.
"""
from __future__ import annotations

from collections import Counter
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
DECKS = ROOT / "native_hooks/decks/mass-96"
GRAVITY = 244
REPLACEMENTS = [151, 241, 525]
OTHER_TARGETS = [307, 1060, 258, 543, 910, 362, 895]


class Cohort11NoGravityControlTests(unittest.TestCase):
    def test_control_replaces_only_gravity_in_both_decks(self):
        for side in ("A", "B"):
            with self.subTest(side=side):
                original = json.loads(
                    (DECKS / f"cohort-11-{side}.json").read_text(encoding="utf-8")
                )
                control = json.loads(
                    (DECKS / "controls" /
                     f"cohort-11-no-gravity-{side}.json").read_text(encoding="utf-8")
                )
                self.assertEqual(set(control), {"main", "second", "third"})
                self.assertEqual(control["second"], original["second"])
                self.assertEqual(control["third"], original["third"])
                self.assertEqual(len(original["main"]), 40)
                self.assertEqual(len(control["main"]), 40)
                self.assertEqual(original["main"].count(GRAVITY), 3)
                self.assertEqual(control["main"].count(GRAVITY), 0)
                self.assertEqual([idx for idx, (a, b) in enumerate(
                    zip(original["main"], control["main"])) if a != b],
                    [idx for idx, a in enumerate(original["main"]) if a == GRAVITY])
                expected = original["main"].copy()
                replacements = iter(REPLACEMENTS)
                expected = [next(replacements) if x == GRAVITY else x for x in expected]
                self.assertEqual(control["main"], expected)
                counts = Counter(control["main"])
                for id in OTHER_TARGETS:
                    self.assertEqual(counts[id], 3)
                for id in REPLACEMENTS:
                    self.assertEqual(counts[id], 3)
                self.assertLessEqual(max(counts.values()), 3)
                self.assertTrue(all(type(i) is int and 1 <= i <= 4095
                                    for i in control["main"]))

    def test_both_sides_still_mirror_same_card_multiset(self):
        def main(side):
            return json.loads(
                (DECKS / "controls" /
                 f"cohort-11-no-gravity-{side}.json").read_text(encoding="utf-8")
            )["main"]
        self.assertEqual(Counter(main("A")), Counter(main("B")))
        self.assertNotEqual(main("A"), main("B"))


if __name__ == "__main__":
    unittest.main()
