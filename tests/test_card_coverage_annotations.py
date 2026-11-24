"""Offline overlays: immutable evidence, conflicts, promotion gates and all output bytes."""
import copy
import json
import unittest
from pathlib import Path

from scripts.coverage.annotations import merge_annotations
from scripts.generate_card_coverage import (ROOT, SOURCE_COMMIT, CALLBACK_PATH, artifacts,
    blob, evidence, generate)

PILOT = ROOT / 'docs/coverage/card-coverage.annotations.json'


def fixture():
    pilot = json.loads(PILOT.read_text())
    identities = [c['identity'] for c in pilot['cards']] + [dict(internalIndex=758, resourceId=345, name='Ookazi')]
    extracted = dict(fingerprints={k: v for k, v in pilot['fingerprints'].items() if k != 'exeSha256'},
                     packSha256='a'*64, cards=[dict(i, properties=0, classCode=0, attack=0, defense=0, level=0, pack=0) for i in identities])
    ledger = [evidence('callbacks', CALLBACK_PATH, 'StaticAnalysis', 'Pinned table'),
              evidence('extraction', 'src/Joey.Core/Archive.cs', 'SourceInspection', 'Pinned decoder'),
              evidence('pack', 'docs/24-dat-graphics.md', 'StaticAnalysis', 'Pinned pack interpretation')]
    raw, aux = generate(extracted, json.loads(blob(CALLBACK_PATH)), SOURCE_COMMIT, ledger)
    return raw, aux, pilot


def permute(value):
    if isinstance(value, dict):
        result = {k: permute(v) for k, v in reversed(list(value.items()))}
        for k in result:
            if isinstance(result[k], list) and (k.endswith('Refs') or k in ('cards', 'mechanics', 'evidence', 'dependencies')):
                result[k].reverse()
        return result
    if isinstance(value, list):
        return [permute(v) for v in value]
    return value


class AnnotationTests(unittest.TestCase):
    def test_pilot_and_raw_independence(self):
        raw, aux, pilot = fixture()
        before = copy.deepcopy((raw, aux, pilot))
        outputs = artifacts(raw, aux, [pilot])
        self.assertEqual((raw, aux, pilot), before)
        for name, data in artifacts(raw, aux).items():
            self.assertEqual(data, outputs[name], name)
        merged = json.loads(outputs['registry.json'])
        self.assertEqual(len(merged['cards']), 6)
        self.assertEqual(len(merged['callbacks']), 443)
        self.assertEqual(len(merged['mechanics']), 6)
        self.assertEqual(sum(len(c['mechanics']) for c in merged['cards']), 6)
        ookazi = merged['cards'][-1]
        self.assertEqual(ookazi, raw['cards'][-1])
        self.assertTrue(all(v['state'] == 'Unknown' for v in ookazi['coverage'].values()))
        self.assertTrue(all(c['coverage']['validation']['state'] == 'Unknown' for c in merged['cards']))
        sword = next(m for m in merged['mechanics'] if m['id'] == 'rules.equip.sword')
        self.assertEqual(sword['dependencies'], ['ai.stats.sword-delta'])
        self.assertEqual(merged['aliases'], raw['aliases'])
        self.assertEqual(merged['callbacks'], raw['callbacks'])

    def test_all_artifacts_permuted_inputs_and_files(self):
        raw, aux, pilot = fixture()
        # Split independently authored documents: order is never precedence.
        first = dict(pilot, cards=pilot['cards'][:2], mechanics=[], evidence=[])
        second = dict(pilot, cards=pilot['cards'][2:])
        expected = artifacts(raw, aux, [first, second])
        self.assertEqual(expected, artifacts(raw, aux, [permute(second), permute(first)]))
        self.assertEqual(expected, artifacts(raw, aux, [pilot, copy.deepcopy(pilot)]))
        for name, data in expected.items():
            import hashlib
            hashes = json.loads((ROOT / 'tests/fixtures/card-coverage/annotations-sha256.json').read_text())
            self.assertEqual(hashlib.sha256(data).hexdigest(), hashes[name], name)

    def reject(self, mutation):
        raw, _, pilot = fixture()
        mutation(pilot)
        with self.assertRaises(ValueError):
            merge_annotations(raw, [pilot], ROOT)

    def test_reject_versions_baseline_fingerprints_identity_and_fields(self):
        for change in [lambda p: p.update(annotationVersion=2), lambda p: p.update(annotationVersion=True),
            lambda p: p.update(baselineCommit='0'*40), lambda p: p['fingerprints'].update(datSha256='0'*64),
            lambda p: p['cards'][0]['identity'].update(resourceId=531),
            lambda p: p['cards'][0].update(cardRef='card:9999'),
            lambda p: p.update(callbacks=[]), lambda p: p['cards'][0].update(callbackLookup={}),
            lambda p: p.pop('evidence')]:
            with self.subTest(change=change): self.reject(change)

    def test_reject_duplicates_and_conflicting_overlays_in_either_order(self):
        self.reject(lambda p: p['cards'].append(copy.deepcopy(p['cards'][0])))
        raw, _, pilot = fixture()
        for key in ('cards', 'mechanics', 'evidence'):
            other = copy.deepcopy(pilot)
            if key == 'cards': other[key][0]['openQuestions'].append('Conflicting review')
            elif key == 'mechanics': other[key][0]['scope'] = 'Different scope'
            else: other[key][0]['claim'] = 'Different claim'
            for docs in ([pilot, other], [other, pilot]):
                with self.subTest(key=key), self.assertRaises(ValueError):
                    merge_annotations(raw, docs, ROOT)

    def test_reject_references_cycles_and_unverified_provenance(self):
        for change in [lambda p: p['cards'][0]['mechanics'][0].update(mechanicRef='missing'),
            lambda p: p['cards'][0]['coverage']['identification'].update(evidenceRefs=['missing']),
            lambda p: p['cards'][0]['mechanics'][0].update(callbackRefs=['callback:66']),
            lambda p: p['mechanics'][0].update(dependencies=['missing']),
            lambda p: p['mechanics'][0].update(dependencies=[p['mechanics'][0]['id']]),
            lambda p: p['evidence'][0]['location'].update(sha256='0'*64),
            lambda p: p['evidence'][0]['location'].update(commit='0'*40),
            lambda p: p['evidence'][0]['location'].update(path='/private/evidence')]:
            with self.subTest(change=change): self.reject(change)

    def test_no_experimental_source_promotion_or_unearned_validation(self):
        def experimental(p):
            p['cards'][0]['coverage']['implementation']['evidenceRefs'] = ['research-policy']
        self.reject(experimental)
        self.reject(lambda p: p['cards'][0]['coverage']['validation'].update(state='ValidatedInScope'))
        self.reject(lambda p: p['cards'][0]['mechanics'][0]['coverage']['validation'].update(state='ValidatedInScope'))
        self.reject(lambda p: p['cards'][0]['coverage'].pop('understanding'))


if __name__ == '__main__':
    unittest.main()
