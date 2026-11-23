"""Synthetic tests; no original DAT/image assets required."""
from __future__ import annotations

import importlib.util
import unittest
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "summarize_native_card_status_dat.py"
spec = importlib.util.spec_from_file_location("summarize_native_card_status_dat", SCRIPT)
assert spec and spec.loader
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)


def fixture() -> dict:
    names = [*mod.FIRST_FOUR, *[f"sprite_{n}.bmp" for n in range(4, 15)]]
    data = {"schemaVersion": 1, "manifests": [
        {"Name": "J/DUEL/CARD/CARD.TXT",
         "Latin1": "// original comment\n" + "\n".join(
             f"{name} 000000 // descriptor" for name in names)}],
        "images": [{"Name": f"j\\duel\\card\\{name}",
                    "Width": 10 + i, "Height": 20 + i, "Error": None}
                   for i, name in enumerate(names)]}
    return data


class CardStatusManifestTests(unittest.TestCase):
    def test_manifest_bank_indices_and_dimensions(self) -> None:
        report = mod.report_for(fixture())
        self.assertEqual(len(report["entries"]), 15)
        self.assertEqual(report["entries"][4]["role"], "modified-attribute-icon")
        self.assertEqual(report["entries"][5]["role"], "modified-type-icon")
        self.assertEqual(report["entries"][6]["role"], "field-status-A")
        self.assertEqual(report["entries"][8]["role"], "field-status-B")
        self.assertEqual(report["entries"][13]["role"],
                         "attack-defense-decrease-flash-B")
        self.assertEqual(report["entries"][13]["width"], 23)
        self.assertEqual(report["missingImageMetadata"], [])
        self.assertTrue(report["indexAnchorsVerified"])

    def test_missing_image_metadata_is_explicit(self) -> None:
        data = fixture()
        data["images"] = data["images"][:7]
        report = mod.report_for(data)
        self.assertEqual(len(report["missingImageMetadata"]), 8)
        self.assertIsNone(report["entries"][12]["width"])

    def test_bad_index_anchor_fails_closed(self) -> None:
        data = fixture()
        data["manifests"][0]["Latin1"] = data["manifests"][0]["Latin1"].replace(
            "card_waku#s.bmp", "not_native.bmp")
        with self.assertRaisesRegex(ValueError, "index anchors"):
            mod.report_for(data)

    def test_wrong_source_rejected(self) -> None:
        with self.assertRaisesRegex(ValueError, "schemaVersion"):
            mod.report_for({"schemaVersion": 2})
        data = fixture()
        data["manifests"] = []
        with self.assertRaisesRegex(ValueError, "exactly one"):
            mod.report_for(data)


if __name__ == "__main__":
    unittest.main()
