"""Report goldens, denominator integrity, conservative ranking and output lifecycle."""
import copy
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

from test_card_coverage_generator import inputs
from test_card_coverage_annotations import permute
from scripts.generate_card_coverage import generate, artifacts, unknown, ROOT
from scripts.coverage.reports import coverage_report, priorities_report, candidates
from scripts.coverage.output import publish, GENERATED, REPORTS

GOLDEN = ROOT / 'tests/fixtures/card-coverage/reports'


def fixture(annotated=False):
    registry, attrs = generate(*inputs())
    if annotated:
        cov = unknown()
        cov['identification'].update(state='Identified', evidenceRefs=['extraction'])
        registry['cards'][1]['coverage'] = copy.deepcopy(cov)
        registry['cards'][1]['mechanics'] = [dict(mechanicRef='test.question', relation='Candidate', callbackRefs=['callback:0'], evidenceRefs=['extraction'], coverage=copy.deepcopy(cov))]
        registry['mechanics'] = [dict(id='test.question', title='Synthetic question | <tag>', domain='Rules', scope='Synthetic only', coverage=cov, dependencies=[], openQuestions=['Which preconditions?'])]
    return registry, copy.deepcopy(attrs)


class ReportTests(unittest.TestCase):
    def test_golden_with_and_without_annotations(self):
        for annotated in (False, True):
            r, a = fixture(annotated)
            for name, data in [('coverage-report.md', coverage_report(r, a)), ('investigation-priorities.md', priorities_report(r, a))]:
                prefix = 'annotated-' if annotated else 'raw-'
                self.assertEqual(data, (GOLDEN / (prefix + name)).read_bytes())
                self.assertTrue(data.endswith(b'\n'))

    def test_counts_from_registry_and_separate_pack_universes(self):
        r, a = fixture(True)
        md = coverage_report(r, a).decode()
        self.assertIn('| Catálogo selecionado completo | 4 |', md)
        self.assertIn('| União dos três jogos (pack &amp; 7) | 2 |', md)
        self.assertIn('| identification | Identified | 1 |', md)
        self.assertIn('| identification | Unknown | 3 |', md)
        self.assertIn('| validation | Unknown | 4 |', md)
        self.assertIn('| Rules | 1 |', md)
        self.assertIn('Which preconditions?', md)
        self.assertIn('SourceInspection', md)
        self.assertIn('&#124;', md)
        self.assertIn('&lt;tag&gt;', md)
        # Deliberately stale RAW coverage summary must not feed annotated histograms.
        self.assertEqual(a['summary']['coverage']['identification'], {'Unknown': 4})

    def test_permutations_and_no_input_mutation(self):
        r, a = fixture(True)
        before = copy.deepcopy((r, a))
        for fn in (coverage_report, priorities_report):
            self.assertEqual(fn(r, a), fn(permute(r), permute(a)))
        self.assertEqual((r, a), before)
        rows = candidates(r, a)
        self.assertEqual(rows[0]['cards'], ['card:2', 'card:5'])
        self.assertEqual(rows[0]['distinctResources'], 1)
        self.assertEqual(rows[0]['callbackRows'], 2)
        self.assertEqual(rows[0]['mechanics'], ['test.question'])
        self.assertTrue(all(c['gain'] == 'Unknown' for c in rows))
        # Same pointers do not propagate the annotation to card:5.
        self.assertEqual(r['cards'][2]['mechanics'], [])
        self.assertEqual(r['cards'][2]['coverage']['identification']['state'], 'Unknown')

    def test_multiple_candidates_have_stable_reach_and_id_order(self):
        r, a = fixture()
        for index, row, resource, profile in [(3, 2, 2077, 'P002'), (0, 3, 0, 'P003')]:
            cb = copy.deepcopy(r['callbacks'][0])
            cb.update(id=f'callback:{row}', row=row, resourceId=resource, profileId=profile, rowVa=f'0x{0x5ed0a8 + 24*row:08x}')
            r['callbacks'].append(cb)
            r['cards'][index]['callbackLookup'].update(state='Present', callbackRefs=[cb['id']])
        a['summary']['callbackRows'] = 4
        self.assertEqual([c['id'] for c in candidates(r, a)], ['profile:P001', 'profile:P002', 'profile:P003'])
        self.assertEqual(priorities_report(r, a), priorities_report(permute(r), permute(a)))

    def test_all_null_structural_potential_unknown(self):
        r, a = fixture()
        for cb in r['callbacks']:
            cb['slots'] = {k: None for k in cb['slots']}
        self.assertFalse(candidates(r, a)[0]['structural'])
        self.assertIn('| Unknown |', priorities_report(r, a).decode())

    def test_inconsistent_records_rejected(self):
        changes = [lambda r, a: a.update(registryVersion=2),
            lambda r, a: a['fingerprints'].update(datSha256='0'*64),
            lambda r, a: a['cards'].pop(),
            lambda r, a: a['cards'].append(copy.deepcopy(a['cards'][0])),
            lambda r, a: a['cards'][0].update(pack=1),
            lambda r, a: a['cards'][0].update(attack=10),
            lambda r, a: a['summary'].update(packUnionRows=4),
            lambda r, a: a.update(evidenceRefs=['missing']),
            lambda r, a: r['callbacks'][0]['slots'].update(primary=None),
            lambda r, a: r['cards'][1]['callbackLookup'].update(state='AbsentInTable', callbackRefs=[]),
            lambda r, a: r['cards'][0]['coverage']['validation'].update(state='ValidatedInScope')]
        for change in changes:
            for fn in (coverage_report, priorities_report):
                r, a = fixture()
                change(r, a)
                with self.subTest(change=change, fn=fn), self.assertRaises(ValueError): fn(r, a)

    def test_raw_artifacts_preserved_and_stale_owned_outputs_retired(self):
        r, a = fixture()
        raw = artifacts(r, a)
        full = artifacts(r, a, coverage=True, priorities=True)
        for name, data in raw.items():
            self.assertEqual(full[name], data)
        with tempfile.TemporaryDirectory() as directory:
            p = Path(directory)
            (p / 'notes.txt').write_text('keep')
            publish(p, full)
            publish(p, {'registry.json': b'old', 'registry.csv': b'old'}, {'registry.json', 'registry.csv'})
            publish(p, raw)
            self.assertEqual({f.name for f in p.iterdir()}, set(raw) | {'notes.txt'})
            self.assertEqual((p / 'notes.txt').read_text(), 'keep')
            publish(p, full)
            publish(p, {'coverage-report.md': full['coverage-report.md']}, REPORTS)
            self.assertFalse((p / 'investigation-priorities.md').exists())
            self.assertTrue((p / 'inventory.json').exists())

    @unittest.skipUnless(os.environ.get('YUGIRE_COVERAGE_DAT') and os.environ.get('YUGIRE_COVERAGE_EXE'), 'Optional real DAT/EXE not configured')
    def test_real_cli_reports_and_existing_json_cli(self):
        with tempfile.TemporaryDirectory() as directory:
            cmd = ['python3', 'scripts/generate_card_coverage.py', '--dat', os.environ['YUGIRE_COVERAGE_DAT'], '--exe', os.environ['YUGIRE_COVERAGE_EXE'], '--output', directory]
            subprocess.check_output(cmd, cwd=ROOT)
            p = Path(directory)
            raw = {f.name: f.read_bytes() for f in p.iterdir()}
            annotated = cmd + ['--annotations', 'docs/coverage/card-coverage.annotations.json', '--coverage-report', '--investigation-priorities']
            subprocess.check_output(annotated, cwd=ROOT)
            all_files = {f.name: f.read_bytes() for f in p.iterdir()}
            for name, data in raw.items(): self.assertEqual(data, all_files[name])
            subprocess.check_output(annotated, cwd=ROOT)
            self.assertEqual(all_files, {f.name: f.read_bytes() for f in p.iterdir()})
            subprocess.check_call(['python3', 'scripts/report_card_coverage.py', '--registry', str(p/'registry.json'), '--attributes', str(p/'attributes.json'), '--output', directory, '--coverage-report', '--investigation-priorities'], cwd=ROOT)
            self.assertEqual(all_files, {f.name: f.read_bytes() for f in p.iterdir()})
            md = all_files['coverage-report.md'].decode()
            self.assertIn('| Catálogo selecionado completo | 1114 |', md)
            self.assertIn('| União dos três jogos (pack &amp; 7) | 771 |', md)
            self.assertIn('| identification | Identified | 5 |', md)
            self.assertIn('| validation | Unknown | 1114 |', md)
            subprocess.check_output(cmd, cwd=ROOT)
            self.assertEqual(raw, {f.name: f.read_bytes() for f in p.iterdir()})


if __name__ == '__main__':
    unittest.main()
