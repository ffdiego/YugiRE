"""Synthetic offline state signatures; never use proprietary native files."""
from __future__ import annotations

import copy
import unittest

from yugilab.state_patterns import analyze_report

SOURCE = "capture-a.jsonl"
SHA = "a" * 64


def slot(idx, occ, associations=(), *, valid=True):
    return dict(cardIndex=idx, occurrenceWord=occ,
                associationCount=len(associations), associationCountValid=valid,
                associations=[dict(referenceWord=ref, metadataWord=meta)
                              for ref, meta in associations])


def field(seq, side, index, before, after, *, prev=None):
    return dict(kind="fieldSlot", seq=seq,
                previousSampleSeq=seq - 1 if prev is None else prev,
                side=side, slot=index, cardBefore=before["cardIndex"],
                cardAfter=after["cardIndex"], before=before, after=after)


def lp(seq, side, amount):
    return dict(kind="lifePoints", seq=seq, previousSampleSeq=seq - 1,
                side=side, before=8000, after=8000 + amount, delta=amount)


def report(transitions, *, duel_count=1):
    return dict(schema="yugire-native-state-interactions-report-v1",
                captures=[dict(file=SOURCE, sha256=SHA, integrityOk=True,
                               terminalCommands=duel_count)],
                transitions=[dict(file=SOURCE, sourceSha256=SHA, duelIndex=0, **t)
                             for t in transitions])


class StatePatternTests(unittest.TestCase):
    def test_detects_real_structures_without_claiming_effect_causality(self):
        empty = slot(0, 0)
        transitions = [
            field(10, 1, 5, empty, slot(987, 10)),
            field(20, 1, 10, empty, slot(990, 20)),
            field(21, 0, 1, slot(24, 5), slot(24, 5, [(2561, 8)])),
            field(21, 1, 0, slot(887, 0), slot(887, 0, [(2561, 8)])),
            field(30, 1, 1, slot(1038, 21), empty),
            field(30, 0, 1, empty, slot(1038, 21, [(370, 13)])),
        ] + [lp(seq, side, -500) for seq, side in
             [(11, 0), (14, 1), (17, 0), (19, 1), (22, 0), (25, 1)]]
        result = analyze_report(report(transitions))
        run = result["runs"][0]
        self.assertEqual(run["counts"], dict(crossSideTransfers=1,
                                             sharedAssociations=1,
                                             alternatingLpLosses=1))
        transfer = run["crossSideTransfers"][0]
        self.assertEqual((transfer["fromSide"], transfer["toSide"], transfer["cardIndex"],
                          transfer["occurrenceWord"]), (1, 0, 1038, 21))
        self.assertEqual(transfer["destinationAssociations"],
                         [dict(referenceWord=370, metadataWord=13)])
        self.assertEqual(run["sharedAssociations"][0]["referenceWord"], 2561)
        losses = run["alternatingLpLosses"][0]
        self.assertEqual(losses["delta"], -500)
        self.assertEqual(losses["occurrences"], 6)
        self.assertEqual([x["cardIndex"] for x in
                          losses["candidateObservedFieldResidencies"]], [987])
        self.assertIn("NotCardOrPhaseAttribution", losses["conclusion"])
        self.assertIn("NotEffectAttribution", transfer["conclusion"])

    def test_no_transfer_without_matching_nonzero_occurrence_or_same_sample(self):
        empty = slot(0, 0)
        cases = [
            [field(10, 1, 1, slot(1038, 21), empty),
             field(10, 0, 1, empty, slot(1038, 22))],
            [field(10, 1, 1, slot(1038, 21), empty),
             field(11, 0, 1, empty, slot(1038, 21))],
            [field(10, 1, 1, slot(1038, 0), empty),
             field(10, 0, 1, empty, slot(1038, 0))],
            [field(10, 1, 1, slot(1038, 21), empty),
             field(10, 1, 2, empty, slot(1038, 21))],
            [field(10, 1, 1, slot(1038, 21), empty),
             field(10, 0, 1, empty, slot(1038, 21), prev=8)],
            [field(10, 1, 1, slot(1038, 21), empty),
             field(10, 0, 1, empty, slot(1038, 21)),
             field(10, 0, 2, empty, slot(1038, 21))],
        ]
        for case in cases:
            with self.subTest(case=case):
                self.assertEqual(
                    analyze_report(report(case))["runs"][0]["crossSideTransfers"], [])

    def test_shared_association_requires_both_sides_valid_and_new(self):
        a = field(11, 0, 1, slot(24, 5), slot(24, 5, [(2561, 8)]))
        b = field(11, 1, 0, slot(887, 7), slot(887, 7, [(2561, 8)]))
        self.assertEqual(len(analyze_report(report([a, b]))["runs"][0]
                             ["sharedAssociations"]), 1)
        for changes in [
            [a, field(12, 1, 0, slot(887, 7), slot(887, 7, [(2561, 8)]))],
            [a, field(11, 0, 2, slot(887, 7), slot(887, 7, [(2561, 8)]))],
            [a, field(11, 1, 0, slot(887, 7, [(2561, 8)]),
                      slot(887, 7, [(2561, 8)]))],
            [a, field(11, 1, 0, slot(887, 7),
                      slot(887, 7, [(2561, 8)], valid=False))],
        ]:
            with self.subTest(changes=changes):
                self.assertEqual(
                    analyze_report(report(changes))["runs"][0]["sharedAssociations"], [])

    def test_lp_pattern_not_card_causality_or_silent_field_drift(self):
        empty = slot(0, 0)
        series = [lp(10, 0, -500), lp(20, 1, -500),
                  lp(30, 0, -500), lp(40, 1, -500)]
        for extras, expected in [
            ([], []),
            ([field(5, 1, 5, empty, slot(987, 10))], [987]),
            ([field(5, 1, 5, empty, slot(987, 10)),
              field(35, 1, 5, slot(987, 10), empty)], []),
            ([field(5, 1, 5, empty, slot(987, 10)),
              field(6, 1, 6, empty, slot(831, 12))], [987, 831]),
        ]:
            with self.subTest(extras=extras):
                run = analyze_report(report(series + extras))["runs"][0]
                self.assertEqual([x["cardIndex"] for x in
                                  run["alternatingLpLosses"][0]
                                  ["candidateObservedFieldResidencies"]], expected)
        repeated = series + [lp(50, 1, -500)]
        self.assertEqual(
            analyze_report(report(repeated))["runs"][0]["alternatingLpLosses"], [])
        self.assertEqual(
            analyze_report(report(series[:3]))["runs"][0]["alternatingLpLosses"], [])

    def test_input_schema_fingerprint_duel_and_integrity_gate(self):
        base = report([lp(11, 0, -500)])
        for alteration in [
            ("schema", "unknown"),
            ("captures", [dict(file=SOURCE, sha256=SHA,
                               integrityOk=False, terminalCommands=1)]),
            ("transitions", [dict(file=SOURCE, sourceSha256="bad",
                                  duelIndex=0, **lp(11, 0, -500))]),
            ("transitions", [dict(file=SOURCE, sourceSha256=SHA,
                                  duelIndex=2, **lp(11, 0, -500))]),
        ]:
            modified = copy.deepcopy(base)
            modified[alteration[0]] = alteration[1]
            with self.subTest(alteration=alteration):
                with self.assertRaises(ValueError):
                    analyze_report(modified)
        with self.assertRaises(ValueError):
            analyze_report(base, min_lp_occurrences=3)


if __name__ == "__main__":
    unittest.main()
