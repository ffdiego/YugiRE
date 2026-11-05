"""Offline tests for the 12-byte output of Joey's FUN_0056DD30."""
from pathlib import Path
import json
import struct
import subprocess
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from decode_ai_combat_stats import decode_hex, decode_stats


class CombatStatsTests(unittest.TestCase):
    def test_layout_and_signed_values(self):
        payload = struct.pack("<HBBii", 273, 6, 15, 1600, 1200)
        record = decode_stats(payload)
        self.assertEqual(record.raw_hex, "1101060F40060000B0040000")
        self.assertEqual(record.internal_card_index, 273)
        self.assertEqual((record.type_code, record.attribute_code), (6, 15))
        self.assertEqual((record.attack, record.defense), (1600, 1200))

    def test_negative_values_can_be_read_without_recalculating(self):
        record = decode_stats(struct.pack("<HBBii", 100, 1, 2, -100, -500))
        self.assertEqual((record.attack, record.defense), (-100, -500))

    def test_hidden_monster_estimates_are_opaque(self):
        for assumed in (1000, 1900):
            with self.subTest(assumed=assumed):
                record = decode_stats(struct.pack("<HBBii", 0, 0, 0, assumed, assumed))
                self.assertEqual(record.internal_card_index, 0)
                self.assertEqual(record.attack, assumed)
                self.assertEqual(record.defense, assumed)

    def test_accepted_input_formats(self):
        raw = "1101060F40060000B0040000"
        self.assertEqual(decode_hex("0x" + raw), decode_hex("11:01:06:0F:40:06:00:00:B0:04:00:00"))
        self.assertEqual(decode_hex("11 01 06 0F 40 06 00 00 B0 04 00 00"), decode_hex(raw))

    def test_invalid_lengths(self):
        for raw in (b"", b"\x00" * 11, b"\x00" * 13):
            with self.assertRaises(ValueError):
                decode_stats(raw)
        for raw in ("", "ZZ" * 12, "00" * 11, "00" * 13):
            with self.assertRaises(ValueError):
                decode_hex(raw)

    def test_json_cli(self):
        script = Path(__file__).resolve().parents[1] / "scripts" / "decode_ai_combat_stats.py"
        result = subprocess.run([sys.executable, str(script), "--json", "1101060F40060000B0040000"],
                                capture_output=True, text=True, check=True)
        obj = json.loads(result.stdout)[0]
        self.assertEqual(obj["attack"], 1600)
        self.assertEqual(obj["defense"], 1200)


if __name__ == "__main__":
    unittest.main()
