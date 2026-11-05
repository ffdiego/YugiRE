"""Offline tests for the native 4-byte hypothetical combat event format."""
from pathlib import Path
import json
import subprocess
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from decode_ai_combat_event import decode_event, decode_hex


class CombatEventDecoderTests(unittest.TestCase):
    def test_attacker_target_score_and_defender_removed(self):
        e = decode_hex("A8096400")  # low=0x09A8; attacker 2, target 3, score 100
        self.assertEqual((e.attacker_slot, e.target_slot), (2, 3))
        self.assertEqual(e.score_signed_i16, 100)
        self.assertTrue(e.useful_flag)
        self.assertTrue(e.defender_removed_in_simulation)
        self.assertFalse(e.attacker_removed_in_simulation)
        self.assertTrue(e.base_useful_condition)
        self.assertEqual(e.unknown_flags_hex, "0x0000")

    def test_signed_negative(self):
        e = decode_hex("200406FF")  # score -250, attacker removed
        self.assertEqual(e.score_signed_i16, -250)
        self.assertEqual(e.score_raw_u16, 0xFF06)
        self.assertTrue(e.attacker_removed_in_simulation)
        self.assertFalse(e.base_useful_condition)

    def test_zero_score_destruction_utility(self):
        e = decode_hex("08080000")
        self.assertEqual(e.score_signed_i16, 0)
        self.assertTrue(e.defender_removed_in_simulation)
        self.assertTrue(e.base_useful_condition)
        both = decode_hex("000C0000")
        self.assertFalse(both.base_useful_condition)

    def test_special_target_does_not_invent_target(self):
        e = decode_hex("12004006")
        self.assertEqual(e.attacker_slot, 1)
        self.assertIsNone(e.target_slot)
        self.assertTrue(e.special_target_path)
        self.assertEqual(e.score_signed_i16, 1600)
        self.assertIsNone(e.base_useful_condition)

    def test_unknown_and_outer_selection(self):
        e = decode_hex("0x0C100000")
        self.assertTrue(e.selected_by_outer_loop)
        self.assertTrue(e.useful_flag)
        self.assertEqual(e.unknown_flags_hex, "0x1000")

    def test_reject_malformed_and_non_four_bytes(self):
        self.assertEqual(decode_hex("A8-09-64-00"), decode_hex("A8:09:64:00"))
        for value in ("", "123", "0000000000", "GG000000"):
            with self.subTest(value=value), self.assertRaises(ValueError):
                decode_hex(value)
        for value in (b"", b"\x00", b"\x00" * 5):
            with self.assertRaises(ValueError):
                decode_event(value)

    def test_json_cli(self):
        script = Path(__file__).resolve().parents[1] / "scripts" / "decode_ai_combat_event.py"
        result = subprocess.run([sys.executable, str(script), "--json", "08080000"],
                                text=True, capture_output=True, check=True)
        event = json.loads(result.stdout)[0]
        self.assertEqual(event["score_signed_i16"], 0)
        self.assertTrue(event["base_useful_condition"])


if __name__ == "__main__":
    unittest.main()
