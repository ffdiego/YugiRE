"""Synthetic, offline regression tests for native command audit."""
import json
from pathlib import Path
import tempfile
import unittest

from yugilab.effect_report import analyze_capture, report


def command(seq, opcode, caller="0x5bc545", rid=0):
    return dict(kind="command", seq=seq, args=[opcode, rid, 0, 0], caller=caller)


class NativeEffectCommandAuditTests(unittest.TestCase):
    def write_capture(self, path, *, tamper=False):
        events = [
            command(1, 0xCF, caller="0x57977f", rid=342),
            command(2, 0xCE, caller="0x5abeab", rid=755),
            command(3, 0x43),
            command(4, 0x0004),
            command(5 if not tamper else 7, 0xCF, caller="0x57977f", rid=345),
            command(6, 0x43),
            command(7, 0x8004),
        ]
        records = [
            dict(kind="metadata", mode="live_duels", requestedDuels=2),
            *events,
            dict(kind="capture_summary", complete=True, overwritten=0,
                 capturedDuels=2, eventsWritten=7)
        ]
        path.write_text("\n".join(json.dumps(x) for x in records) + "\n", encoding="utf-8")

    def test_capture_integrity_and_candidates_not_causality(self):
        with tempfile.TemporaryDirectory() as temp:
            capture = Path(temp) / "duels-test.jsonl"
            self.write_capture(capture)
            item = analyze_capture(capture)
            self.assertTrue(item["integrityOk"])
            self.assertEqual(item["terminalCommands"], 2)
            self.assertEqual(item["lpCommandCount"], 2)
            self.assertEqual(item["commandCandidates"], {"CF:342": 1, "CF:345": 1})
            self.assertEqual(item["perDuel"][0]["candidateCommands"], {"CF:342": 1})
            self.assertEqual(item["perDuel"][1]["candidateCommands"], {"CF:345": 1})
            all_data = report([Path(temp)])
            self.assertEqual(all_data["captureCount"], 1)
            self.assertIn("NOT proven", all_data["interpretationLimit"])

    def test_missing_sequence_rejected_even_when_footer_says_complete(self):
        with tempfile.TemporaryDirectory() as temp:
            capture = Path(temp) / "duels-broken.jsonl"
            self.write_capture(capture, tamper=True)
            result = analyze_capture(capture)
            self.assertFalse(result["integrityOk"])
            self.assertTrue(any("Noncontiguous" in x for x in result["issues"]))

    def test_no_false_positive_from_unrelated_command(self):
        with tempfile.TemporaryDirectory() as temp:
            capture = Path(temp) / "duels-test.jsonl"
            self.write_capture(capture)
            lines = capture.read_text(encoding="utf-8").splitlines()
            event = json.loads(lines[1])
            event["caller"] = "0x123456"
            lines[1] = json.dumps(event)
            capture.write_text("\n".join(lines) + "\n", encoding="utf-8")
            item = analyze_capture(capture)
            self.assertEqual(item["commandCandidates"], {"CF:345": 1})


if __name__ == "__main__":
    unittest.main()
