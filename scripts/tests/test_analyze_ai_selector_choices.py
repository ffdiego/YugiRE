"""Offline tests of selector-entry snapshot analysis; no Joey / Frida needed."""
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from analyze_ai_selector_choices import scan_capture


class SelectorChoiceTests(unittest.TestCase):
    def scan(self, events):
        with tempfile.TemporaryDirectory() as directory:
            filename = Path(directory) / "duels.jsonl"
            filename.write_text("\n".join(json.dumps(e) for e in events) + "\n",
                                encoding="utf-8")
            return scan_capture(filename)

    def test_selected_card_and_executor_match_entry_hand(self):
        events = [
            {"kind": "demo_init", "function": "scene_config",
             "phase": "enter", "requestedSceneMode": 7, "seq": 1},
            {"kind": "ai_probe", "function": "select_normal", "phase": "enter",
             "seq": 2, "threadId": 4, "callId": 22, "side": 0,
             "handCount": 2, "handIndices": [101, 202],
             "handWords": [101, 202], "pass": 0},
            {"kind": "ai_probe", "function": "select_normal", "phase": "leave",
             "seq": 3, "threadId": 4, "callId": 22, "side": 0,
             "selectedHandIndex": 1, "selectedCardIndexAtEntry": 202},
            {"kind": "demo_executor", "function": "normal_placement",
             "phase": "enter", "seq": 4, "threadId": 4, "side": 0,
             "handIndex": 1, "handInternalIndex": 202},
        ]
        rows, stats = self.scan(events)
        self.assertEqual(stats["scenes"], 1)
        self.assertEqual(stats["selectors"], 1)
        self.assertEqual(stats["handSnapshots"], 1)
        self.assertEqual(stats["identifiedChoices"], 1)
        self.assertEqual(stats["forwardedToExecutor"], 1)
        self.assertEqual(stats["matchedToExecutor"], 1)
        self.assertEqual(stats["mismatchedToExecutor"], 0)
        self.assertEqual(rows[0]["selectedCardWordAtEntry"], 202)

    def test_negative_selection_has_no_card(self):
        events = [
            {"kind": "ai_probe", "function": "select_priority", "phase": "enter",
             "seq": 1, "threadId": 1, "callId": 1, "side": 1,
             "handCount": 1, "handIndices": [500], "handWords": [500]},
            {"kind": "ai_probe", "function": "select_priority", "phase": "leave",
             "seq": 2, "threadId": 1, "callId": 1, "side": 1,
             "selectedHandIndex": -1}
        ]
        rows, stats = self.scan(events)
        self.assertIsNone(rows[0]["selectedCardIndexAtEntry"])
        self.assertEqual(stats["identifiedChoices"], 0)

    def test_no_hand_read_does_not_guess_card(self):
        events = [
            {"kind": "ai_probe", "function": "select_normal", "phase": "enter",
             "seq": 1, "threadId": 1, "callId": 1, "side": 0,
             "duelRunning": False},
            {"kind": "ai_probe", "function": "select_normal", "phase": "leave",
             "seq": 2, "threadId": 1, "callId": 1, "side": 0,
             "selectedHandIndex": 0}
        ]
        rows, stats = self.scan(events)
        self.assertEqual(stats["handSnapshots"], 0)
        self.assertIsNone(rows[0]["selectedCardIndexAtEntry"])

    def test_mismatch_between_return_and_recorded_choice_fails(self):
        events = [
            {"kind": "ai_probe", "function": "select_normal", "phase": "enter",
             "seq": 1, "threadId": 1, "callId": 1, "side": 0,
             "handIndices": [111], "handWords": [111], "handCount": 1},
            {"kind": "ai_probe", "function": "select_normal", "phase": "leave",
             "seq": 2, "threadId": 1, "callId": 1, "side": 0,
             "selectedHandIndex": 0, "selectedCardIndexAtEntry": 999}
        ]
        with self.assertRaisesRegex(ValueError, "Selected card mismatch"):
            self.scan(events)


if __name__ == "__main__":
    unittest.main()
