"""Check shipped observer hook signatures against the pinned, read-only PE.

Skips when the original game is absent. Never starts Joey or writes its files.
"""
import hashlib
from pathlib import Path
import re
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from pe_preflight import EXPECTED_SHA256, parse_pe


class DeckTracePreflightTests(unittest.TestCase):
    def test_hook_signatures_match_original_pe_text(self):
        root = Path(__file__).resolve().parents[2]
        executable = root.parent / "passion" / "joey_pc.exe"
        if not executable.is_file():
            self.skipTest("Original Joey executable not present")
        raw = executable.read_bytes()
        self.assertEqual(hashlib.sha256(raw).hexdigest(), EXPECTED_SHA256)
        info = parse_pe(raw)
        source = (root / "native_rules" / "deck_trace.js").read_text()
        targets = re.findall(r"\[(0x[0-9a-f]+), '([0-9a-f]+)'\]", source)
        self.assertEqual(len(targets), 5)
        for address, encoded in targets:
            rva = int(address, 16) - info["image_base"]
            signature = bytes.fromhex(encoded)
            text = next(section for section in info["sections"]
                        if section["name"] == ".text"
                        and section["rva"] <= rva
                        and rva + len(signature) <= section["rva"] + section["raw_size"])
            offset = text["file_offset"] + rva - text["rva"]
            with self.subTest(address=address):
                self.assertEqual(raw[offset:offset + len(signature)], signature)
