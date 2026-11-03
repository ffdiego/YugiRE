"""Synthetic fixtures for the PE preflight; never require original game files."""
import importlib.util
from pathlib import Path
import struct
import unittest

SCRIPT = Path(__file__).resolve().parents[1] / "experiments" / "hybrid_probe" / "pe_preflight.py"
spec = importlib.util.spec_from_file_location("joey_hybrid_pe_preflight", SCRIPT)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


def fake_pe(machine=0x14C, image_base=0x400000):
    data = bytearray(0x2000)
    data[:2] = b"MZ"
    struct.pack_into("<I", data, 0x3C, 0x80)
    data[0x80:0x84] = b"PE\x00\x00"
    struct.pack_into("<HHIIIHH", data, 0x84, machine, 1, 0, 0, 0, 0xE0, 0)
    struct.pack_into("<H", data, 0x98, 0x10B)
    struct.pack_into("<I", data, 0x98 + 28, image_base)
    section = 0x98 + 0xE0
    data[section:section+8] = b".text\x00\x00\x00"
    struct.pack_into("<IIII", data, section + 8, 0x1000, 0x1000, 0x1000, 0x1000)
    return bytes(data)


class PEPreflightTests(unittest.TestCase):
    def test_pe32_x86(self):
        report = module.parse_pe(fake_pe())
        self.assertEqual(report["machine"], 0x14C)
        self.assertEqual(report["image_base"], 0x400000)
        self.assertEqual(report["sections"][0]["name"], ".text")

    def test_rejects_invalid_headers(self):
        with self.assertRaises(ValueError):
            module.parse_pe(b"MZ" + bytes(4))

    def test_rejects_64bit_optional_header(self):
        data = bytearray(fake_pe())
        struct.pack_into("<H", data, 0x98, 0x20B)
        with self.assertRaisesRegex(ValueError, "PE32"):
            module.parse_pe(bytes(data))

    def test_fails_closed_on_other_exe_hash(self):
        import tempfile
        with tempfile.TemporaryDirectory() as d:
            path = Path(d) / "joey_pc.exe"
            path.write_bytes(fake_pe())
            report = module.probe(path)
            self.assertFalse(report["checks"]["expected_sha256"])
            self.assertFalse(report["passed"])


if __name__ == "__main__":
    unittest.main()
