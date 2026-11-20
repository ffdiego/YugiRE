"""Offline synthetic checks: no original DAT, EXE, network or Frida required."""
import csv
import json
import tempfile
import unittest
from pathlib import Path

from scripts.card_playability import (
    CATALOG, MANIFEST, ROOT, build, kind_for, main,
    read_callbacks, read_manifest, report_markdown, save,
)


def row(index, resource, kind, pack):
    return {
        "internalIndex": index, "resourceId": resource, "name": f"Card {index}",
        "classCode": kind, "properties": kind << 20,
        "pack": pack, "attack": 0, "defense": 0, "level": 0,
    }


def fixture():
    return {"cards": [
        row(1, 342, 22, 7),    # Sparks Done in every game
        row(2, 332, 22, 7),    # Sogen Done
        row(3, 300, 22, 4),    # Legendary Sword Partial
        row(4, 1056, 21, 4),   # Mirror Force Trap Pending
        row(5, 3999, 15, 1),     # Generic Monster Pending
        row(6, 1427, 22, 0),  # Infinite Cards implemented, outside the scope
    ], "fingerprints": {"datSha256": "a" * 64}}


class CardPlayabilityTests(unittest.TestCase):
    def setUp(self):
        self.manifest = read_manifest(MANIFEST)
        self.callbacks = read_callbacks(CATALOG)

    def test_catalog_evidence_is_source_limited_and_unique(self):
        self.assertEqual(len(self.manifest), 315)
        self.assertEqual(len({v["id"] for v in self.manifest.values()}), 17)
        self.assertEqual({self.manifest[card]["status"] for card in (101, 427, 47, 573, 1108, 1110, 1117, 1119, 1120, 1123, 1114, 1115, 1116, 561, 610, 1109, 1122, 335, 336)}, {"Done"})
        self.assertEqual(self.manifest[332]["subtype"], "Field")
        self.assertEqual(self.manifest[300]["status"], "Partial")
        self.assertEqual(self.manifest[1427]["status"], "Done")
        self.assertEqual(self.callbacks[332], "P036")

    def test_main_type_identification(self):
        self.assertEqual([kind_for(c) for c in (0, 1, 15, 20, 21, 22, 23, 24)],
                         ["Other", "Monster", "Monster", "Monster", "Trap", "Spell", "Other", "Other"])

    def test_denominators_are_pack_indices_not_callback_counts(self):
        rows, summary = build(fixture(), self.manifest, self.callbacks)
        union = summary["byScope"]["all-packs"]
        joey = summary["byScope"]["joey"]
        self.assertEqual(summary["totalUnfiltered"], 6)
        self.assertEqual(union["total"], 5)
        self.assertEqual(joey["total"], 4)
        self.assertEqual(union["complete"], 2)
        self.assertEqual(union["completePct"], 40.0)
        self.assertEqual(union["statuses"],
                         {"Pending": 2, "Partial": 1, "Done": 2, "Verified": 0})
        self.assertEqual(joey["byKind"]["Trap"]["total"], 1)
        self.assertEqual(union["byKind"]["Monster"]["completePct"], 0.0)
        self.assertEqual(union["genericMonsterBasics"], 1)
        self.assertEqual(union["unclassifiedSubtypes"], 2)
        self.assertEqual(next(r for r in rows if r["resourceId"] == 300)["callbackProfile"], "P025")
        self.assertTrue(next(r for r in rows if r["resourceId"] == 3999)["basicMonsterMechanics"])
        self.assertNotIn("1427", [str(r["resourceId"]) for r in rows if r["packMask"] & 7])

    def test_report_and_csv_are_readable_without_original_dat(self):
        rows, summary = build(fixture(), self.manifest, self.callbacks)
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp) / "progress"
            save(out, rows, summary)
            self.assertIn("2/5 = 40.00%", (out / "PROGRESS.md").read_text(encoding="utf-8"))
            self.assertIn("Monstros", (out / "PROGRESS.md").read_text(encoding="utf-8"))
            with (out / "cards.csv").open(encoding="utf-8", newline="") as stream:
                records = list(csv.DictReader(stream))
            self.assertEqual(len(records), 6)
            self.assertEqual(records[0]["status"], "Done")
            self.assertEqual(records[3]["kind"], "Trap")
            self.assertEqual(len(json.loads((out / "cards.json").read_text(encoding="utf-8"))), 6)
            self.assertEqual(json.loads((out / "summary.json").read_text(encoding="utf-8"))["byScope"]["all-packs"]["complete"], 2)

    def test_cli_with_extracted_fixture(self):
        with tempfile.TemporaryDirectory() as tmp:
            extracted = Path(tmp) / "extracted.json"
            extracted.write_text(json.dumps(fixture()), encoding="utf-8")
            self.assertEqual(main(["--extracted", str(extracted), "--output", str(Path(tmp) / "out")]), 0)
            self.assertTrue((Path(tmp) / "out" / "PROGRESS.md").exists())

    def test_manifest_is_not_auto_applied_to_plus_2000_aliases(self):
        value = fixture()
        value["cards"].append(row(7, 2342, 22, 4))
        cards, summary = build(value, self.manifest, self.callbacks)
        self.assertEqual(next(r for r in cards if r["resourceId"] == 2342)["status"], "Pending")
        self.assertEqual(summary["byScope"]["joey"]["complete"], 2)

    def test_invalid_alias_and_mismatch_fail_closed(self):
        invalid = fixture()
        invalid["cards"][0]["classCode"] = 21
        with self.assertRaisesRegex(ValueError, "Class code inconsistent"):
            build(invalid, self.manifest, self.callbacks)
        invalid = fixture()
        invalid["cards"][0]["properties"] = 21 << 20
        invalid["cards"][0]["classCode"] = 21
        with self.assertRaisesRegex(ValueError, "Known classification mismatch"):
            build(invalid, self.manifest, self.callbacks)
        invalid = fixture()
        invalid["cards"].append(dict(invalid["cards"][0]))
        with self.assertRaisesRegex(ValueError, "Duplicate internal index"):
            build(invalid, self.manifest, self.callbacks)

    def test_no_bogus_percentage_for_empty_scope(self):
        extracted = {"cards": [row(1, 342, 22, 0)]}
        _, summary = build(extracted, self.manifest, self.callbacks)
        self.assertEqual(summary["byScope"]["all-packs"]["completePct"], 0.0)
        self.assertEqual(summary["byScope"]["all-packs"]["total"], 0)


if __name__ == "__main__":
    unittest.main()
