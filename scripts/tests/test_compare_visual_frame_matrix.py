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

    def test_reports_element_bounds_and_enforces_its_own_pixel_budget(self):
        pair = self.pair | {"maxMismatchedPixels": 1, "regions": {
            "impact": {"boundsExclusive": [2, 0, 4, 4], "maxMismatchedPixels": 0},
            "other": {"boundsExclusive": [0, 0, 2, 4], "maxMismatchedPixels": 0},
        }}
        process = self.run_matrix([pair])
        self.assertEqual(process.returncode, 1, process.stderr)
        report = json.loads((self.output / "matrix-report.json").read_text(encoding="utf-8"))
        self.assertEqual(report["pairsOverLimit"], 1)
        result, = report["pairs"]
        self.assertEqual(result["failedLimits"], ["regions.impact.maxMismatchedPixels"])
        self.assertEqual(result["regions"]["impact"]["mismatchBoundsExclusive"], [2, 1, 3, 2])
        self.assertEqual(result["regions"]["other"]["mismatchedPixels"], 0)
        matching = self.run_matrix([pair | {"threshold": 7}])
        self.assertEqual(matching.returncode, 0, matching.stderr)

    def test_rejects_invalid_region_before_any_pair_is_written(self):
        for bounds in ([0, 0, 800, 601], [0, False, 4, 4], [2, 0, 2, 4]):
            with self.subTest(bounds=bounds):
                invalid = self.pair | {"id": "second", "regions": {
                    "element": {"boundsExclusive": bounds},
                }}
                process = self.run_matrix([self.pair, invalid])
                self.assertEqual(process.returncode, 2, process.stderr)
                self.assertIn("second.regions.element.boundsExclusive", process.stderr)
        self.assertFalse(self.output.exists())

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

    def test_compares_capture_fields_before_scoring_pixels(self):
        (self.root / "reference.json").write_text(json.dumps({
            "drawCounter": 18, "duelPhase": "Battle", "overlay": False,
        }), encoding="utf-8")
        for name, debug in (("candidate", False), ("incompatible", 0)):
            (self.root / f"{name}.json").write_text(json.dumps({
                "cardEffectUpdate": 16, "phase": "Battle", "debugVisible": debug,
            }), encoding="utf-8")
        pair = self.pair | {
            "referenceMetadata": "reference.json",
            "candidateMetadata": "candidate.json",
            "referenceUpdate": "drawCounter",
            "candidateUpdate": "cardEffectUpdate",
            "stateFields": {"phase": ["duelPhase", "phase"], "debug": ["overlay", "debugVisible"]},
        }
        incompatible = pair | {"id": "mismatch", "candidateMetadata": "incompatible.json"}
        process = self.run_matrix([incompatible, pair])
        self.assertEqual(process.returncode, 1, process.stderr)
        report = json.loads((self.output / "matrix-report.json").read_text(encoding="utf-8"))
        self.assertEqual(report["pairsIncomparable"], 1)
        self.assertEqual(report["pairsOverLimit"], 0)
        mismatch, match = report["pairs"]
        self.assertEqual(mismatch["stateMismatches"], ["debug"])
        self.assertIsNone(mismatch["report"])
        self.assertIsNone(mismatch["mismatchedPixels"])
        self.assertFalse((self.output / "mismatch").exists())
        self.assertEqual(match["updateDelta"], -2)
        self.assertEqual(match["mismatchedPixels"], 1)
        self.assertEqual(match["captureMetadata"]["candidate"]["updateField"], "cardEffectUpdate")
        self.assertTrue(match["stateChecks"]["phase"]["matches"])

        invalid = pair | {"id": "missing", "candidateUpdate": "missingCounter"}
        process = self.run_matrix([pair, invalid])
        self.assertEqual(process.returncode, 2, process.stderr)
        self.assertIn("missing.candidateUpdate", process.stderr)
        process = self.run_matrix([incompatible | {"id": "only-mismatch"}])
        self.assertEqual(process.returncode, 1, process.stderr)

    def test_protects_metadata_from_output_overwrite(self):
        capture = self.root / "capture.json"
        original = json.dumps({"step": 16})
        capture.write_text(original, encoding="utf-8")
        self.output.mkdir()
        os.link(capture, self.output / "matrix-report.json")
        process = self.run_matrix([self.pair | {
            "candidateMetadata": "capture.json", "candidateUpdate": "step",
        }])
        self.assertEqual(process.returncode, 2, process.stderr)
        self.assertIn("Output would overwrite an input", process.stderr)
        self.assertEqual(capture.read_text(encoding="utf-8"), original)

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
