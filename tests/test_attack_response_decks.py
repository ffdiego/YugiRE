"""Regression guards for the native P200/P192 attack-response deck presets.

Only the reviewed internal IDs are injected; all other cards come from
already versioned native demo-A/demo-B pools. This tests artifact integrity,
not whether the original AI will activate a given Trap or play optimally.
"""
from __future__ import annotations

import collections
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DECKS = ROOT / "native_hooks" / "decks"
FOLDER = DECKS / "attack-responses"
CATALOG = ROOT / "docs" / "evidence" / "card-callback-catalog.json"
PACKS = ROOT / "docs" / "cards" / "pack-index.json"

FOCUS = {
    "Magic Cylinder": (894, 1214, 4),
    "Negate Attack": (282, 960, 2),
    "Mirror Force": (467, 1056, 1),
}


class AttackResponseDeckChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.a = json.loads((DECKS / "demo-A.json").read_text(encoding="utf-8"))
        cls.b = json.loads((DECKS / "demo-B.json").read_text(encoding="utf-8"))

    def test_static_identity_and_availability(self):
        records = json.loads(CATALOG.read_text(encoding="utf-8"))["records"]
        by_resource = {r["resource_id"]: r for r in records}
        packs = {row[0]: row for row in json.loads(PACKS.read_text(encoding="utf-8"))["cards"]}
        for name, (idx, rid, expected_pack) in FOCUS.items():
            with self.subTest(name=name):
                self.assertTrue(any(
                    i["index"] == idx and i["name"] == name and i["class_code"] == 21
                    for i in by_resource[rid]["identities"]
                ))
                self.assertEqual(packs[rid][1], expected_pack)

    def test_preset_decks_follow_safe_native_format(self):
        expected = {
            "defense-cylinder.json": (3, 0, 0, 40),
            "defense-negate.json": (0, 3, 0, 40),
            "defense-mixed.json": (2, 2, 2, 40),
            "opponent-demo-B.json": (0, 0, 0, 43),
        }
        for filename, (cyl, negate, mirror, total) in expected.items():
            with self.subTest(preset=filename):
                deck = json.loads((FOLDER / filename).read_text(encoding="utf-8"))
                self.assertEqual(set(deck), {"main", "second", "third"})
                self.assertEqual(len(deck["main"]), total)
                self.assertLessEqual(len(deck["second"]), 15)
                self.assertLessEqual(len(deck["third"]), 15)
                for group in deck.values():
                    self.assertTrue(all(type(i) is int and 1 <= i <= 4095 for i in group))
                self.assertLessEqual(max(collections.Counter(deck["main"]).values()), 3)
                self.assertEqual(
                    tuple(deck["main"].count(i) for i in (894, 282, 467)),
                    (cyl, negate, mirror),
                )

    def test_only_targeted_slots_differ_from_previous_native_demo_decks(self):
        edits = {
            "defense-cylinder.json": {36: 894, 37: 894, 39: 894},
            "defense-negate.json": {36: 282, 37: 282, 39: 282},
            "defense-mixed.json": {
                33: 467, 34: 467, 36: 894, 37: 894, 38: 282, 39: 282,
            },
        }
        for filename, replacements in edits.items():
            with self.subTest(preset=filename):
                deck = json.loads((FOLDER / filename).read_text(encoding="utf-8"))
                expected_main = self.a["main"].copy()
                for offset, index in replacements.items():
                    expected_main[offset] = index
                self.assertEqual(deck["main"], expected_main)
                self.assertEqual(deck["second"], self.a["second"])
                self.assertEqual(deck["third"], self.a["third"])
        self.assertEqual(
            json.loads((FOLDER / "opponent-demo-B.json").read_text(encoding="utf-8")),
            self.b,
        )


if __name__ == "__main__":
    unittest.main()
