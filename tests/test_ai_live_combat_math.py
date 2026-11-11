"""Real passive combat buffers as an oracle for format and utility primitives.

Does not execute Joey and does not claim to reproduce a hand selection.
The original full JSONL is optional; the reduced observed fixture is tracked.
"""
import hashlib
import json
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
from decode_ai_combat_event import decode_hex
from decode_ai_combat_stats import decode_hex as decode_stats_hex


class LiveCombatMathTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.fixture = json.loads((ROOT / "src/tests/Joey.Tests/Fixtures/ai-live-combat-events.json").read_text())

    def test_all_observed_utility_bits_match_known_native_predicate(self):
        self.assertEqual(len(self.fixture["events"]), 22)
        for event in self.fixture["events"]:
            with self.subTest(seq=event["seq"]):
                decoded = decode_hex(event["outputHex"])
                self.assertFalse(decoded.special_target_path)
                self.assertEqual(decoded.useful_flag, decoded.base_useful_condition)
                self.assertEqual(decoded.score_signed_i16, event["expectedSignedScore"])
                self.assertEqual((decoded.attacker_slot, decoded.target_slot), tuple(event["rawArgs"][1:3]))

    def test_zero_score_can_remove_opponent_without_losing_attacker(self):
        by_seq = {event["seq"]: decode_hex(event["outputHex"]) for event in self.fixture["events"]}
        self.assertTrue(by_seq[304].useful_flag)
        self.assertEqual(by_seq[304].score_signed_i16, 0)
        self.assertTrue(by_seq[304].defender_removed_in_simulation)
        self.assertFalse(by_seq[304].attacker_removed_in_simulation)
        self.assertFalse(by_seq[162].useful_flag)
        self.assertTrue(by_seq[162].defender_removed_in_simulation)
        self.assertTrue(by_seq[162].attacker_removed_in_simulation)

    def test_event25_is_compatible_with_captured_1900_minus_1700(self):
        first = self.fixture["events"][0]
        self.assertEqual(first["seq"], 25)
        attacker, target = [decode_stats_hex(event["outputHex"]) for event in self.fixture["event25PrecedingComputedStats"]]
        self.assertEqual((attacker.internal_card_index, attacker.attack), (550, 1900))
        self.assertEqual((target.internal_card_index, target.attack), (24, 1700))
        self.assertEqual(decode_hex(first["outputHex"]).score_signed_i16, attacker.attack - target.attack)

    def test_reduced_rows_are_identical_to_original_capture_when_available(self):
        source = ROOT / "scratch/traces" / self.fixture["sourceCapture"]
        if not source.is_file():
            self.skipTest("Full original capture is optional and not versioned")
        self.assertEqual(hashlib.sha256(source.read_bytes()).hexdigest(), self.fixture["sourceCaptureSha256"])
        records = {row["seq"]: row for line in source.read_text().splitlines() if "seq" in (row := json.loads(line))}
        for event in self.fixture["events"] + self.fixture["event25PrecedingComputedStats"]:
            for key, value in event.items():
                if key != "expectedSignedScore":
                    self.assertEqual(value, records[event["seq"]][key])


if __name__ == "__main__":
    unittest.main()
