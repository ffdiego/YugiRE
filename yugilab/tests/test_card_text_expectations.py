"""Offline expectations are research hints, not executable native parity."""
from __future__ import annotations
import copy
import json
from pathlib import Path
import unittest

from yugilab.card_text_expectations import validate_expectations, temporal_candidates

ROOT = Path(__file__).resolve().parents[2]
EXPECTATIONS = ROOT / "docs/research/card-text-expectations.v1.json"


class CardTextExpectationsTests(unittest.TestCase):
    def setUp(self):
        self.ref = json.loads(EXPECTATIONS.read_text(encoding="utf-8"))
        self.cards = [
            dict(internalIndex=969, resourceId=1315,
                 name="Rain of Mercy", joeyAvailable=True),
            dict(internalIndex=414, resourceId=1306,
                 name="Skull Invitation", joeyAvailable=True),
        ]

    def test_validates_provenance_and_native_identity(self):
        a, h = validate_expectations(self.ref, self.cards)
        self.assertEqual(set(a), {969, 414})
        self.assertEqual(len(h), 1)
        self.assertEqual(a[414]["expectedMechanics"][0]["amountPerCard"], 300)
        self.assertEqual(a[969]["expectedMechanics"][0]["amountEach"], 1000)
        self.assertEqual(h[0]["conclusion"], "Unverified")

    def test_rejects_unmatched_name_and_id(self):
        altered = copy.deepcopy(self.ref)
        altered["cards"][0]["resourceId"] = 1306
        with self.assertRaisesRegex(ValueError, "identity"):
            validate_expectations(altered, self.cards)
        altered = copy.deepcopy(self.ref)
        altered["cards"][1]["name"] = "Different Card"
        with self.assertRaisesRegex(ValueError, "identity"):
            validate_expectations(altered, self.cards)
        altered = copy.deepcopy(self.ref)
        altered["cards"][0]["source"]["url"] = "http://unverified.example/test"
        with self.assertRaisesRegex(ValueError, "provenance"):
            validate_expectations(altered, self.cards)

    def test_temporal_cf_pair_is_never_declared_mechanic_evidence(self):
        annotations, hypotheses = validate_expectations(self.ref, self.cards)
        examples = {
            1306: [dict(kind="CF", seq=66, duelIndex=0, file="duels-a.jsonl"),
                   dict(kind="CF", seq=510, duelIndex=1, file="duels-a.jsonl")],
            1315: [dict(kind="CF", seq=335, duelIndex=0, file="duels-a.jsonl"),
                   dict(kind="CF", seq=1191, duelIndex=1, file="duels-a.jsonl")]
        }
        observed = temporal_candidates(hypotheses, annotations, examples)
        self.assertEqual(len(observed), 1)
        h = observed[0]
        self.assertEqual(h["observationClass"], "CFTemporalCooccurrenceOnly")
        self.assertEqual(h["conclusion"], "Unverified")
        self.assertEqual([p["ordering"] for p in h["candidatePairs"]],
                         ["B_before_A", "B_before_A"])
        self.assertIn("not proof", h["limitation"])

        examples[1306][1]["file"] = "different-capture.jsonl"
        observed = temporal_candidates(hypotheses, annotations, examples)
        self.assertEqual(len(observed[0]["candidatePairs"]), 1)


if __name__ == "__main__":
    unittest.main()
