"""Synthetic tests for chain-only DAT metadata extraction (no game assets)."""
import importlib.util
from pathlib import Path
import unittest

SOURCE = Path(__file__).resolve().parents[1] / "summarize_native_chain_dat.py"
spec = importlib.util.spec_from_file_location("chain_dat", SOURCE)
chain_dat = importlib.util.module_from_spec(spec)
spec.loader.exec_module(chain_dat)


class ChainDatTests(unittest.TestCase):
    def test_filters_and_preserves_manifest_text_without_image_pixels(self):
        data = {
            "schemaVersion": 1,
            "manifests": [
                {"Name": "J/DUEL/CHAIN/DISABLE.TXT", "Bytes": 3, "Latin1": "a\nb"},
                {"Name": "j\\duel\\chain\\chain.txt", "Bytes": 2, "Latin1": "x"},
                {"Name": "j\\duel\\list\\selectlist.txt", "Bytes": 2, "Latin1": "z"},
            ],
            "images": [
                {"Name": "j\\duel\\chain\\sprite01.yga", "Width": 12,
                 "Height": 34, "DecodedBytes": 100, "Sha256": "secret"},
                {"Name": "j\\duel\\other\\sprite02.yga", "Width": 4, "Height": 4},
            ],
        }
        report = chain_dat.report_for(data)
        self.assertEqual(len(report["manifests"]), 2)
        self.assertEqual(report["missingExpectedManifests"], [])
        self.assertEqual(report["manifests"][1]["latin1"], "a\nb")
        self.assertEqual(report["images"], [{"name": "j\\duel\\chain\\sprite01.yga",
                                             "width": 12, "height": 34,
                                             "decodedBytes": 100, "error": None}])
        self.assertNotIn("Sha256", repr(report))

    def test_missing_manifest_is_explicit_not_inferred(self):
        report = chain_dat.report_for({"schemaVersion": 1, "manifests": [],
                                      "images": [{"Name": "j\\duel\\chain\\a.yga"}]})
        self.assertEqual(len(report["missingExpectedManifests"]), 2)

    def test_rejects_invalid_or_unrelated_report(self):
        for value in ({"schemaVersion": 2, "manifests": [], "images": []},
                      {"schemaVersion": 1, "images": []},
                      {"schemaVersion": 1, "manifests": [], "images": []}):
            with self.subTest(value=value), self.assertRaises(ValueError):
                chain_dat.report_for(value)


if __name__ == "__main__":
    unittest.main()
