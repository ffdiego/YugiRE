"""Synthetic regression tests for compare_ydr; no game files required."""
from __future__ import annotations

import importlib.util
from pathlib import Path
import struct
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parents[1] / "experiments" / "hybrid_probe" / "compare_ydr.py"
spec = importlib.util.spec_from_file_location("joey_compare_ydr", SCRIPT)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)


def ydr(commands, *, first_card=590):
    # Exploratory YGR-header and six card groups, per inspect_joey_replay.read_file.
    header = bytearray(0x1C)
    header[:4] = b"YGR\x00"
    decks = [[first_card], [], [], [47], [], []]
    lists = b"".join(struct.pack("<H", len(x)) + b"".join(struct.pack("<H", v) for v in x)
                     for x in decks)
    cmds = b"".join(struct.pack("<4H", *x) for x in commands)
    return bytes(header) + lists + struct.pack("<I", len(commands)) + cmds


class CompareYDRTests(unittest.TestCase):
    def check(self, base, patched):
        with tempfile.TemporaryDirectory() as folder:
            a = Path(folder) / "a.ydr"
            b = Path(folder) / "b.ydr"
            a.write_bytes(base)
            b.write_bytes(patched)
            return mod.compare_ydr(a, b)

    def test_same_commands_ignores_header_identifier(self):
        a = bytearray(ydr([(0x8001, 0, 0, 0), (0x61, 1, 1, 0)]))
        b = bytearray(a)
        b[4:12] = b"DIFFSAVE"
        result = self.check(a, b)
        self.assertTrue(result["same_initial_card_lists"])
        self.assertTrue(result["same_command_stream"])
        self.assertIsNone(result["first_difference"])

    def test_detects_first_difference(self):
        result = self.check(
            ydr([(0x8001, 0, 0, 0), (0x61, 1, 1, 0)]),
            ydr([(0x8001, 0, 0, 0), (0x61, 2, 1, 0)]))
        self.assertEqual(result["matching_prefix_records"], 1)
        self.assertEqual(result["first_difference"]["record_index"], 1)
        self.assertFalse(result["same_command_stream"])

    def test_detects_extra_command(self):
        result = self.check(ydr([(1, 0, 0, 0)]), ydr([(1, 0, 0, 0), (2, 0, 0, 0)]))
        self.assertEqual(result["matching_prefix_records"], 1)
        self.assertFalse(result["same_command_stream"])

    def test_identical_commands_but_different_decks(self):
        result = self.check(ydr([(1, 0, 0, 0)], first_card=590),
                            ydr([(1, 0, 0, 0)], first_card=47))
        self.assertTrue(result["same_command_stream"])
        self.assertFalse(result["same_initial_card_lists"])


if __name__ == "__main__":
    unittest.main()
