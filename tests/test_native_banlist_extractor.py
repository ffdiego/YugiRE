"""Offline PE32 and banlist mapping tests; no original executable needed."""
import hashlib
import struct
import unittest

from scripts.extract_joey_banlist import (
    build_report, extract_native_limits, va_to_file_offset,
)


def pe_fixture(entries=((47, 1), (573, 2), (500, 0))):
    data = bytearray(0x600)
    data[:2] = b"MZ"
    struct.pack_into("<I", data, 0x3C, 0x80)
    data[0x80:0x84] = (b"PE" + bytes(2))
    struct.pack_into("<H", data, 0x86, 1)
    struct.pack_into("<H", data, 0x94, 0xE0)
    struct.pack_into("<H", data, 0x98, 0x10B)
    struct.pack_into("<I", data, 0xB4, 0x400000)
    header = 0x80 + 24 + 0xE0
    struct.pack_into("<IIII", data, header + 8,
                     0x200, 0x1000, 0x200, 0x200)
    for i, (card, limit) in enumerate(entries):
        struct.pack_into("<I", data, 0x280 + i * 4,
                         ((limit & 0xFFFF) << 16) | card)
    return bytes(data)


class NativeBanlistExtractorTests(unittest.TestCase):
    VA = 0x401080

    def test_pe32_virtual_address_to_file_offset(self):
        self.assertEqual(va_to_file_offset(pe_fixture(), self.VA, 12), 0x280)

    def test_extract_low12_resource_id_upper16_copy_limit(self):
        result = extract_native_limits(pe_fixture(), self.VA, 3)
        self.assertEqual([(x["resourceId"], x["limit"]) for x in result],
                         [(47, 1), (573, 2), (500, 0)])
        self.assertEqual(result[0]["raw"], 0x0001002F)
        self.assertTrue(all(x["reservedBits12to15"] == 0 for x in result))

    def test_hash_gate_for_different_executable(self):
        with self.assertRaisesRegex(ValueError, "SHA256"):
            build_report(pe_fixture(), table_va=self.VA, count=3)
        report = build_report(pe_fixture(), strict_hash=False, table_va=self.VA, count=3)
        self.assertFalse(report["fingerprintMatched"])
        self.assertEqual(report["exeSha256"], hashlib.sha256(pe_fixture()).hexdigest())
        self.assertEqual(report["defaultLimitForOtherCards"], 3)

    def test_reject_repeated_ids_and_unexpected_limits(self):
        with self.assertRaisesRegex(ValueError, "Repeated resource ID"):
            extract_native_limits(pe_fixture(((47, 1), (47, 2))), self.VA, 2)
        with self.assertRaisesRegex(ValueError, "Unexpected copy limit"):
            extract_native_limits(pe_fixture(((47, 4),)), self.VA, 1)

    def test_reject_bad_pe_and_unmapped_section(self):
        with self.assertRaisesRegex(ValueError, "Not a PE"):
            va_to_file_offset(b"bad", self.VA, 12)
        with self.assertRaisesRegex(ValueError, "not backed"):
            va_to_file_offset(pe_fixture(), self.VA + 0x1000, 12)
        with self.assertRaisesRegex(ValueError, "section"):
            va_to_file_offset(pe_fixture(), self.VA, 0x300)
        with self.assertRaisesRegex(ValueError, "Invalid candidate"):
            extract_native_limits(pe_fixture(), self.VA, 0)


if __name__ == "__main__":
    unittest.main()
