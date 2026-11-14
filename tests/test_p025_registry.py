"""New snapshot preserves history, bounds P025 support, and never claims native parity."""
import copy
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

from scripts.generate_card_coverage import ROOT, artifacts, blob, evidence, generate
from scripts.coverage.annotations import merge_annotations, verify_evidence

PATH=ROOT/'docs/coverage/p025.annotations.json'


def fixture():
    doc=json.loads(PATH.read_text())
    identities=[c['identity'] for c in doc['cards']]+[dict(internalIndex=0,resourceId=0,name='Unreviewed')]
    extracted=dict(fingerprints={k:v for k,v in doc['fingerprints'].items() if k!='exeSha256'},packSha256='a'*64,
        cards=[dict(c,properties=0,classCode=0,attack=0,defense=0,level=0,pack=0) for c in identities])
    ledger=[evidence('callbacks','docs/evidence/card-callback-catalog.json','StaticAnalysis','Pinned table'),
        evidence('extraction','src/Joey.Core/Archive.cs','SourceInspection','Pinned decoder'),
        evidence('pack','docs/24-dat-graphics.md','StaticAnalysis','Pinned pack')]
    raw,aux=generate(extracted,json.loads(blob('docs/evidence/card-callback-catalog.json')),doc['baselineCommit'],ledger)
    return raw,aux,doc


class P025RegistryTests(unittest.TestCase):
    def test_union_keeps_nine_cards_and_adds_exactly_two(self):
        raw,_,doc=fixture();merged=merge_annotations(raw,[doc],ROOT)
        old=json.loads((ROOT/'docs/coverage/consolidated.annotations.json').read_text())
        oldcards={c['cardRef']:c for c in old['cards']};newcards={c['cardRef']:c for c in doc['cards']}
        self.assertEqual(set(newcards)-set(oldcards),{'card:983','card:1018'})
        self.assertTrue(set(oldcards)<=set(newcards))
        self.assertEqual(len(newcards),11)
        self.assertEqual(len(doc['mechanics']),9)
        self.assertEqual(sum(len(c['mechanics']) for c in doc['cards']),27)
        for ref,previous in oldcards.items():
            current=newcards[ref]
            self.assertEqual(current['identity'],previous['identity'])
            for axis,v in previous['coverage'].items():self.assertEqual(current['coverage'][axis]['state'],v['state'])
            if ref!='card:614':
                self.assertEqual([m['mechanicRef'] for m in current['mechanics']],[m['mechanicRef'] for m in previous['mechanics']])
                for a,b in zip(previous['mechanics'],current['mechanics']):
                    for axis in a['coverage']:self.assertEqual(a['coverage'][axis]['state'],b['coverage'][axis]['state'])
        for c in merged['cards']:
            self.assertEqual(c['mechanicInventory'],'Open')
            self.assertEqual(c['coverage']['validation']['state'],'Unknown')
        for id in ('ai.stats.sword-delta','rules.equip.sword'):self.assertNotIn(id,[m['id'] for m in doc['mechanics']])
        p025=[c for c in doc['cards'] if c['identity']['resourceId'] in (300,301,307)]
        for c in p025:
            self.assertEqual([m['coverage']['implementation']['state'] for m in c['mechanics']],['ImplementedInScope','ImplementedInScope','Partial'])
            self.assertTrue(all(m['coverage']['validation']['state']=='SyntheticOnly' for m in c['mechanics']))
        self.assertEqual(merged['cards'][0],raw['cards'][0])

    def test_provenance_and_reviewed_baseline_sources(self):
        _,_,doc=fixture()
        for e in doc['evidence']:verify_evidence(e,ROOT)
        for e in doc['evidence']:
            loc=e['location']
            if loc['commit']==doc['baselineCommit'] and loc['path'].startswith('src/'):
                pinned=subprocess.check_output(['git','show',f"{doc['baselineCommit']}:{loc['path']}"],cwd=ROOT)
                self.assertEqual(hashlib.sha256(pinned).hexdigest(),loc['sha256'])
        original=subprocess.check_output(['git','show','6f183e3:docs/coverage/consolidated.annotations.json'],cwd=ROOT)
        self.assertEqual(original,(ROOT/'docs/coverage/consolidated.annotations.json').read_bytes())

    def test_determinism_raw_preservation_and_strict_merge(self):
        raw,aux,doc=fixture();before=copy.deepcopy((raw,aux,doc))
        outputs=artifacts(raw,aux,[doc],coverage=True,priorities=True)
        other=copy.deepcopy(doc)
        for key in ('cards','mechanics','evidence'):other[key].reverse()
        for c in other['cards']:c['mechanics'].reverse()
        self.assertEqual(outputs,artifacts(raw,aux,[other,doc],coverage=True,priorities=True))
        self.assertEqual((raw,aux,doc),before)
        for name,data in artifacts(raw,aux).items():self.assertEqual(data,outputs[name])
        old=json.loads((ROOT/'docs/coverage/consolidated.annotations.json').read_text())
        for docs in ([old,doc],[doc,old]):
            with self.assertRaises(ValueError):merge_annotations(raw,docs,ROOT)
        broken=copy.deepcopy(doc);broken['evidence'][-1]['location']['sha256']='0'*64
        with self.assertRaises(ValueError):merge_annotations(raw,[broken],ROOT)
        broken=copy.deepcopy(doc);broken['cards'][0]['coverage']['validation']['state']='ValidatedInScope'
        with self.assertRaises(ValueError):merge_annotations(raw,[broken],ROOT)

    @unittest.skipUnless(os.environ.get('YUGIRE_COVERAGE_DAT') and os.environ.get('YUGIRE_COVERAGE_EXE'),'Optional DAT/EXE not configured')
    def test_real_registry_only_three_p025_and_deterministic_outputs(self):
        _,_,doc=fixture()
        with tempfile.TemporaryDirectory() as directory:
            cmd=['python3','scripts/generate_card_coverage.py','--dat',os.environ['YUGIRE_COVERAGE_DAT'],
                '--exe',os.environ['YUGIRE_COVERAGE_EXE'],'--output',directory,'--baseline-commit',doc['baselineCommit']]
            subprocess.check_output(cmd,cwd=ROOT)
            p=Path(directory);raw={f.name:f.read_bytes() for f in p.iterdir()}
            cmd+=['--annotations',str(PATH),'--coverage-report','--investigation-priorities']
            subprocess.check_output(cmd,cwd=ROOT)
            first={f.name:f.read_bytes() for f in p.iterdir()}
            for name,data in raw.items():self.assertEqual(data,first[name])
            subprocess.check_output(cmd,cwd=ROOT)
            self.assertEqual(first,{f.name:f.read_bytes() for f in p.iterdir()})
            registry=json.loads(first['registry.json'])
            self.assertEqual(len(registry['cards']),1114)
            self.assertEqual(sum(c['coverage']['implementation']['state']=='Partial' for c in registry['cards']),9)
            self.assertTrue(all(c['coverage']['validation']['state']=='Unknown' for c in registry['cards']))
            callbacks={c['id'] for c in registry['callbacks'] if c['profileId']=='P025'}
            members=[c for c in registry['cards'] if callbacks.intersection(c['callbackLookup']['callbackRefs'])]
            annotated=[c for c in members if c['mechanics']]
            self.assertEqual(len(members),41)
            self.assertEqual({c['identity']['resourceId'] for c in annotated},{300,301,307})
            self.assertTrue(all(c['coverage']['implementation']['state']=='Unknown' for c in members if not c['mechanics']))


if __name__=='__main__':unittest.main()
