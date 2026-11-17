"""Synthetic offline audit: Sogen presence is not a causal conclusion."""
from __future__ import annotations

import json
from pathlib import Path
import tempfile
import unittest

from yugilab.battle_attribute_audit import analyze, audit_capture, _attributes


def field(slot, idx=0, occ=0, refs=None):
    associations = [{"referenceWord": r, "metadataWord": m} for r, m in (refs or [])]
    return dict(slot=slot, cardIndex=idx, occurrenceWord=occ,
                positionWord=2 if idx else 0, flagsWord=0,
                associationCount=len(associations),
                associationCountValid=True, associations=associations)


def sides():
    a = [field(i) for i in range(11)]
    b = [field(i) for i in range(11)]
    a[10] = field(10, 490, 4)
    a[0] = field(0, 124, 9, [(0x0A00, 8)])
    b[1] = field(1, 169, 7)
    return [dict(side=j, fieldSlots=x, handIndices=[]) for j, x in enumerate((a, b))]


def fake_capture():
    return [
        dict(kind="metadata", battleAttributesTrace=dict(enabled=True)),
        dict(kind="command", seq=1, opcodeMasked="0x0031",
             stateObservation=dict(kind="baseline", sides=sides()),
             battleAttributesObservation=dict(
                 schema="joey-command-entry-battle-attributes-v1",
                 selectorWord=0,
                 blocks=[
                     dict(blockIndex=0, cardIndexWord=124, descriptorWord=16,
                          calculatedAttack=1600, calculatedDefense=1900,
                          damageWord=200),
                     dict(blockIndex=1, cardIndexWord=169, descriptorWord=0,
                          calculatedAttack=1500, calculatedDefense=800,
                          damageWord=200)])),
        dict(kind="command", seq=2, opcodeMasked="0x0004"),
        dict(kind="capture_summary", complete=True, overwritten=0,
             stateTrace=dict(readErrors=0),
             battleAttributesTrace=dict(samples=1, readErrors=0)),
    ]


class BattleAttributeAuditTests(unittest.TestCase):
    def make(self, d: Path, *, rows=None):
        path = d/"duels-example.jsonl"
        path.write_text("\n".join(json.dumps(x) for x in (rows or fake_capture()))+"\n",
                        encoding="utf-8")
        attrs = d/"attributes.json"
        attrs.write_text(json.dumps(dict(registryVersion=1,cards=[
            dict(cardRef="card:124",attack=1400,defense=1700,classCode=9),
            dict(cardRef="card:169",attack=1500,defense=800,classCode=3)
        ])), encoding="utf-8")
        return path,attrs

    def test_computed_minus_base_and_field_context_keep_uncertainty(self):
        with tempfile.TemporaryDirectory() as td:
            p, attrs = self.make(Path(td))
            report = analyze([p],attrs)
            self.assertEqual(report["schema"],"yugire-native-battle-attributes-audit-v1")
            sample = report["captures"][0]["samples"][0]
            self.assertEqual(sample["fieldSpellObserved"][0]["side"],0)
            self.assertEqual(sample["rawFieldAssociationCandidates"][0]
                             ["referenceWord"],0x0A00)
            self.assertEqual(sample["blocks"][0]["calculatedMinusBaseAttack"],200)
            self.assertEqual(sample["blocks"][0]["calculatedMinusBaseDefense"],200)
            self.assertEqual(sample["blocks"][0]["fieldMatches"][0]["side"],0)
            self.assertEqual(sample["blocks"][1]["calculatedMinusBaseAttack"],0)
            self.assertIn("not",report["interpretationLimit"])

    def test_fail_closed_on_missing_samples_incomplete_and_corrupt_capture(self):
        with tempfile.TemporaryDirectory() as td:
            d=Path(td)
            rows=fake_capture()
            rows[1]["battleAttributesObservation"]=None
            p,attrs=self.make(d,rows=rows)
            with self.assertRaisesRegex(ValueError,"Missing/incomplete"):
                analyze([p],attrs)
            rows=fake_capture()
            rows[-1]["battleAttributesTrace"]["readErrors"]=1
            p,attrs=self.make(d,rows=rows)
            with self.assertRaisesRegex(ValueError,"Incomplete or unreadable"):
                analyze([p],attrs)
            rows=fake_capture()
            rows[-1]["battleAttributesTrace"]["samples"]=2
            p,attrs=self.make(d,rows=rows)
            with self.assertRaisesRegex(ValueError,"disagree"):
                analyze([p],attrs)

    def test_rejects_duplicate_and_unversioned_attribute_rows(self):
        bad = dict(registryVersion=1,cards=[
            dict(cardRef="card:2"),dict(cardRef="card:2")])
        with self.assertRaisesRegex(ValueError,"Duplicate"):
            _attributes(bad)
        with self.assertRaisesRegex(ValueError,"registryVersion"):
            _attributes(dict(cards=[]))


if __name__ == "__main__":
    unittest.main()
