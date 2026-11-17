"""Temporal graveyard/LP links are bounded observations, not effect proofs."""
from __future__ import annotations
import unittest
from yugilab.temporal_correlations import correlate_graveyard_lp300


def t(seq, side, kind, delta, duel=0):
    return dict(seq=seq, side=side, kind=kind, delta=delta, duelIndex=duel)


class TemporalCorrelationsTests(unittest.TestCase):
    def test_same_side_one_to_one_pairs_and_preserves_extra_damage(self):
        observations = [
            t(10,0,"graveyardCount",1), t(12,0,"lifePoints",-300),
            t(13,1,"graveyardCount",1), t(15,1,"lifePoints",-300),
            t(16,0,"graveyardCount",1), t(17,0,"lifePoints",-300),
            t(18,1,"lifePoints",-300)
        ]
        r = correlate_graveyard_lp300(observations)
        self.assertEqual(r["unitGraveyardIncreases"], 3)
        self.assertEqual(r["pairedCount"], 3)
        self.assertEqual([x["seqGap"] for x in r["pairs"]], [2,2,1])
        self.assertEqual(r["unpairedLpLoss300"], [
            dict(duelIndex=0, side=1, lossSeq=18, lossDelta=-300)])
        self.assertTrue(all(x["conclusion"] == "TemporalCorrelationOnly" for x in r["pairs"]))
        self.assertIn("does not show WHICH", r["limitation"])

    def test_grave_counts_do_not_cross_sides_or_duels(self):
        observations = [t(10,0,"graveyardCount",1),
                        t(12,1,"lifePoints",-300),
                        t(11,0,"lifePoints",-300,duel=1)]
        r = correlate_graveyard_lp300(observations)
        self.assertEqual(r["pairedCount"],0)
        self.assertEqual(len(r["unpairedGraveyardIncreases"]),1)
        self.assertEqual(len(r["unpairedLpLoss300"]),2)

    def test_timing_and_grave_count_ambiguity_do_not_pretend_match(self):
        observations = [t(10,0,"graveyardCount",2),
                        t(20,0,"graveyardCount",1),
                        t(19,0,"lifePoints",-300),
                        t(27,0,"lifePoints",-300)]
        r = correlate_graveyard_lp300(observations)
        self.assertEqual(r["pairedCount"],0)
        self.assertEqual(r["unitGraveyardIncreases"],1)
        self.assertEqual(len(r["unpairedLpLoss300"]),2)

    def test_rejects_invalid_window(self):
        for n in (0,33,-1,True,1.5):
            with self.subTest(window=n):
                with self.assertRaisesRegex(ValueError,"max_seq_gap"):
                    correlate_graveyard_lp300([],n)


if __name__ == "__main__":
    unittest.main()
