import sys
from pathlib import Path
import unittest
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'scripts'))
from temporary_state_model import cleanup_associations, cleanup_flags


class TemporaryStateTests(unittest.TestCase):
    def test_soul_exchange_marker_expires_on_next_cleanup(self):
        self.assertEqual(cleanup_associations([(264, 3), (264, 0x103)]), [])

    def test_timers_preserve_kind_owner_and_require_following_cleanup(self):
        once = cleanup_associations([(100, 0xab13), (101, 0xcd1c)])
        self.assertEqual(once, [(100, 0xab03), (101, 0xcd0c)])
        self.assertEqual(cleanup_associations(once), [])
        self.assertEqual(cleanup_associations([(100, 0x12f3)]), [(100, 0x12e3)])

    def test_compaction_preserves_other_entries_and_order(self):
        entries = [(1, 1), (2, 0), (3, 3), (4, 12), (5, 13), (6, 11), (7, 4)]
        self.assertEqual(cleanup_associations(entries), [(1, 1), (5, 13), (6, 11), (7, 4)])

    def test_global_category_processed_without_occupant(self):
        rows = [(264, 0x103)]
        self.assertEqual(cleanup_associations(rows, occupied=False, category=0), rows)
        self.assertEqual(cleanup_associations(rows, occupied=False, category=11), [])

    def test_upper_flag_bits_clear_but_lower_bits_survive(self):
        for bit in range(32):
            self.assertEqual(cleanup_flags(1 << bit), (1 << bit) if bit < 16 else 0)
        self.assertEqual(cleanup_flags(0x1234ffff), 0xffff)
        self.assertEqual(0xa55d74 + 11 * 0x90 + 0x8c, 0xa56430)


if __name__ == '__main__':
    unittest.main()
