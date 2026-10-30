from pathlib import Path
import struct
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "scripts"))
from catalog_ritual_rules import parse_rules


class RitualRulesTests(unittest.TestCase):
    def pack(self, monster, spell, level):
        return struct.pack("<I", monster | (spell << 13) | (level << 26))

    def test_fields_and_padding(self):
        result = parse_rules(self.pack(363, 669, 8) + bytes(8))
        self.assertEqual((result[0]["monster_id"], result[0]["spell_id"], result[0]["required_level"]), (363, 669, 8))

    def test_field_boundaries(self):
        result = parse_rules(self.pack(8191, 8191, 63) + bytes(4))
        self.assertEqual((result[0]["monster_id"], result[0]["spell_id"], result[0]["required_level"]), (8191, 8191, 63))

    def test_rejects_truncation_and_missing_sentinel(self):
        for payload in [b"", bytes(3), self.pack(1, 2, 3)]:
            with self.subTest(payload=payload), self.assertRaises(ValueError):
                parse_rules(payload)

    def test_rejects_partial_sentinel_and_zero_requirement(self):
        for fields in [(0, 2, 3), (1, 0, 3), (1, 2, 0)]:
            with self.subTest(fields=fields), self.assertRaises(ValueError):
                parse_rules(self.pack(*fields) + bytes(4))

    def test_rejects_duplicate_spell(self):
        with self.assertRaises(ValueError):
            parse_rules(self.pack(1, 2, 3) + self.pack(4, 2, 5) + bytes(4))

    def test_rejects_nonzero_tail(self):
        with self.assertRaises(ValueError):
            parse_rules(bytes(4) + self.pack(1, 2, 3))


if __name__ == "__main__":
    unittest.main()
