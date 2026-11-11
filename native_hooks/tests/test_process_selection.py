"""Regression tests for no-argument Joey startup and PID selection."""
from __future__ import annotations

import sys
import unittest
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from patch_dual_rules import build_parser, resolve_joey_pid  # noqa: E402


class FakeDevice:
    def __init__(self, *processes):
        self.processes = [SimpleNamespace(pid=pid, name=name) for pid, name in processes]
        self.scans = 0

    def enumerate_processes(self):
        self.scans += 1
        return self.processes


class JoeyStartupTests(unittest.TestCase):
    def test_no_argument_defaults(self):
        args = build_parser().parse_args([])
        self.assertIsNone(args.pid)
        self.assertEqual(args.sparks, 200)
        self.assertEqual(args.raimei, 400)
        self.assertEqual(args.sparks_mode, "draw")
        self.assertEqual(args.raimei_mode, "damage-draw")
        self.assertEqual(args.console_side, 1)

    def test_unique_process_case_insensitive(self):
        device = FakeDevice((103, "explorer.exe"), (2401, "JOEY_PC.EXE"))
        self.assertEqual(resolve_joey_pid(device), 2401)
        self.assertEqual(device.scans, 1)

    def test_missing_process_has_clear_error(self):
        with self.assertRaisesRegex(ValueError, "joey_pc.exe is not running"):
            resolve_joey_pid(FakeDevice((103, "explorer.exe")))

    def test_multiple_processes_require_explicit_pid(self):
        with self.assertRaisesRegex(ValueError, r"2401, 2940.*--pid"):
            resolve_joey_pid(FakeDevice((2940, "joey_pc.exe"),
                                        (2401, "JOEY_PC.EXE")))

    def test_override_bypasses_discovery(self):
        device = FakeDevice((2401, "joey_pc.exe"), (2940, "joey_pc.exe"))
        self.assertEqual(resolve_joey_pid(device, 2940), 2940)
        self.assertEqual(device.scans, 0)

    def test_invalid_manual_pid_rejected(self):
        with self.assertRaisesRegex(ValueError, "positive"):
            resolve_joey_pid(FakeDevice(), 0)


if __name__ == "__main__":
    unittest.main()
