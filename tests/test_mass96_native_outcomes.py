"""Offline tests of native Gravity Bind / terminal-outcome classification."""
from __future__ import annotations

import json
from pathlib import Path
import tempfile
import unittest
import zipfile

from scripts.analyze_mass96_outcomes import analyze_events, analyze_zip


def command(seq, opcode, observation=None, resource=None):
    return {
        "kind": "command", "seq": seq, "opcodeMasked": opcode,
        "args": [0, resource or 0, 0, 0],
        **({"stateObservation": observation} if observation is not None else {}),
    }


def baseline(lp0=8000, lp1=8000, deck0=3, deck1=3):
    return {"kind": "baseline", "sides": [
        {"side": 0, "lifePoints": lp0, "deckCount": deck0,
         "graveyardCount": 0, "terminalFlags": 0},
        {"side": 1, "lifePoints": lp1, "deckCount": deck1,
         "graveyardCount": 0, "terminalFlags": 0},
    ]}


def delta(side, **updates):
    return {"kind": "delta", "changes": [
        {"side": side, **{
            key: {"before": 1, "after": value} for key, value in updates.items()
        }}
    ]}


def fixture():
    # Duel 1: Gravity Bind effect + both decks empty with positive LP.
    # Duel 2: card is in deck, but there is no effect command and LP reaches 0.
    # Duel 3: Gravity effect occurs; still ends by damage, not exhaustion.
    return [
        {"kind": "metadata", "requestedDuels": 3,
         "deckPlans": {"0": {"main": [244]}, "1": {"main": [244]}}},
        command(1, "0x0001", baseline()),
        command(2, "0x0072", resource=(0xF1 << 16) | 1323),
        command(3, "0x0055", delta(0, deckCount=0)),
        command(4, "0x0055", delta(1, deckCount=0)),
        command(5, "0x0004", delta(0, terminalFlags=256)),
        command(6, "0x0001", baseline()),
        command(7, "0x0055", delta(1, lifePoints=0)),
        command(8, "0x0004"),
        command(9, "0x0001", baseline(deck0=0, deck1=0)),
        command(10, "0x0072", resource=1323),
        command(11, "0x0055", delta(1, lifePoints=0)),
        command(12, "0x0004"),
        {"kind": "capture_summary", "complete": True, "capturedDuels": 3,
         "overwritten": 0, "stateTrace": {"readErrors": 0}},
    ]


class Mass96OutcomeTests(unittest.TestCase):
    def test_rule_distinguishes_presence_effect_and_terminal_reason(self):
        result = analyze_events(fixture(), 11)
        self.assertEqual(len(result), 3)
        self.assertEqual([d["outcomeEvidence"] for d in result], [
            "both-decks-empty-positive-lp", "life-points-zero", "life-points-zero"
        ])
        self.assertEqual([d["gravityBindEffectCommands"] for d in result],
                         [1, 0, 1])
        self.assertEqual(result[0]["gravityBindEffectSequences"], [2])
        self.assertEqual(result[0]["side0"]["terminalFlags"], 256)
        self.assertEqual(result[0]["side1"]["deckCount"], 0)
        self.assertEqual(result[1]["side1"]["lifePoints"], 0)

    def test_end_to_end_zip_analysis_and_source_hash(self):
        with tempfile.TemporaryDirectory() as td:
            path = Path(td) / "captures.zip"
            with zipfile.ZipFile(path, "w") as archive:
                archive.writestr(
                    "mass96/cohort-11-rep-01/cohort-11-A_cohort-11-B_123_v2.jsonl",
                    "\n".join(json.dumps(x) for x in fixture())+"\n")
            result = analyze_zip(path)
            self.assertEqual(result["cohortsCount"], 1)
            self.assertEqual(result["duelsCount"], 3)
            self.assertEqual(result["gravityBindActivationDuels"], 2)
            self.assertEqual(result["gravityBindAndBothDecksEmpty"], 1)
            self.assertEqual(result["outcomeEvidenceCounts"],
                             {"both-decks-empty-positive-lp": 1,
                              "life-points-zero": 2})
            self.assertEqual(len(result["sourceSha256"]), 64)

    def test_reject_incomplete_and_missing_baseline(self):
        rows = fixture()
        for broken in (
            [*rows[:-1], {**rows[-1], "overwritten": 1}],
            [*rows[:-1], {**rows[-1], "capturedDuels": 4}],
            [*rows[:-1], {**rows[-1], "stateTrace": {"readErrors": 1}}],
        ):
            with self.subTest(reason=broken[-1]), self.assertRaises(ValueError):
                analyze_events(broken, 11)
        empty = [
            {"kind": "metadata", "requestedDuels": 1},
            command(1, "0x0004"),
            {"kind": "capture_summary", "complete": True,
             "capturedDuels": 1, "overwritten": 0,
             "stateTrace": {"readErrors": 0}},
        ]
        with self.assertRaises(ValueError):
            analyze_events(empty, 11)


if __name__ == "__main__":
    unittest.main()
