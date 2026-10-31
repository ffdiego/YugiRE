"""Synthetic fixtures only; original user saves are not part of the repository."""

import importlib.util
from pathlib import Path
import struct
import unittest


SPEC = importlib.util.spec_from_file_location(
    "system_save", Path(__file__).resolve().parents[1] / "scripts/inspect_system_save.py")
SAVE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SAVE)


def encode_fixture(decoded):
    # Independent inverse from native 00439FB0: exponent 5, modulus 299.
    bits = sum(pow(value, 5, 299) << (index * 9)
               for index, value in enumerate(decoded))
    result = bytearray(4) + bits.to_bytes((len(decoded) * 9 + 7) // 8, "little")
    for lane, seed in enumerate(SAVE.LANE_SEEDS):
        result[lane] = (-seed - sum(result[4 + lane:len(decoded):4])) & 255
    return result


def image_fixture():
    image = bytearray(SAVE.IMAGE_SIZE)
    struct.pack_into("<H", image, 10, 3)
    struct.pack_into("<H", image, 12 + 2 * 7, 0x4002)
    struct.pack_into("<H", image, 12 + 2 * 8, 0x8001)
    struct.pack_into("<H", image, 12 + 2 * 9, 0x4000)  # Flags alone are not ownership.
    image[0x117A:0x1182] = b"YUGIOH01"
    struct.pack_into("<H", image, 0x1182, 0xFBA5)
    checksum = -sum(struct.unpack("<2242H", image[:0x1184])) & 65535
    struct.pack_into("<H", image, 0x1184, checksum)
    return image


class SystemSaveTests(unittest.TestCase):
    def test_modular_codec_all_bytes(self):
        decoded = bytes(range(256))
        self.assertEqual(SAVE.decode_block(encode_fixture(decoded), len(decoded)), decoded)

    def test_small_registry_block(self):
        header = bytes.fromhex("010000008e79e56a")
        self.assertEqual(SAVE.decode_block(encode_fixture(header), 8), header)

    def test_counts_copies_and_flags_separately(self):
        report = SAVE.inspect_save(encode_fixture(b"\x01" + bytes(7) + image_fixture()))
        self.assertEqual(report["owned_types_without_pack_filter"], 2)
        self.assertEqual(report["owned_copies"], 3)
        self.assertEqual(report["stored_copy_counter"], 3)
        self.assertEqual(report["owned_cards"][0]["flags_hex"], "4000")

    def test_rejects_truncated_block(self):
        with self.assertRaisesRegex(ValueError, "Expected"):
            SAVE.decode_block(bytes(12), 8)

    def test_rejects_encoded_checksum_corruption(self):
        encoded = encode_fixture(bytes(range(32)))
        encoded[4] ^= 1
        with self.assertRaisesRegex(ValueError, "Encoded block checksum"):
            SAVE.decode_block(encoded, 32)

    def test_rejects_corrupt_image_even_with_valid_outer_checksum(self):
        image = image_fixture()
        image[10] ^= 1
        with self.assertRaisesRegex(ValueError, "image checksum"):
            SAVE.inspect_save(encode_fixture(b"\x01" + bytes(7) + image))


if __name__ == "__main__":
    unittest.main()
