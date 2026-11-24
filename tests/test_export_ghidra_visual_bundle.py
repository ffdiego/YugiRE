"""Focused Ghidra export selection is testable without the game or Ghidra."""
from contextlib import redirect_stdout
from io import StringIO
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import zipfile

from scripts import export_ghidra_visual_bundle as exporter
from scripts.export_ghidra_visual_bundle import (
    ROOT, choose_targets, instruction_windows, validate_instruction_window_dump,
    memory_queries, query_arguments, validate_memory_dump,
)


class FocusedVisualExportTests(unittest.TestCase):
    def test_query_checks_the_entire_fingerprint_line(self):
        with tempfile.TemporaryDirectory() as directory:
            work = Path(directory)
            report = work / "batch.txt"
            for suffix, accepted in (("", True), ("0", False), (" wrong", False)):
                with self.subTest(suffix=suffix):
                    report.write_text(f"SHA256: {exporter.EXPECTED_SHA256}{suffix}\n", encoding="utf-8")
                    completed = subprocess.CompletedProcess([], 0, "")
                    with patch.object(exporter.subprocess, "run", return_value=completed):
                        ok, _ = exporter.run_query(work, work, "batch", [], 30)
                    self.assertEqual(ok, accepted)

    def test_rejected_batch_cannot_satisfy_targets_or_enter_the_bundle(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            support = root / "support"
            support.mkdir()
            (support / "analyzeHeadless").touch()
            output = root / "bundle.zip"
            recovered = False

            def query(_ghidra, work, name, _arguments, _timeout):
                report = work / (name + ".txt")
                if name == "batch-000":
                    report.write_text("SHA256: wrong\nFUNCTION Sample @00100000\n", encoding="utf-8")
                    return False, "Unexpected executable fingerprint"
                self.assertEqual(name, "retry-000-00100000")
                result = "FUNCTION Sample @00100000\nvoid Sample() {}\n" if recovered else (
                    "DECOMPILE_FAILED 00100000 Timeout\n")
                report.write_text(f"SHA256: {exporter.EXPECTED_SHA256}\n"
                                  f"{result}FUNCTION Unrelated @00100010\n", encoding="utf-8")
                return True, "ok"

            with (patch.object(exporter, "ROOT", root), patch.object(exporter, "run_query", side_effect=query),
                  patch.dict(os.environ, {"GHIDRA_INSTALL_DIR": str(root)}), redirect_stdout(StringIO())):
                status = exporter.main(["--output", str(output), "--targets", "00100000", "--retry-missing"])
                recovered = True
                second = root / "recovered.zip"
                recovered_status = exporter.main([
                    "--output", str(second), "--targets", "00100000", "--retry-missing"])

            self.assertEqual(status, 1)
            with zipfile.ZipFile(output) as bundle:
                manifest = json.loads(bundle.read("manifest.json"))
                self.assertEqual(manifest["decompiled"], [])
                self.assertEqual(manifest["missing"], ["00100000"])
                self.assertEqual(manifest["attempts"][0]["resolved"], [])
                self.assertEqual(manifest["attempts"][0]["message"], "Unexpected executable fingerprint")
                self.assertNotIn("batch-000.txt", bundle.namelist())
                self.assertIn("retry-000-00100000.txt", bundle.namelist())
            self.assertEqual(recovered_status, 0)
            with zipfile.ZipFile(second) as bundle:
                manifest = json.loads(bundle.read("manifest.json"))
                self.assertEqual(manifest["decompiled"], ["00100000"])
                self.assertEqual(manifest["missing"], [])
                self.assertEqual(manifest["attempts"][1]["resolved"], ["00100000"])
                self.assertNotIn("batch-000.txt", bundle.namelist())
            self.assertEqual(len(list((root / "scratch").glob("*/batch-000.txt"))), 2)

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

    def test_bounded_instruction_windows_select_exact_return_addresses(self):
        self.assertEqual(
            instruction_windows(["0041a1e6:48", "0041A520:64"]),
            ["ins:0041A1E6:48", "ins:0041A520:64"],
        )
        self.assertEqual(instruction_windows(None), [])
        for invalid in ("0x0041A1E6:48", "0041A1:48", "0041A1EZ:48",
                        "0041A1E6:0", "0041A1E6:-1", "0041A1E6:1025"):
            with self.subTest(spec=invalid), self.assertRaisesRegex(
                    ValueError, "Invalid instruction window"):
                instruction_windows([invalid])
        with self.assertRaisesRegex(ValueError, "Duplicate instruction window"):
            instruction_windows(["0041A1E6:48", "0041a1e6:64"])
        with self.assertRaisesRegex(ValueError, "Too many instruction windows"):
            instruction_windows([f"{0x41a100 + i:08X}:4" for i in range(33)])

    def test_exact_instruction_window_requires_markers_and_correct_first_va(self):
        commands = instruction_windows(["0041A1E6:3", "0041A520:2"])
        with tempfile.TemporaryDirectory() as directory:
            file = Path(directory) / "windows.txt"
            valid = (
                "SHA256: expected\n"
                "INS_WINDOW_BEGIN 0041A1E6 count=3\n"
                "INS 0041a1e6 MOV EAX,EBX\n"
                "INS 0041a1e8 CALL EDX\n"
                "INS 0041a1ea MOV ECX,EAX\n"
                "INS_WINDOW_END 0041A1E6 actual=3\n"
                "INS_WINDOW_BEGIN 0041A520 count=2\n"
                "INS 0041a520 MOV EDI,EBX\n"
                "INS 0041a522 CALL EAX\n"
                "INS_WINDOW_END 0041A520 actual=2\n"
            )
            file.write_text(valid, encoding="utf-8")
            self.assertTrue(validate_instruction_window_dump(file, commands))
            # A shifted Ghidra fallback must never count as target evidence.
            for corrupt in (
                valid.replace("INS 0041a1e6 ", "INS 0041a1e7 "),
                valid.replace("actual=3", "actual=4"),
                valid.replace("count=3", "count=2"),
                valid.replace("INS_WINDOW_END 0041A520 actual=2\n", ""),
                valid.replace("INS_WINDOW_BEGIN 0041A520 count=2\n", ""),
                valid.replace("INS 0041a520 MOV EDI,EBX\n", ""),
                valid.replace("INS_WINDOW_BEGIN 0041A520 count=2\n",
                              "INS_WINDOW_BEGIN 0041A1E6 count=2\n"),
            ):
                with self.subTest(corrupt=corrupt[:50]):
                    file.write_text(corrupt, encoding="utf-8")
                    self.assertFalse(validate_instruction_window_dump(file, commands))
            file.write_text(valid + valid, encoding="utf-8")
            self.assertFalse(validate_instruction_window_dump(file, commands))

    def test_memory_queries_split_read_only_ranges_at_512_byte_boundaries(self):
        self.assertEqual(
            memory_queries(["005DB570:4", "00a52ee8:1025"]),
            ["bytes:005DB570:4", "bytes:00A52EE8:512",
             "bytes:00A530E8:512", "bytes:00A532E8:1"],
        )
        self.assertEqual(memory_queries(None), [])
        with self.assertRaisesRegex(ValueError, "Overlapping memory"):
            memory_queries(["00A52EE8:512", "00A52F00:2"])
        for bad in ["0x00A52EE8:4", "00A52EE8:0", "00A52EE8:-1",
                    "00A52EE8:4097", "FFFFFFFE:4", "wrong:4"]:
            with self.subTest(bad=bad):
                with self.assertRaises(ValueError):
                    memory_queries([bad])

    def test_memory_read_has_strict_address_length_and_hex_validation(self):
        commands = memory_queries(["005DB570:4", "00A52EE8:3"])
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "memory.txt"
            path.write_text("SHA256: expected\nBYTES 005db570 count=4 "
                            "01 02 03 04\nBYTES 00a52ee8 count=3 10 20 30\n")
            self.assertTrue(validate_memory_dump(path, commands))
            path.write_text("BYTES 005db570 count=3 01 02 03\n"
                            "BYTES 00a52ee8 count=3 10 20 30\n")
            self.assertFalse(validate_memory_dump(path, commands))
            path.write_text("BYTES 005db570 count=4 01 02 03 FF\n")
            self.assertFalse(validate_memory_dump(path, commands))

    def test_longer_assembly_request_is_explicit_and_bounded(self):
        self.assertEqual(
            query_arguments(["0043B280"], disassemble=True,
                            instruction_count=512),
            ["0043B280", "ins:0043B280:512"],
        )
        with self.assertRaisesRegex(ValueError, "Instruction count"):
            query_arguments(["0043B280"], disassemble=True,
                            instruction_count=1025)

    def test_list_only_never_needs_ghidra_or_dat(self):
        process = subprocess.run(
            [sys.executable, str(ROOT / "scripts/export_ghidra_visual_bundle.py"),
             "--targets", "0044EFD0", "0044F390", "--disassemble",
             "--list-only"],
            cwd=ROOT, capture_output=True, text=True, check=True,
        )
        self.assertIn("2 target functions", process.stdout)
        self.assertIn("0044EFD0\n0044F390", process.stdout)
        memory = subprocess.run(
            [sys.executable, str(ROOT / "scripts/export_ghidra_visual_bundle.py"),
             "--targets", "004524F0", "--memory", "00A52EE8:2048",
             "--memory", "005DB570:4", "--list-only"],
            cwd=ROOT, capture_output=True, text=True, check=True,
        )
        self.assertIn("5 memory chunks", memory.stdout)
        self.assertIn("bytes:00A52EE8:512", memory.stdout)
        self.assertIn("bytes:005DB570:4", memory.stdout)
        focused = subprocess.run(
            [sys.executable, str(ROOT / "scripts/export_ghidra_visual_bundle.py"),
             "--targets", "0041A070", "--instruction-window", "0041A1E6:48",
             "--instruction-window", "0041A520:64", "--list-only"],
            cwd=ROOT, capture_output=True, text=True, check=True,
        )
        self.assertIn("2 focused instruction windows", focused.stdout)
        self.assertIn("ins:0041A1E6:48", focused.stdout)
        self.assertIn("ins:0041A520:64", focused.stdout)


if __name__ == "__main__":
    unittest.main()
