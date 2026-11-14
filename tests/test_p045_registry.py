"""P045 annotations target the reviewed implementation commit, not Phase 1."""
import copy
import json
import os
import subprocess
from pathlib import Path
import tempfile
import unittest

from scripts.generate_card_coverage import ROOT, blob, evidence, generate, artifacts
from scripts.coverage.annotations import merge_annotations
from scripts.inspect_p045 import inspect

PILOT = ROOT / 'docs/coverage/p045.annotations.json'


def fixture():
    doc = json.loads(PILOT.read_text())
    identities = [c['identity'] for c in doc['cards']] + [dict(internalIndex=0, resourceId=0, name='Unannotated')]
    extracted = dict(fingerprints={k:v for k,v in doc['fingerprints'].items() if k != 'exeSha256'},
        packSha256='a'*64, cards=[dict(c, properties=0, classCode=0, attack=0, defense=0, level=0, pack=0) for c in identities])
    ledger = [evidence('callbacks','docs/evidence/card-callback-catalog.json','StaticAnalysis','Pinned table'),
        evidence('extraction','src/Joey.Core/Archive.cs','SourceInspection','Pinned extraction'),
        evidence('pack','docs/24-dat-graphics.md','StaticAnalysis','Pinned availability')]
    raw, aux = generate(extracted,json.loads(blob('docs/evidence/card-callback-catalog.json')),doc['baselineCommit'],ledger)
    return raw,aux,doc


class P045RegistryTests(unittest.TestCase):
    def test_bounded_annotations_and_no_integral_validation(self):
        raw,aux,doc = fixture()
        r = merge_annotations(raw,[doc],ROOT)
        self.assertEqual([c['identity']['resourceId'] for c in doc['cards']], [345,344,342,343,1007])
        self.assertEqual(len(r['mechanics']),3)
        self.assertEqual(sum(len(c['mechanics']) for c in r['cards']),15)
        self.assertEqual(r['cards'][0],raw['cards'][0])
        self.assertTrue(all(c['coverage']['validation']['state']=='Unknown' for c in r['cards']))
        self.assertTrue(all(c['coverage']['implementation']['state']=='Partial' for c in r['cards'][1:]))
        self.assertTrue(all(m['coverage']['validation']['state']=='SyntheticOnly' for m in r['mechanics']))
        self.assertEqual(next(m for m in r['mechanics'] if m['id']=='rules.lp.p045-demo')['dependencies'],['rules.lp.p045-command','rules.lp.p045-target'])
        before = artifacts(raw,aux)
        outputs = artifacts(raw,aux,[doc],coverage=True,priorities=True)
        for n,b in before.items():self.assertEqual(b,outputs[n])
        other=copy.deepcopy(doc)
        other['cards'].reverse();other['evidence'].reverse();other['mechanics'].reverse()
        self.assertEqual(outputs,artifacts(raw,aux,[other],coverage=True,priorities=True))

    def test_phase1_baseline_not_silently_promoted(self):
        raw,_,doc = fixture()
        raw['baselineCommit']='7e5ca132f7185bd2c3df4c9782e080fb95a135f5'
        with self.assertRaises(ValueError):merge_annotations(raw,[doc],ROOT)

    def test_exe_fingerprint_fails_closed(self):
        with tempfile.TemporaryDirectory() as directory:
            p=Path(directory)/'invalid.exe';p.write_bytes(b'not the target')
            with self.assertRaises(ValueError):inspect(p)

    @unittest.skipUnless(os.environ.get('YUGIRE_COVERAGE_EXE') and os.environ.get('YUGIRE_COVERAGE_DAT'),'Optional DAT/EXE not configured')
    def test_real_phase2_baseline_determinism_and_raw_preservation(self):
        doc=json.loads(PILOT.read_text())
        with tempfile.TemporaryDirectory() as directory:
            cmd=['python3','scripts/generate_card_coverage.py','--dat',os.environ['YUGIRE_COVERAGE_DAT'],
                 '--exe',os.environ['YUGIRE_COVERAGE_EXE'],'--output',directory,'--baseline-commit',doc['baselineCommit']]
            subprocess.check_output(cmd,cwd=ROOT)
            p=Path(directory);raw={f.name:f.read_bytes() for f in p.iterdir()}
            full=cmd+['--annotations',str(PILOT),'--coverage-report','--investigation-priorities']
            subprocess.check_output(full,cwd=ROOT)
            generated={f.name:f.read_bytes() for f in p.iterdir()}
            for name,data in raw.items():self.assertEqual(data,generated[name])
            subprocess.check_output(full,cwd=ROOT)
            self.assertEqual(generated,{f.name:f.read_bytes() for f in p.iterdir()})
            registry=json.loads(generated['registry.json'])
            self.assertEqual(len(registry['cards']),1114)
            self.assertEqual(registry['baselineCommit'],doc['baselineCommit'])
            self.assertEqual(sum(c['coverage']['implementation']['state']=='Partial' for c in registry['cards']),5)
            self.assertTrue(all(c['coverage']['validation']['state']=='Unknown' for c in registry['cards']))

    @unittest.skipUnless(os.environ.get('YUGIRE_COVERAGE_EXE'),'Optional local EXE not configured')
    def test_static_metadata_reproduces_without_native_execution(self):
        actual=inspect(os.environ['YUGIRE_COVERAGE_EXE'])
        self.assertEqual(actual,json.loads((ROOT/'docs/evidence/p045-static-checks.json').read_text()))


if __name__=='__main__':unittest.main()
