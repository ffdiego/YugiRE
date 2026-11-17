"""Synthetic offline tests of bounded CF command windows."""
from __future__ import annotations
import json
from pathlib import Path
import tempfile
import unittest

from yugilab.inspect_effect_windows import extract, markdown


def command(seq, code, *, caller="0x123456", rid=0, lp=None):
    item = dict(kind="command", seq=seq, caller=caller, threadId=123,
                args=[code, rid, 0, 0])
    if lp is not None:
        item["lifePointsObserved"] = {"lifePoints": lp}
    return item


class EffectWindowsTests(unittest.TestCase):
    def make_capture(self, path, *, broken=False):
        events = [
            command(1, 0xCF, caller="0x57977f", rid=1315),
            command(2, 0x0042, caller="0x578823", rid=1000, lp=[8000, 7500]),
            command(3, 0x8042, caller="0x578823", rid=1000, lp=[9000, 7500]),
            command(4, 0x12, lp=[9000, 8500]),
            command(5, 0x04),
            command(6, 0xCF, caller="0x57977f", rid=1023),
            command(7, 0x04),
        ]
        records = [
            dict(kind="metadata", mode="live_duels", requestedDuels=2),
            *events,
            dict(kind="capture_summary", complete=not broken, overwritten=0,
                 capturedDuels=2, eventsWritten=7),
        ]
        path.write_text("\n".join(json.dumps(x) for x in records) + "\n",
                        encoding="utf-8")

    def test_cf_rain_window_preserves_lp_deltas_and_duel_boundary(self):
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "duels-test.jsonl"
            self.make_capture(path)
            result = extract([path], {1315}, before=2, after=100)
            self.assertEqual(len(result["windows"]), 1)
            w = result["windows"][0]
            self.assertEqual(w["cfSeq"], 1)
            self.assertEqual([c["seq"] for c in w["commands"]], [1, 2, 3, 4, 5])
            self.assertEqual(len(w["lpChangesWithinWindow"]), 2)
            self.assertEqual([c["delta"] for c in w["lpChangesWithinWindow"]],
                             [[1000, 0], [0, 1000]])
            self.assertIn("not establish causality", markdown(result))
            self.assertIn("not proof", result["interpretationLimit"])

    def test_rain_pair_with_prior_and_later_native_lp_samples(self):
        events = [
            command(1, 0x50, lp=[5000, 3000]),
            command(2, 0xCF, caller="0x57977f", rid=1315),
            command(3, 0x0D),
            command(4, 0x72),
            command(5, 0x42, caller="0x578823", rid=1000),
            command(6, 0x8042, caller="0x578823", rid=1000),
            command(7, 0x0E, lp=[6000, 4000]),
            command(8, 0x43, caller="0x578513", rid=300, lp=[6000, 4000]),
            command(9, 0x12, lp=[5700, 4000]),
            command(10, 0x04),
        ]
        records = [
            dict(kind="metadata", mode="live_duels", requestedDuels=1),
            *events,
            dict(kind="capture_summary", complete=True, overwritten=0,
                 capturedDuels=1, eventsWritten=len(events)),
        ]
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "duels-test.jsonl"
            def write():
                path.write_text("\n".join(json.dumps(x) for x in records) + "\n",
                                encoding="utf-8")
            write()
            result = extract([path], {1315})
            self.assertEqual(len(result["windows"]), 1)
            pair = result["windows"][0]["pairedLp42Candidate"]
            self.assertEqual(pair["commandSeq"], [5, 6])
            self.assertEqual(pair["targetSides"], [0, 1])
            self.assertEqual(pair["amounts"], [1000, 1000])
            self.assertEqual(pair["beforeCfLpSample"]["lifePoints"], [5000, 3000])
            self.assertEqual(pair["firstLpSampleAfterPair"]["lifePoints"], [6000, 4000])
            self.assertEqual(pair["netLpDeltaBetweenSamples"], [1000, 1000])
            self.assertEqual(pair["otherLpCommandsBetweenSamples"], [])
            self.assertIn("NOT proven effect", markdown(result))

            # Command pair can be observed even without an earlier LP sample.
            events[0].pop("lifePointsObserved")
            write()
            pair = extract([path], {1315})["windows"][0]["pairedLp42Candidate"]
            self.assertIsNone(pair["beforeCfLpSample"])
            self.assertIsNone(pair["netLpDeltaBetweenSamples"])

            # Mismatched amounts are not the paired fixed-amount signature.
            events[5]["args"][1] = 700
            write()
            self.assertIsNone(
                extract([path], {1315})["windows"][0]["pairedLp42Candidate"])

    def test_rejects_broken_capture_and_invalid_options(self):
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "duels-test.jsonl"
            self.make_capture(path, broken=True)
            with self.assertRaisesRegex(ValueError, "Capture invalid"):
                extract([path], {1315})
            with self.assertRaisesRegex(ValueError, "resource IDs"):
                extract([path], {0})
            with self.assertRaisesRegex(ValueError, "0..100"):
                extract([path], {1315}, after=101)


if __name__ == "__main__":
    unittest.main()
