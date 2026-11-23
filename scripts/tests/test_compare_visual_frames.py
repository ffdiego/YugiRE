"""Synthetic pixel-level tests; do not require original game assets."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
from PIL import Image

SOURCE = Path(__file__).resolve().parents[1] / "compare_visual_frames.py"
spec = importlib.util.spec_from_file_location("visual_diff", SOURCE)
visual_diff = importlib.util.module_from_spec(spec)
spec.loader.exec_module(visual_diff)


class VisualDiffTests(unittest.TestCase):
    def test_equal_images(self):
        img = Image.new("RGB", (800, 600), (12, 24, 36))
        result, difference, mask, overlay = visual_diff.analyze(img, img)
        self.assertEqual(result["mismatchedPixels"], 0)
        self.assertEqual(result["mismatchBoundsExclusive"], None)
        self.assertEqual(result["maxChannelDelta"], 0)
        self.assertEqual(result["meanAbsoluteRgbChannelError"], 0)
        self.assertEqual(result["regions"]["sidebar"]["mismatchedPixels"], 0)
        self.assertEqual(difference.getpixel((1, 1)), (0, 0, 0))
        self.assertEqual(mask.getpixel((1, 1)), (0, 0, 0))
        self.assertEqual(overlay.getpixel((1, 1)), (12, 24, 36))

    def test_exact_changes_and_sidebar_boundary(self):
        left = Image.new("RGB", (800, 600), (0, 0, 0))
        right = left.copy()
        right.putpixel((229, 0), (1, 0, 0))
        right.putpixel((230, 599), (100, 0, 0))
        result, _, mask, _ = visual_diff.analyze(left, right)
        self.assertEqual(result["mismatchedPixels"], 2)
        self.assertEqual(result["mismatchBoundsExclusive"], [229, 0, 231, 600])
        self.assertEqual(result["regions"]["sidebar"]["mismatchedPixels"], 1)
        self.assertEqual(result["regions"]["board"]["mismatchedPixels"], 1)
        self.assertEqual(result["maxChannelDelta"], 100)
        self.assertEqual(mask.getpixel((230, 599)), (255, 0, 0))
        self.assertEqual(mask.getpixel((231, 599)), (0, 0, 0))
        threshold, _, threshold_mask, _ = visual_diff.analyze(left, right, threshold=1)
        self.assertEqual(threshold["mismatchedPixels"], 1)
        self.assertEqual(threshold["mismatchBoundsExclusive"], [230, 599, 231, 600])
        self.assertEqual(threshold_mask.getpixel((229, 0)), (0, 0, 0))

    def test_validates_geometry_format_alpha_and_output(self):
        with tempfile.TemporaryDirectory() as root:
            p = Path(root)
            img = Image.new("RGB", (800, 600), (3, 3, 3))
            img.save(p / "native.png")
            img.save(p / "yugire.png")
            summary = visual_diff.compare_files(p / "native.png", p / "yugire.png", p / "out")
            self.assertEqual(summary["mismatchedPixels"], 0)
            for name in visual_diff.OUTPUT_FILES:
                self.assertTrue((p / "out" / name).is_file(), name)
            img.save(p / "difference.png")
            with self.assertRaisesRegex(ValueError, "overwrite"):
                visual_diff.compare_files(p / "difference.png", p / "yugire.png", p)
            with self.assertRaisesRegex(ValueError, "threshold"):
                visual_diff.analyze(img, img, 256)
            wrong = Image.new("RGB", (799, 600))
            wrong.save(p / "wrong.png")
            with self.assertRaisesRegex(ValueError, "800x600"):
                visual_diff.load_image(p / "wrong.png")
            rgba = Image.new("RGBA", (800, 600), (0, 0, 0, 255))
            rgba.putpixel((0, 0), (0, 0, 0, 0))
            rgba.save(p / "alpha.png")
            with self.assertRaisesRegex(ValueError, "Nonopaque"):
                visual_diff.load_image(p / "alpha.png")


if __name__ == "__main__":
    unittest.main()
