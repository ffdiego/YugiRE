"""Synthetic malformed-input and grouping tests; never require game files."""

from pathlib import Path
import struct
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "scripts"))
from catalog_card_callbacks import group_callbacks, native_search_model, parse_callbacks, parse_identities


class CallbackCatalogTests(unittest.TestCase):
    def parse(self, rows):
        payload = b"".join(struct.pack("<6I", *row) for row in rows)
        return parse_callbacks(payload, [(0x1000, 0x2000)])

    def test_distinguishes_exact_profile_from_shared_primary(self):
        rows = self.parse([(10, 0x1000, 0, 0, 0, 0),
                           (20, 0x1000, 0x1100, 0, 0, 0),
                           (30, 0x1000, 0, 0, 0, 0)])
        profiles, primary = group_callbacks(rows)
        self.assertEqual(len(profiles), 2)
        self.assertEqual(profiles[(0x1000, 0, 0, 0, 0)], [0, 2])
        self.assertEqual(primary[0x1000], [0, 1, 2])

    def test_null_primary_does_not_mean_all_null(self):
        rows = self.parse([(10, 0, 0, 0, 0, 0), (20, 0, 0, 0x1000, 0, 0)])
        profiles, primary = group_callbacks(rows)
        self.assertEqual(primary[0], [0, 1])
        self.assertEqual(profiles[(0, 0, 0, 0, 0)], [0])

    def test_rejects_empty_and_truncated_table(self):
        for payload in [b"", bytes(23), bytes(25)]:
            with self.subTest(length=len(payload)), self.assertRaises(ValueError):
                parse_callbacks(payload, [(0x1000, 0x2000)])

    def test_rejects_duplicate_and_unsorted_ids(self):
        for second in [9, 10]:
            with self.subTest(second=second), self.assertRaises(ValueError):
                self.parse([(10, 0, 0, 0, 0, 0), (second, 0, 0, 0, 0, 0)])

    def test_rejects_upper_id_bits_and_nonexecutable_pointers(self):
        for row in [(0x10001, 0, 0, 0, 0, 0), (10, 0x2000, 0, 0, 0, 0),
                    (10, 0, 0, 0, 0, 0xFFF)]:
            with self.subTest(row=row), self.assertRaises(ValueError):
                self.parse([row])

    def test_search_first_last_missing_and_singleton(self):
        rows = self.parse([(1, 0, 0, 0, 0, 0), (100, 0, 0, 0, 0, 0), (65535, 0, 0, 0, 0, 0)])
        for target, expected in [(0, -1), (1, 0), (2, -1), (100, 1), (65534, -1), (65535, 2)]:
            with self.subTest(target=target):
                self.assertEqual(native_search_model(rows, target), expected)
        self.assertEqual(native_search_model(rows[:1], 1), 0)
        self.assertEqual(native_search_model(rows[:1], 2), -1)

    def test_identity_aliases_empty_names_and_sentinel(self):
        ids = struct.pack("<5H", 65535, 342, 342, 2342, 343)
        names = b"".join(name.ljust(64, b"\0") for name in [b"", b"Sparks", b"Alias", b"Sparks", b""])
        result = parse_identities(ids, names, bytes(20))
        self.assertNotIn(65535, result)
        self.assertEqual([x["index"] for x in result[342]], [1, 2])
        self.assertEqual(result[2342][0]["index"], 3)
        self.assertEqual(result[343][0]["name"], "")

    def test_identity_sizes_and_unterminated_names(self):
        for ids, names, props in [(b"x", b"", b""), (bytes(2), bytes(63), bytes(4)),
                                  (bytes(2), bytes(64), bytes(3)), (bytes(2), b"A" * 64, bytes(4))]:
            with self.subTest(sizes=(len(ids), len(names), len(props))), self.assertRaises(ValueError):
                parse_identities(ids, names, props)


if __name__ == "__main__":
    unittest.main()
