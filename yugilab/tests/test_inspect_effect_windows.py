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
            command(2, 0x0043, rid=1000, lp=[8000, 7500]),
            command(3, 0x8043, rid=1000, lp=[9000, 7500]),
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
