"""End-to-end checks for the public GitHub progress badges (no DAT needed)."""
import json
import unittest

from scripts.publish_playability import generated, load, counts
from scripts.card_playability import build, read_manifest, read_callbacks, read_subtypes, MANIFEST, CATALOG


class PublishedPlayabilityTests(unittest.TestCase):
    def test_pack_subset_and_progress(self):
        cards, status, subtypes, sources = load()
        all_cards = counts(cards, status, subtypes, 7)
        joey = counts(cards, status, subtypes, 4)
        self.assertEqual((len(cards), all_cards["total"], joey["total"]), (771, 771, 350))
        self.assertEqual((all_cards["done"], all_cards["pct"]), (311, 40.34))
        self.assertEqual((joey["done"], joey["pct"]), (97, 27.71))
        self.assertEqual(all_cards["bykind"]["Monster"]["total"], 499)
        self.assertEqual(all_cards["bykind"]["Spell"]["total"], 172)
        self.assertEqual(all_cards["bykind"]["Trap"]["total"], 100)
        self.assertEqual(all_cards["bykind"]["Spell"]["Partial"], 3)
        self.assertEqual(all_cards["bykind"]["Spell"]["Done"], 25)
        self.assertEqual(all_cards["bykind"]["Monster"]["Done"], 286)

    def test_public_subtypes_are_explicit_not_playability(self):
        cards, status, subtypes, sources = load()
        all_cards = counts(cards, status, subtypes, 7)
        self.assertEqual(all_cards["bysubtype"], {
            "Normal": 264, "Effect": 193, "Fusion": 34, "Ritual": 8, "ToReview": 0
        })
        self.assertEqual(sum(all_cards["bysubtype"].values()), 499)
        self.assertEqual(status.get(500), "Done")  # Man-Eater Bug FLIP playable in sandbox
        self.assertEqual(status.get(590), "Done")  # Morphing Jar FLIP playable in sandbox
        self.assertEqual(status.get(601), "Done")  # Penguin Soldier FLIP target return
        self.assertEqual(status.get(540), "Done")  # Hane-Hane mandatory single return
        self.assertEqual(status.get(1163), "Done")  # Morphing Jar #2 excavation
        self.assertEqual(status.get(332), "Done")
        self.assertEqual(len(sources), 6)

    def test_generated_badges_and_markdown_checked_in(self):
        files, values = generated()
        self.assertEqual(len(files), 3)
        for path, expected in files.items():
            with self.subTest(path=path):
                self.assertTrue(path.is_file(), str(path))
                self.assertEqual(path.read_text(encoding="utf-8"), expected)
        one = json.loads(next(content for path, content in files.items()
                              if path.name == "all-packs.json"))
        self.assertEqual(one["schemaVersion"], 1)
        self.assertEqual(one["message"], "311/771 (40.34%)")

    def test_card_csv_classifier_keeps_generic_monster_pending(self):
        card = {"internalIndex": 1060, "resourceId": 1405,
                "name": "Revival Jam", "classCode": 14,
                "properties": 14 << 20, "pack": 4}
        rows, summary = build({"cards": [card]}, read_manifest(MANIFEST),
                              read_callbacks(CATALOG), read_subtypes())
        self.assertEqual(rows[0]["subtype"], "Effect")
        self.assertEqual(rows[0]["subtypeSource"], "public-reference")
        self.assertEqual(rows[0]["status"], "Pending")
        self.assertTrue(rows[0]["basicMonsterMechanics"])
        self.assertEqual(summary["byScope"]["joey"]["complete"], 0)


if __name__ == "__main__":
    unittest.main()
