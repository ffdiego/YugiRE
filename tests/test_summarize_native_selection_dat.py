"""DAT selection metadata tests; does not require data.dat."""
import unittest
from scripts.summarize_native_selection_dat import EXPECTED, in_scope, report_for


class NativeSelectionDatTests(unittest.TestCase):
    def test_restricted_scope(self):
        self.assertTrue(in_scope(r"j\duel\list\selectlist1.txt"))
        self.assertTrue(in_scope("J/DUEL/LIST_ICON/LIST_BOARD.BMP"))
        self.assertFalse(in_scope(r"j\list\list_scene.txt"))
        self.assertFalse(in_scope(r"j\duel\listing\other.txt"))

    def test_records_text_and_dimensions_without_pixels(self):
        data = {"schemaVersion": 1,
                "manifests": [
                    {"Name": r"j\duel\list\selectlist2.txt", "Latin1": "2", "Hex": "32"},
                    {"Name": r"j\duel\list\selectlist1.txt", "Latin1": "1", "Hex": "31"},
                    {"Name": r"j\list\other.txt", "Latin1": "skip"}],
                "images": [
                    {"Name": r"j\duel\list\sele_box.bmp",
                     "Width": 24, "Height": 30, "Sha256": "a"},
                    {"Name": r"j\list\card_waku.bmp", "Width": 99}]}
        output = report_for(data)
        self.assertEqual(len(output["manifests"]), 2)
        self.assertTrue(output["manifests"][0]["name"].endswith("selectlist1.txt"))
        self.assertEqual(output["images"][0]["width"], 24)
        self.assertNotIn("Hex", str(output))
        self.assertNotIn("Sha256", str(output))
        self.assertEqual(len(output["missingExpectedListManifests"]), len(EXPECTED)-2)

    def test_invalid_input_is_not_silent(self):
        with self.assertRaises(ValueError):
            report_for({"schemaVersion": 2, "manifests": [], "images": []})
        with self.assertRaises(ValueError):
            report_for({"schemaVersion": 1, "manifests": [], "images": []})


if __name__ == "__main__":
    unittest.main()
