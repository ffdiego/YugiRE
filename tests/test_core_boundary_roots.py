"""Read-only regression for the actual default AI entry, using the pinned PE table.

No native execution, compiler, Ghidra import or generated test binary required.
"""
import hashlib
from pathlib import Path
import re
import struct
import unittest

ROOT = Path(__file__).resolve().parents[1]


class CoreBoundaryRootTests(unittest.TestCase):
    def test_default_ai_root_is_the_native_phase_table_entry(self):
        exe = ROOT.parent / "passion" / "joey_pc.exe"
        if not exe.is_file():
            self.skipTest("Original pinned PE unavailable")
        raw = exe.read_bytes()
        self.assertEqual(hashlib.sha256(raw).hexdigest(),
                         "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de")
        source = (ROOT / "scripts/CoreBoundaryReach.java").read_text()
        match = re.search(r"DEFAULT_ROOTS\s*=\s*\{([^}]+)\}", source)
        self.assertIsNotNone(match)
        roots = [int(value, 16) for value in re.findall(r"0x([0-9A-Fa-f]+)L", match.group(1))]
        # This PE maps these .data/.text VAs to raw RVA offsets (pinned hash).
        ai_entry = struct.unpack_from("<I", raw, 0x005EFB84 - 0x00400000)[0]
        self.assertEqual(roots, [0x005BC700, ai_entry])
        self.assertEqual(raw[ai_entry - 0x400000:ai_entry - 0x400000 + 5], bytes.fromhex("A0FA77A500"))
        self.assertFalse(any(0x005B6FF0 < address <= 0x005B7283 for address in roots),
                         "Default must not silently resolve inside the summon executor")
