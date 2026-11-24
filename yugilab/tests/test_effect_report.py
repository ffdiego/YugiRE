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

    def test_raimei_command_then_exact_observed_lp_decrease(self):
        def native_event(seq, opcode, caller, life=None, resource=0):
            entry = command(seq, opcode, caller=caller, rid=resource)
            entry["threadId"] = 8440
            if life is not None:
                entry["lifePointsObserved"] = {
                    "lifePoints": life, "samplePoint": "command_producer_onEnter"
                }
            return entry
        events = [
            native_event(1, 0xCF, "0x57977f", resource=1007),
            native_event(2, 0x0D, "0x579984"),
            native_event(3, 0x72, "0x5783f2"),
            native_event(4, 0x8043, "0x578513", [8000, 8000], 300),
            native_event(5, 0x0E, "0x5799ef", [8000, 7700]),
            native_event(6, 0x0004, "0x5bc545"),
        ]
        with tempfile.TemporaryDirectory() as temp:
            capture = Path(temp) / "duels-native-p045.jsonl"
            records = [
                dict(kind="metadata", mode="live_duels", requestedDuels=1),
                *events,
                dict(kind="capture_summary", complete=True, overwritten=0,
                     capturedDuels=1, eventsWritten=6)
            ]
            capture.write_text("\n".join(json.dumps(x) for x in records) + "\n",
                               encoding="utf-8")
            item = analyze_capture(capture)
            self.assertTrue(item["integrityOk"])
            self.assertEqual(item["linkedLpCasesExact"], 1)
            self.assertEqual(len(item["linkedLpCases"]), 1)
            matched = item["linkedLpCases"][0]
            self.assertEqual((matched["cfSeq"], matched["damageSeq"], matched["afterSeq"]),
                             (1, 4, 5))
            self.assertEqual(matched["sourceResourceId"], 1007)
            self.assertEqual(matched["targetLifeIndex"], 1)
            self.assertEqual(matched["observedLoss"], [0, 300])
            self.assertTrue(matched["exactLpDelta"])
            self.assertEqual(report([capture])["linkedLpCasesExact"], 1)

            # Distinct observation: matching command without LP loss must
            # remain a mismatch, never a confirmed observed delta.
            events[4]["lifePointsObserved"]["lifePoints"] = [8000, 8000]
            capture.write_text("\n".join(json.dumps(x) for x in records) + "\n",
                               encoding="utf-8")
            item = analyze_capture(capture)
            self.assertEqual(item["linkedLpCasesExact"], 0)
            self.assertEqual(item["linkedLpCases"][0]["observedLoss"], [0, 0])
            self.assertFalse(item["linkedLpCases"][0]["exactLpDelta"])

    def test_unrelated_lp_producer_does_not_link_raimei(self):
        with tempfile.TemporaryDirectory() as temp:
            capture = Path(temp) / "duels-test.jsonl"
            self.write_capture(capture)
            result = analyze_capture(capture)
            self.assertEqual(result["linkedLpCases"], [])

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
