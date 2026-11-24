"""Tests for the visual native-Draw coverage badge, independent of DAT/EXE."""
import json
import unittest

from scripts.publish_visual_coverage import generated, inventory_draws, load, INVENTORY


class VisualCoverageTests(unittest.TestCase):
    def test_manifest_covers_unique_native_draw_entries(self):
        rows = load()
        known = inventory_draws(INVENTORY.read_text(encoding="utf-8"))
        self.assertEqual({r["address"] for r in rows}, known)
        self.assertEqual(len(rows), len(known))
        self.assertEqual(sum(r["address"] == "00418EF0" for r in rows), 1)
        self.assertIn("004514B0", known)
        self.assertNotIn("00480500", known)

    def test_generated_badge_is_checked_in(self):
        outputs, stats = generated()
        rows = load()
        covered = sum(row["status"] != "Pending" for row in rows)
        verified = sum(row["status"] == "Verified" for row in rows)
        total = len(rows)
        self.assertEqual((stats["covered"], stats["total"], stats["verified"]), (covered, total, verified))
        for path, expected in outputs.items():
            with self.subTest(path=path):
                self.assertTrue(path.is_file())
                self.assertEqual(path.read_text(encoding="utf-8"), expected)
        badge = json.loads(next(x for p, x in outputs.items() if p.name == "visual.json"))
        self.assertEqual(badge["label"], "VISUALS")
        self.assertEqual(badge["message"], f"{covered}/{total} Draw ({100 * covered / total:.2f}%)")


if __name__ == "__main__":
    unittest.main()
