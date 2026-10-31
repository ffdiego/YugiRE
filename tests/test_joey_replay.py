"""Synthetic replay fixtures plus an optional original local sample.

No original game replay data is checked into the repository.
"""
import hashlib
import importlib.util
from pathlib import Path
import struct
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("inspect_joey_replay", ROOT / "scripts/inspect_joey_replay.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


def fixture(records=((0x8001, 0, 0, 0), (0x0061, 1, 1, 0), (0x0004, 0, 0, 0))):
    buf = bytearray(struct.pack("<4s8s4I", b"YGR\0", bytes(8), 0x514, 1, 1, 123456))
    for group in ((730, 1123), (), (), (1123,), (), ()):
        buf += struct.pack("<H", len(group))
        if group:
            buf += struct.pack("<" + "H" * len(group), *group)
    buf += struct.pack("<I", len(records))
    for record in records:
        buf += struct.pack("<4H", *record)
    return bytes(buf)


class ReplayReaderTests(unittest.TestCase):
    def parse(self, data):
        with tempfile.TemporaryDirectory() as d:
            path = Path(d) / "fixture.ydr"
            path.write_bytes(data)
            return module.read_file(path)

    def test_8_byte_command_records_and_side_bit(self):
        records, summary = self.parse(fixture())
        self.assertEqual(len(records), 3)
        self.assertEqual(summary["deck_groups"][0]["groups"][0]["resource_ids"], [730, 1123])
        self.assertEqual(records[0]["opcode_hex"], "0x0001")
        self.assertEqual(records[0]["high_bit_side_candidate"], 1)
        self.assertEqual(records[1]["opcode_hex"], "0x0061")
        self.assertEqual(records[2]["opcode_hex"], "0x0004")
        self.assertEqual(summary["commands_start_offset_hex"], "0x32")

    def test_invalid_signature(self):
        data = bytearray(fixture())
        data[0] = 0x01
        with self.assertRaisesRegex(ValueError, "signature"):
            self.parse(data)

    def test_truncated_records(self):
        with self.assertRaisesRegex(ValueError, "Command count"):
            self.parse(fixture()[:-1])

    def test_optional_real_replay(self):
        sample = ROOT / "scratch/replays/replaycomrelinquishe.ydr"
        if not sample.is_file():
            self.skipTest("operator replay is optional and ignored by Git")
        data = sample.read_bytes()
        self.assertEqual(hashlib.sha256(data).hexdigest(),
                         "83ccbecef104789333c830fe6bc98553ebcb24ff889a2bc85acacd8a93c53f46")
        records, info = module.read_file(sample)
        self.assertEqual(info["record_count"], 1062)
        self.assertEqual(info["commands_start_offset_hex"], "0xDC")
        self.assertEqual([records[1051]["argument_2"], records[1052]["argument_1"],
                          records[1059]["argument_1"], records[1060]["argument_1"]],
                         [331, 0, 1400, 1400])
        self.assertEqual(records[1052]["opcode_hex"], "0x0031")
        self.assertEqual(records[1060]["opcode_hex"], "0x0043")


if __name__ == "__main__":
    unittest.main()
