"""Reviewed union; historical overlays remain immutable and cannot auto-migrate."""
import copy
import hashlib
import json
import os
from pathlib import Path
import tempfile
import subprocess
import unittest

from scripts.coverage.annotations import merge_annotations, verify_evidence
from scripts.generate_card_coverage import ROOT, artifacts, blob, evidence, generate

PATH = ROOT / 'docs/coverage/consolidated.annotations.json'


def fixture():
    doc = json.loads(PATH.read_text())
    identities = [c['identity'] for c in doc['cards']] + [dict(internalIndex=0, resourceId=0, name='Unaudited')]
    extracted = dict(fingerprints={k:v for k,v in doc['fingerprints'].items() if k != 'exeSha256'},
        packSha256='a'*64, cards=[dict(c,properties=0,classCode=0,attack=0,defense=0,level=0,pack=0) for c in identities])
    ledger = [evidence('callbacks','docs/evidence/card-callback-catalog.json','StaticAnalysis','Pinned table'),
        evidence('extraction','src/Joey.Core/Archive.cs','SourceInspection','Pinned extraction'),
        evidence('pack','docs/24-dat-graphics.md','StaticAnalysis','Pinned availability')]
    raw, aux = generate(extracted,json.loads(blob('docs/evidence/card-callback-catalog.json')),doc['baselineCommit'],ledger)
    return raw, aux, doc


class ConsolidationTests(unittest.TestCase):
    def test_union_and_explicit_sparks_replacement(self):
        raw,_,doc = fixture()
        merged = merge_annotations(raw,[doc],ROOT)
        self.assertEqual(len(doc['cards']),9)
        self.assertEqual(len(doc['mechanics']),8)
        self.assertEqual(sum(len(c['mechanics']) for c in doc['cards']),20)
        self.assertNotIn('rules.lp.sparks',[m['id'] for m in doc['mechanics']])
        self.assertEqual(len([c for c in doc['cards'] if c['identity']['resourceId']==342]),1)
        old = json.loads((ROOT/'docs/coverage/card-coverage.annotations.json').read_text())
        pilot = json.loads((ROOT/'docs/coverage/p045.annotations.json').read_text())
        self.assertEqual({c['cardRef'] for c in doc['cards']},{c['cardRef'] for c in old['cards']+pilot['cards']})
        for historical in old['cards']+pilot['cards']:
            current=next(c for c in doc['cards'] if c['cardRef']==historical['cardRef'])
            for axis,value in historical['coverage'].items():
                self.assertEqual(current['coverage'][axis]['state'],value['state'])
            if historical['identity']['resourceId']==342 and historical in old['cards']:continue
            for previous in historical['mechanics']:
                current_mechanic=next(m for m in current['mechanics'] if m['mechanicRef']==previous['mechanicRef'])
                for axis,value in previous['coverage'].items():
                    self.assertEqual(current_mechanic['coverage'][axis]['state'],value['state'])
        self.assertEqual(merged['cards'][0],raw['cards'][0])
        self.assertTrue(all(c['coverage']['validation']['state']=='Unknown' for c in merged['cards']))
        self.assertTrue(all(c['mechanicInventory']=='Open' for c in merged['cards']))

    def test_all_output_bytes_deterministic_and_raw_immutable(self):
        raw,aux,doc=fixture();before=copy.deepcopy((raw,aux,doc))
        expected=artifacts(raw,aux,[doc],coverage=True,priorities=True)
        other=copy.deepcopy(doc)
        for key in ('cards','mechanics','evidence'):other[key].reverse()
        for entry in other['cards']:
            entry['mechanics'].reverse()
        self.assertEqual(expected,artifacts(raw,aux,[other,doc],coverage=True,priorities=True))
        for name,data in artifacts(raw,aux).items():self.assertEqual(data,expected[name])
        self.assertEqual((raw,aux,doc),before)

    def test_reviewed_baseline_and_historical_provenance(self):
        _,_,doc=fixture()
        for e in doc['evidence']:verify_evidence(e,ROOT)
        reviews=[e for e in doc['evidence'] if e['id'].startswith('review-')]
        self.assertEqual(len(reviews),6)
        for e in reviews:
            location=e['location']
            pinned=subprocess.check_output(['git','show',f"{doc['baselineCommit']}:{location['path']}"],cwd=ROOT)
            self.assertEqual(hashlib.sha256(pinned).hexdigest(),location['sha256'])
            self.assertEqual(location['commit'],doc['baselineCommit'])
        for name in ('card-coverage.annotations.json','p045.annotations.json'):
            self.assertEqual((ROOT/'docs/coverage'/name).read_bytes(),subprocess.check_output(['git','show',f"{doc['baselineCommit']}:docs/coverage/{name}"],cwd=ROOT))

    @unittest.skipUnless(os.environ.get('YUGIRE_COVERAGE_DAT') and os.environ.get('YUGIRE_COVERAGE_EXE'), 'Optional DAT/EXE not configured')
    def test_real_registry_determinism_and_no_p025_promotion(self):
        _,_,doc=fixture()
        with tempfile.TemporaryDirectory() as directory:
            cmd=['python3','scripts/generate_card_coverage.py','--dat',os.environ['YUGIRE_COVERAGE_DAT'],
                '--exe',os.environ['YUGIRE_COVERAGE_EXE'],'--output',directory,
                '--baseline-commit',doc['baselineCommit']]
            subprocess.check_output(cmd,cwd=ROOT)
            p=Path(directory)
            raw={f.name:f.read_bytes() for f in p.iterdir()}
            cmd+=['--annotations',str(PATH),'--coverage-report','--investigation-priorities']
            subprocess.check_output(cmd,cwd=ROOT)
            first={f.name:f.read_bytes() for f in p.iterdir()}
            for name,data in raw.items():self.assertEqual(data,first[name])
            subprocess.check_output(cmd,cwd=ROOT)
            self.assertEqual(first,{f.name:f.read_bytes() for f in p.iterdir()})
            registry=json.loads(first['registry.json'])
            self.assertEqual(len(registry['cards']),1114)
            self.assertEqual(sum(c['coverage']['implementation']['state']=='Partial' for c in registry['cards']),7)
            self.assertTrue(all(c['coverage']['validation']['state']=='Unknown' for c in registry['cards']))
            audited={c['cardRef'] for c in doc['cards']}
            self.assertTrue(all(not c['mechanics'] for c in registry['cards'] if c['id'] not in audited))

    def test_wrong_baseline_tampered_evidence_and_promotion_fail(self):
        raw,_,doc=fixture()
        for name in ('card-coverage.annotations.json','p045.annotations.json'):
            old=json.loads((ROOT/'docs/coverage'/name).read_text())
            with self.assertRaises(ValueError):merge_annotations(raw,[old,doc],ROOT)
        bad=copy.deepcopy(doc);bad['evidence'][-1]['location']['sha256']='0'*64
        with self.assertRaises(ValueError):merge_annotations(raw,[bad],ROOT)
        bad=copy.deepcopy(doc);bad['cards'][0]['coverage']['validation']['state']='ValidatedInScope'
        with self.assertRaises(ValueError):merge_annotations(raw,[bad],ROOT)


if __name__=='__main__':unittest.main()
