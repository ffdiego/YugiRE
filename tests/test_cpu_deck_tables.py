import importlib.util
from pathlib import Path
import struct
import unittest

REPO = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("cpu_tables", REPO / "scripts/extract_cpu_deck_selection.py")
TABLES = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(TABLES)


class CpuDeckTableTests(unittest.TestCase):
    def test_rejects_different_executable(self):
        with self.assertRaisesRegex(ValueError, "fingerprint"):
            TABLES.read_tables(b"MZ unsupported")

    def test_counts_all_three_ydc_sections(self):
        data = bytes(8) + struct.pack("<3H", 2, 1, 2) + struct.pack("<2H", 0, 1) + struct.pack("<H", 3)
        self.assertEqual(TABLES.deck_counts(data), [2, 0, 1])

    def test_rejects_truncated_cards_and_trailing_bytes(self):
        for data in (bytes(8) + struct.pack("<H", 40), bytes(14) + b"\0"):
            with self.assertRaises(ValueError):
                TABLES.deck_counts(data)

    @unittest.skipUnless((REPO.parent / "passion/joey_pc.exe").is_file(), "Original EXE unavailable")
    def test_original_table_boundaries_and_overlap(self):
        rows = TABLES.read_tables((REPO.parent / "passion/joey_pc.exe").read_bytes())
        self.assertEqual(len(rows), 14)
        self.assertEqual([len(row["resources"]) for row in rows[7:]], [3, 7, 6, 7, 6, 6, 6])
        self.assertEqual(len(set(name for row in rows[:7] for name in row["resources"])), 21)
        self.assertEqual(len(set(name for row in rows[7:] for name in row["resources"])), 37)
        self.assertIn("j\\file\\cpu_003.ydc", rows[0]["resources"])
        self.assertIn("j\\file\\ful_3exo.ydc", rows[-1]["resources"])
        self.assertNotIn("j\\file\\ful_3exo.ydc", rows[-2]["resources"])


if __name__ == "__main__":
    unittest.main()
