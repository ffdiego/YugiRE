"""Focused Ghidra export selection is testable without the game or Ghidra."""
import subprocess
import sys
import unittest

from scripts.export_ghidra_visual_bundle import (
    ROOT, choose_targets, query_arguments,
)


class FocusedVisualExportTests(unittest.TestCase):
    def test_explicit_targets_are_normalized_sorted_and_deduplicated(self):
        self.assertEqual(
            choose_targets(["0044f390", "0044EFD0", "0044efd0"], 3),
            ["0044EFD0", "0044F390"],
        )

    def test_invalid_and_excessive_targets_fail_closed(self):
        for value in ("0x0044EFD0", "0044EF", "0044EFG0", "../evil"):
            with self.subTest(value=value):
                with self.assertRaisesRegex(ValueError, "Invalid target VA"):
                    choose_targets([value], 3)
        with self.assertRaisesRegex(ValueError, "Too many explicit VAs"):
            choose_targets(["0044EFD0", "0044F370"], 1)

    def test_assembly_is_observational_and_transient_fallback_is_explicit(self):
        addresses = ["0044EFD0", "0044F390"]
        self.assertEqual(query_arguments(addresses), addresses)
        self.assertEqual(
            query_arguments(addresses, disassemble=True),
            addresses + ["ins:0044EFD0:100", "ins:0044F390:100"],
        )
        self.assertEqual(
            query_arguments(["0044EFD0"], disassemble=True, transient=True),
            ["tempfunc:0044EFD0", "ins:0044EFD0:100"],
        )

    def test_list_only_never_needs_ghidra_or_dat(self):
        process = subprocess.run(
            [sys.executable, str(ROOT / "scripts/export_ghidra_visual_bundle.py"),
             "--targets", "0044EFD0", "0044F390", "--disassemble",
             "--list-only"],
            cwd=ROOT, capture_output=True, text=True, check=True,
        )
        self.assertIn("2 target functions", process.stdout)
        self.assertIn("0044EFD0\n0044F390", process.stdout)


if __name__ == "__main__":
    unittest.main()
