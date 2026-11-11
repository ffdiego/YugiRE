"""Offline schema/correlation tests. No native program or hypothetical calls."""
from copy import deepcopy
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from ai_decision_trace_audit import audit


class AiDecisionTraceAuditTests(unittest.TestCase):
    def fixture(self):
        events = [
            {"kind": "ai_probe", "phase": "enter", "function": "select_normal", "threadId": 7,
             "side": 0, "callId": 1, "pass": -1, "handCount": 1, "handWords": [550], "handIndices": [550]},
            {"kind": "ai_probe", "phase": "leave", "function": "computed_stats", "threadId": 7,
             "side": 0, "withinSelector": True, "rawArgs": [0, 0], "outputHex": "260212056C07000084030000"},
            {"kind": "ai_probe", "phase": "leave", "function": "combat_event", "threadId": 7,
             "side": 0, "withinSelector": True, "rawArgs": [0, 0, 0], "outputHex": "0808C800"},
            {"kind": "ai_probe", "phase": "leave", "function": "select_normal", "threadId": 7,
             "side": 0, "callId": 1, "selectedHandIndex": 0, "selectedCardIndexAtEntry": 550},
            {"kind": "command", "threadId": 7, "args": [0xCE, 0, 0, 0], "ceNativeState": {"source": 550}},
        ]
        for index, event in enumerate(events, 1):
            event["seq"] = index
        return [{"kind": "metadata", "seen": 5, "retained": 5, "overwritten": 0}, *events]

    def test_window_decoding_and_temporal_link(self):
        result = audit(self.fixture())
        self.assertEqual(result["calls"][0]["stats"][0]["decoded"]["attack"], 1900)
        self.assertEqual(result["calls"][0]["combatEvents"][0]["decoded"]["score_signed_i16"], 200)
        self.assertTrue(result["ceTemporalLinks"][0]["sourceMatchesLastValid"])
        self.assertIn("no decision reproduced", result["scope"])

    def test_thread_and_side_isolation(self):
        for name, change in [("thread", lambda e: e.update(threadId=8)),
                             ("side", lambda e: e.update(args=[0x80CE, 0, 0, 0]))]:
            data = self.fixture()
            change(data[-1])
            with self.subTest(name=name):
                self.assertIsNone(audit(data)["ceTemporalLinks"][0]["selectionSeq"])

    def test_fail_closed_integrity_and_hand_resolution(self):
        for mutate in [lambda d: d[0].update(overwritten=1),
                       lambda d: d[2].update(seq=99),
                       lambda d: d[0].update(seen=8),
                       lambda d: d[4].update(selectedCardIndexAtEntry=551),
                       lambda d: d[4].update(threadId=8),
                       lambda d: d[1].update(handIndices=[551])]:
            data = self.fixture()
            mutate(data)
            with self.assertRaises(ValueError):
                audit(data)

    def test_negative_selection_does_not_become_last_valid(self):
        data = self.fixture()
        data[4]["selectedHandIndex"] = -1
        del data[4]["selectedCardIndexAtEntry"]
        result = audit(data)
        self.assertEqual(result["normalNegative"], 1)
        self.assertIsNone(result["ceTemporalLinks"][0]["selectionSeq"])

    def test_mismatch_is_reported_not_hidden(self):
        data = self.fixture()
        data[-1]["ceNativeState"]["source"] = 551
        self.assertFalse(audit(data)["ceTemporalLinks"][0]["sourceMatchesLastValid"])

    def test_read_failure_remains_explicit(self):
        data = self.fixture()
        data[2]["readError"] = "unreadable"
        result = audit(data)
        self.assertEqual(result["readFailures"], [{"seq": 2, "reason": "output readError"}])
        self.assertEqual(result["calls"][0]["stats"], [])

    def test_unclosed_selector_rejected(self):
        data = self.fixture()
        del data[4]
        data[0].update(seen=4, retained=4)
        for index, event in enumerate(data[1:], 1):
            event["seq"] = index
        with self.assertRaises(ValueError):
            audit(data)


if __name__ == "__main__":
    unittest.main()
