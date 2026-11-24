"""Native LP DAT extraction: synthetic metadata only, no copyrighted image data."""
import unittest
from scripts.inspect_native_life_dat import descriptor_rows, report_for


def image(name, w, h):
    return {"Name": r"j\duel\font" + "\\" + name, "Width": w,
            "Height": h, "Sha256": "do-not-copy", "Error": None}


class NativeLifeDatTests(unittest.TestCase):
    def sample(self, manifest: str) -> dict:
        return {
            "schemaVersion": 1,
            "manifests": [{"Name": r"j\duel\font\number.txt", "Latin1": manifest,
                           "Hex": "do-not-copy"}],
            "images": [image("duel_fontl.bmp", 240, 16),
                       image("duel_fontm_y.bmp", 120, 12),
                       image("damage00.bmp", 144, 18),
                       image("damage01.bmp", 144, 18)],
        }

    def test_primary_atlas_selected_and_dimensions_calculated(self):
        data = self.sample("duel_fontl.bmp 12 // atlas\n"
                           "duel_fontm_y.bmp 10\n"
                           "damage00.bmp 12\ndamage01.bmp 12\n")
        result = report_for(data)
        self.assertEqual(result["currentClientSelection"], "duel_fontl.bmp")
        self.assertTrue(result["currentClientCanDrawBitmap"])
        self.assertEqual(result["atlases"][0]["sourceGlyphWidth"], 20)
        self.assertEqual(result["atlases"][0]["glyphSizeAt32pxHeight"], [40.0, 32])
        self.assertEqual(result["atlases"][2]["sourceGlyphWidth"], 12)
        self.assertNotIn("Sha256", str(result))
        self.assertNotIn("Hex", str(result))

    def test_unknown_primary_falls_back_without_inventing_columns(self):
        data = self.sample("duel_fontl.bmp X\n"
                           "duel_fontm_y.bmp 10\n")
        result = report_for(data)
        self.assertEqual(result["currentClientSelection"], "duel_fontm_y.bmp")
        self.assertIsNone(result["atlases"][0]["clientParsedColumns"])

    def test_divisibility_and_metadata_warnings(self):
        data = self.sample("duel_fontl.bmp 12\n")
        data["images"][0]["Width"] = 239
        self.assertFalse(report_for(data)["currentClientCanDrawBitmap"])

    def test_malformed_and_reduced_reports_fail_loudly(self):
        with self.assertRaisesRegex(ValueError, "FULL"):
            report_for({"schemaVersion": 1, "manifests": [], "images": []})
        with self.assertRaisesRegex(ValueError, "schemaVersion"):
            report_for({"schemaVersion": 2, "manifests": [], "images": []})
        self.assertEqual(descriptor_rows("// nothing"), [])


if __name__ == "__main__":
    unittest.main()
