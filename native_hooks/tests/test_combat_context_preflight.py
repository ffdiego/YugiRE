"""Validate passive F40 callsite ABI against the pinned local PE, without execution."""
import hashlib
import re
from pathlib import Path
import struct
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from pe_preflight import EXPECTED_SHA256, parse_pe


class CombatContextPreflightTests(unittest.TestCase):
    def test_direct_calls_and_cdecl_return_match_original(self):
        executable = Path(__file__).resolve().parents[3] / "passion" / "joey_pc.exe"
        if not executable.is_file():
            self.skipTest("Original Joey executable not present")
        raw = executable.read_bytes()
        self.assertEqual(hashlib.sha256(raw).hexdigest(), EXPECTED_SHA256)
        pe = parse_pe(raw)

        def read(va, size):
            rva = va - pe["image_base"]
            section = next(s for s in pe["sections"]
                           if s["rva"] <= rva and rva + size <= s["rva"] + s["raw_size"])
            offset = section["file_offset"] + rva - section["rva"]
            return raw[offset:offset + size]

        loader = (Path(__file__).resolve().parents[1] / "yugire_dual_loader.js").read_text()
        points = re.findall(r"\['[a-z_]+', '(0x[0-9A-F]+)', '([0-9A-F]+)'\]", loader)
        self.assertEqual(len(points), 9)
        for address, signature in points:
            expected = bytes.fromhex(signature)
            self.assertGreaterEqual(len(expected), 5)
            self.assertEqual(read(int(address, 16), len(expected)), expected, address)

        # Return VAs from F40's assembly, not inferred proximity in a trace.
        services = {
            0x005ADD60: [0x005B0F97],
            0x005ADDC0: [0x005B0FAE],
            0x0056D790: [0x005B1060],
            0x0056D8D0: [0x005B1117, 0x005B112F, 0x005B1153, 0x005B116F],
            0x005699A0: [0x005B11F4, 0x005B1227],
            0x0056B220: [0x005B1205, 0x005B1238],
            0x004022E0: [0x005B0FB8, 0x005B1009, 0x005B10FD, 0x005B1251],
        }
        for target, returns in services.items():
            for return_va in returns:
                with self.subTest(target=hex(target), return_va=hex(return_va)):
                    call = read(return_va - 5, 5)
                    self.assertEqual(call[0], 0xE8)
                    self.assertEqual(return_va + struct.unpack("<i", call[1:])[0], target)
        self.assertEqual(read(0x005B0F40, 7), bytes.fromhex("83ec2053555657"))
        self.assertEqual(read(0x005B1305, 4), bytes.fromhex("83c420c3"))
        self.assertEqual(read(0x005B0E20, 3), bytes.fromhex("83c410"))
