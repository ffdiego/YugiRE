"""Exercise the matrix CLI with generated opaque 800×600 frames."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from PIL import Image

SOURCE = Path(__file__).resolve().parents[1] / "compare_visual_frame_matrix.py"


class VisualFrameMatrixTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.output = self.root / "out"
        self.manifest = self.root / "pairs.json"
        self.reference = self.root / "reference.png"
        self.candidate = self.root / "candidate.png"
        frame = Image.new("RGB", (800, 600))
        frame.save(self.reference)
        frame.putpixel((2, 1), (0, 7, 0))
        frame.save(self.candidate)
        self.pair = {"id": "impact", "reference": "reference.png", "candidate": "candidate.png",
                     "referenceUpdate": 18, "candidateUpdate": 16}

    def run_matrix(self, pairs):
        self.manifest.write_text(json.dumps({"schemaVersion": 1, "pairs": pairs}), encoding="utf-8")
        return subprocess.run([sys.executable, str(SOURCE), str(self.manifest), "--output", str(self.output)],
                              capture_output=True, text=True, check=False)

    def test_compares_all_pairs_and_applies_each_limit_independently(self):
        first = self.pair | {"maxMismatchedPixels": 0, "maxUpdateDelta": 2}
        second = self.pair | {"id": "recovery", "threshold": 7, "maxMismatchedPixels": 0, "maxUpdateDelta": 1}
        observed = self.pair | {"id": "observation"}
        process = self.run_matrix([first, second, observed])
        self.assertEqual(process.returncode, 1, process.stderr)
        report = json.loads((self.output / "matrix-report.json").read_text(encoding="utf-8"))
        self.assertEqual(report["pairsOverLimit"], 2)
        self.assertEqual([pair["id"] for pair in report["pairs"]], ["impact", "recovery", "observation"])
        impact, recovery, observation = report["pairs"]
        self.assertEqual(impact["updateDelta"], -2)
        self.assertEqual(impact["mismatchedPixels"], 1)
        self.assertEqual(impact["failedLimits"], ["maxMismatchedPixels"])
        self.assertEqual(recovery["mismatchedPixels"], 0)
        self.assertEqual(recovery["failedLimits"], ["maxUpdateDelta"])
        self.assertEqual(observation["failedLimits"], [])
        for pair in report["pairs"]:
            self.assertTrue((self.output / pair["report"]).is_file())
            self.assertTrue((self.output / pair["id"] / "mismatch-mask.png").is_file())
        within_limits = self.run_matrix([first | {"maxMismatchedPixels": 1}])
        self.assertEqual(within_limits.returncode, 0, within_limits.stderr)

    def test_rejects_duplicate_ids_and_invalid_counters_before_creating_outputs(self):
        process = self.run_matrix([self.pair, self.pair | {"id": "IMPACT"}])
        self.assertEqual(process.returncode, 2, process.stderr)
        self.assertIn("Duplicate frame pair ID", process.stderr)
        for invalid_update in (True, 2.5, -1, None):
            with self.subTest(invalid_update=invalid_update):
                process = self.run_matrix([self.pair, self.pair | {"id": "recovery", "candidateUpdate": invalid_update}])
                self.assertEqual(process.returncode, 2, process.stderr)
                self.assertIn("recovery.candidateUpdate", process.stderr)
        self.assertFalse(self.output.exists())

    def test_rejects_output_hard_linked_to_another_pairs_input(self):
        first_report = self.output / "impact" / "difference.png"
        first_report.parent.mkdir(parents=True)
        other_reference = self.root / "other.png"
        other_reference.write_bytes(self.reference.read_bytes())
        original = other_reference.read_bytes()
        os.link(other_reference, first_report)
        process = self.run_matrix([self.pair, self.pair | {"id": "second", "reference": "other.png"}])
        self.assertEqual(process.returncode, 2, process.stderr)
        self.assertIn("Output would overwrite an input", process.stderr)
        self.assertEqual(other_reference.read_bytes(), original)
        self.assertFalse((self.output / "matrix-report.json").exists())


    def test_rejects_outputs_shared_between_pairs(self):
        first = self.output / "impact" / "report.json"
        second = self.output / "second" / "report.json"
        first.parent.mkdir(parents=True)
        second.parent.mkdir(parents=True)
        original = b"previous report"
        first.write_bytes(original)
        for link in (os.link, os.symlink):
            with self.subTest(link=link.__name__):
                link(first, second)
                self.addCleanup(second.unlink, missing_ok=True)
                process = self.run_matrix([self.pair, self.pair | {"id": "second"}])
                self.assertEqual(process.returncode, 2, process.stderr)
                self.assertIn("Output would overwrite another output", process.stderr)
                self.assertEqual(first.read_bytes(), original)
                self.assertEqual(second.read_bytes(), original)
                self.assertFalse((first.parent / "difference.png").exists())
                self.assertFalse((self.output / "matrix-report.json").exists())
                second.unlink()


if __name__ == "__main__":
    unittest.main()
