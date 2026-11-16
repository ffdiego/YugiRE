"""Offline synthetic generator and serialization tests; real DAT is opt-in."""
import copy
import hashlib
import json
import os
from pathlib import Path
import struct
import subprocess
import tempfile
import unittest

from scripts.generate_card_coverage import generate, canonical, csv_bytes, artifacts, SOURCE_COMMIT, ROOT
from scripts.coverage.validation import validate

GOLDEN = Path(__file__).parent / 'fixtures/card-coverage'


def inputs():
    fp = {k: 'a'*64 for k in ('datSha256', 'cardIdSha256', 'namesSha256', 'propertiesSha256')}
    rows = [dict(internalIndex=i, resourceId=r, name=n, properties=0, classCode=0, attack=0,
                 defense=0, level=0, pack=p) for i, r, n, p in [(0, 0, '', 0), (2, 77, 'A, "é"\nB', 3), (5, 77, 'Duplicate', 4), (9, 2077, 'Variant', 0)]]
    extracted = dict(fingerprints=fp, cards=rows, packSha256='b'*64)
    catalog = dict(target_sha256='c'*64, card_id_sha256='a'*64, names_sha256='a'*64, properties_sha256='a'*64,
        table=dict(va='0x5ed0a8', stride=24, count=2),
        slot_order=['primary', 'candidate', 'availability', 'preparation', 'target_preparation'],
        profiles=[dict(id='P001', callbacks=['0x584e60', None, None, None, None])],
        records=[dict(row=0, resource_id=77, profile='P001'), dict(row=1, resource_id=1245, profile='P001')])
    ledger = [dict(id=id, kind='SourceInspection', claim='Synthetic fixture only', capture=None,
        limitations=['Not native evidence'], location=dict(repository='ffdiego/YugiRE', commit=SOURCE_COMMIT,
        path='tests/test_card_coverage_generator.py', locator='inputs()', sha256='d'*64)) for id in ('callbacks', 'extraction', 'pack')]
    return extracted, catalog, SOURCE_COMMIT, ledger


class GeneratorTests(unittest.TestCase):
    def test_golden_bytes_and_hashes(self):
        s, a = generate(*inputs())
        for name, data in [('inventory.json', canonical(s)), ('attributes.json', canonical(a)), ('inventory.csv', csv_bytes(s, a))]:
            self.assertEqual(data, (GOLDEN / name).read_bytes())
            self.assertEqual(hashlib.sha256(data).hexdigest(), json.loads((GOLDEN / 'sha256.json').read_text())[name])

    def test_order_does_not_change_bytes(self):
        args = inputs()
        expected = generate(*args)
        args[0]['cards'].reverse()
        args[1]['records'].reverse()
        args[3].reverse()
        actual = generate(*args)
        self.assertEqual(artifacts(*expected), artifacts(*actual))

    def test_identity_alias_orphan_denominators_and_unknown(self):
        s, a = generate(*inputs())
        self.assertEqual([c['identity']['internalIndex'] for c in s['cards']], [0, 2, 5, 9])
        self.assertEqual(s['cards'][0]['identity']['name'], '')
        self.assertEqual(a['summary']['distinctResources'], 3)
        self.assertEqual(a['summary']['catalogRows'], 4)
        self.assertEqual(a['summary']['packUnionRows'], 2)
        self.assertEqual(a['summary']['orphanCallbacks'], ['callback:1'])
        self.assertEqual(len(s['aliases']), 3)
        self.assertEqual(s['mechanics'], [])
        for c in s['cards']:
            self.assertEqual(c['mechanics'], [])
            self.assertTrue(all(v['state'] == 'Unknown' for v in c['coverage'].values()))

    def test_invalid_inputs_fail_closed(self):
        mutations = [lambda e, c: e['cards'].append(copy.deepcopy(e['cards'][0])),
            lambda e, c: e['cards'][0].update(resourceId=65535),
            lambda e, c: e['cards'][0].update(pack=-1),
            lambda e, c: e['cards'][0].update(attack=10),
            lambda e, c: e['fingerprints'].update(cardIdSha256='0'*64),
            lambda e, c: c['records'].pop(),
            lambda e, c: c['records'][1].update(resource_id=77),
            lambda e, c: c['profiles'][0].update(callbacks=[None])]
        for change in mutations:
            args = inputs()
            change(args[0], args[1])
            with self.subTest(change=change), self.assertRaises(ValueError):
                generate(*args)

    def test_references_and_denominator_rejected(self):
        for change in [lambda s: s['catalogScope'].update(expectedCardCount=3),
                       lambda s: s['cards'][0]['callbackLookup'].update(evidenceRefs=['missing'])]:
            s, _ = generate(*inputs())
            change(s)
            with self.assertRaises(ValueError):
                validate(s)

    def test_catalog_load_bridge_synthetic_dat(self):
        # Sentinel between real rows: list position must never replace Card.Index.
        resources = {'card_id': struct.pack('<4H', 0, 65535, 77, 77),
                     'card_nameeng': b'\0'*256, 'card_prop': b'\0'*16,
                     'card_desceng': b'\0', 'card_indxeng': b'\0'*16,
                     'card_pack': struct.pack('<4H', 0, 0, 3, 4)}
        offset = 12 + 268*len(resources)
        index, payload = bytearray(), bytearray()
        for key, value in resources.items():
            name = ('bin#\\' + key + '.bin').encode().ljust(256, b'\0')
            index.extend(bytes(((b >> 4) | (b << 4)) & 255 for b in name))
            index.extend(struct.pack('<3I', offset, len(value), len(value)))
            payload.extend(value)
            offset += len(value)
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'synthetic.dat'
            path.write_bytes(b'KCEJYUGI' + struct.pack('<I', len(resources)) + index + payload)
            result = json.loads(subprocess.check_output(['dotnet', 'run', '--no-build', '--project', 'src/Joey.Coverage', '--', str(path)], cwd=ROOT))
            self.assertEqual([c['internalIndex'] for c in result['cards']], [0, 2, 3])
            self.assertEqual([c['resourceId'] for c in result['cards']], [0, 77, 77])
            self.assertTrue(all(c['name'] == '' for c in result['cards']))
            self.assertNotIn('description', json.dumps(result).lower())

    @unittest.skipUnless(os.environ.get('YUGIRE_COVERAGE_DAT') and os.environ.get('YUGIRE_COVERAGE_EXE'), 'Optional local DAT/EXE not configured')
    def test_real_dat_repeatable(self):
        with tempfile.TemporaryDirectory() as directory:
            cmd = ['python3', 'scripts/generate_card_coverage.py', '--dat', os.environ['YUGIRE_COVERAGE_DAT'],
                   '--exe', os.environ['YUGIRE_COVERAGE_EXE'], '--output', directory]
            subprocess.check_output(cmd, cwd=ROOT)
            first = {p.name: p.read_bytes() for p in Path(directory).iterdir()}
            subprocess.check_output(cmd, cwd=ROOT)
            self.assertEqual(first, {p.name: p.read_bytes() for p in Path(directory).iterdir()})
            s = json.loads(first['inventory.json'])
            self.assertEqual(len(s['cards']), 1114)
            self.assertEqual(len(s['callbacks']), 443)
            self.assertEqual(json.loads(first['attributes.json'])['summary']['packUnionRows'], 771)
            annotated_cmd = cmd + ['--annotations', str(ROOT / 'docs/coverage/card-coverage.annotations.json')]
            subprocess.check_output(annotated_cmd, cwd=ROOT)
            annotated = {p.name: p.read_bytes() for p in Path(directory).iterdir()}
            for name, data in first.items():
                self.assertEqual(data, annotated[name], name)
            subprocess.check_output(annotated_cmd, cwd=ROOT)
            self.assertEqual(annotated, {p.name: p.read_bytes() for p in Path(directory).iterdir()})
            merged = json.loads(annotated['registry.json'])
            self.assertEqual(len(merged['cards']), 1114)
            self.assertEqual(len(merged['mechanics']), 6)
            self.assertEqual(sum(c['coverage']['identification']['state'] == 'Unknown' for c in merged['cards']), 1109)



if __name__ == '__main__':
    unittest.main()
