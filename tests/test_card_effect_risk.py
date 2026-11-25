from collections import Counter
from copy import deepcopy
import json
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'scripts'))
from card_effect_risk import SLOTS, adjust_risk, build, callback_slots
from card_playability import CATALOG, ROOT, MANIFEST, read_manifest, read_subtypes


def fixture():
    catalog = dict(slot_order=list(SLOTS), target_sha256='a' * 64,
                   card_id_sha256='1' * 64, names_sha256='1' * 64,
                   properties_sha256='1' * 64, table={'count': 3},
                   profiles=[
                       dict(id='P001', callbacks=[None] * 5, rows=[0]),
                       dict(id='P002', callbacks=['0x401000', None, None, None, None], rows=[1]),
                       dict(id='P003', callbacks=['0x401000'] * 3 + [None] * 2, rows=[2])],
                   records=[dict(row=i, resource_id=10+i, profile=f'P00{i+1}') for i in range(3)])
    ref = dict(schemaVersion=1, datSha256='1' * 64,
               fields=['resourceId', 'packMask', 'kindCode'], kinds={'1': 'Monster', '2': 'Spell'},
               cards=[[10, 4, 2], [11, 4, 2], [12, 1, 2], [13, 4, 1], [14, 4, 1]])
    return catalog, ref


class RiskTests(unittest.TestCase):
    def test_missing_and_zero_are_distinct_live_status_is_preserved(self):
        cat, ref = fixture()
        result = build(cat, ref, {11: 'Done', 12: 'Partial'}, {13: 'Effect', 14: 'Normal'})
        by_id = {r['resourceId']: r for r in result['cards']}
        self.assertEqual(result['catalogSlotCounts'], {'0': 1, '1': 1, '3': 1})
        self.assertEqual(by_id[10]['slotCount'], 0)
        self.assertIsNone(by_id[13]['slotCount'])
        self.assertEqual(by_id[13]['adjustedRisk'], 'indeterminate')
        self.assertEqual(by_id[14]['adjustedRisk'], 'not-applicable')
        self.assertEqual(by_id[11]['status'], 'Done')
        self.assertEqual(by_id[12]['status'], 'Partial')
        self.assertEqual(result['reviewResourceIds'], [10, 12, 13])
        self.assertEqual(len(build(cat, ref, {}, {}, mask=4)['cards']), 4)

    def test_semantics_raise_risk_and_do_not_blindly_downgrade(self):
        self.assertEqual(adjust_risk('low', 'Trap', 'queue-or-response', ['negation'])[0], 'high')
        self.assertEqual(adjust_risk('low', 'Spell', 'queue-or-response', ['search'])[0], 'medium')
        self.assertEqual(adjust_risk('high', 'Spell', 'trivial-candidate', ['draw'])[0], 'high')
        self.assertEqual(adjust_risk('indeterminate', 'Spell', 'trivial-candidate', ['draw']),
                         ('low', ['missing-or-empty-callbacks', 'strict-c01-simple-effect'], True))
        self.assertTrue(adjust_risk('low', 'Spell', 'review', [])[2])

    def test_taxonomy_join_preserves_exact_id_and_checks_fingerprints(self):
        cat, ref = fixture()
        rows = [dict(internalIndex=99-i, resourceId=rid, packMask=mask,
                     kind=kind, families=families, route=route, callbackProfile=None,
                     currentStatus='Pending')
                for i, ((rid, mask, _), (kind, families, route)) in enumerate(zip(ref['cards'], [
                    ('Spell', ['draw'], 'trivial-candidate'),
                    ('Spell', ['control'], 'queue-or-response'),
                    ('Spell', ['damage'], 'trivial-candidate'),
                    ('Effect', [], 'review'),
                    ('Normal', [], 'not-applicable')]))]
        taxonomy = dict(schemaVersion=1, fingerprints=dict.fromkeys(
            ['datSha256', 'cardIdSha256', 'namesSha256', 'propertiesSha256'], '1' * 64),
            packSha256='2' * 64, routes=dict(Counter(r['route'] for r in rows)), cards=rows)
        result = build(cat, ref, {11: 'Partial'}, {}, taxonomy)
        by_id = {r['resourceId']: r for r in result['cards']}
        self.assertEqual(by_id[11]['internalIndex'], 98)
        self.assertEqual(by_id[11]['adjustedRisk'], 'high')
        self.assertEqual(by_id[11]['status'], 'Partial')
        self.assertNotIn('description', by_id[11])
        broken = deepcopy(taxonomy)
        broken['fingerprints']['namesSha256'] = '3' * 64
        with self.assertRaisesRegex(ValueError, 'fingerprints differ'):
            build(cat, ref, {}, {}, broken)

    def test_bad_profiles_and_duplicate_ids_rejected(self):
        cat, _ = fixture()
        for change in ('row', 'resource_id', 'profile'):
            with self.subTest(change=change):
                broken = deepcopy(cat)
                broken['records'][1][change] = broken['records'][0][change]
                with self.assertRaises(ValueError):
                    callback_slots(broken)
        broken = deepcopy(cat)
        broken['profiles'][1]['callbacks'][0] = '0x0'
        with self.assertRaises(ValueError):
            callback_slots(broken)

    def test_real_versioned_inventory_reproduces_slot_distribution(self):
        cat = json.loads(CATALOG.read_text(encoding='utf-8'))
        ref = json.loads((ROOT / 'docs/cards/pack-index.json').read_text(encoding='utf-8'))
        manifest = read_manifest(MANIFEST)
        report = build(cat, ref, {rid: row['status'] for rid, row in manifest.items()}, read_subtypes())
        self.assertEqual(report['catalogSlotCounts'], {'0': 15, '1': 94, '2': 134, '3': 146, '4': 47, '5': 7})
        self.assertEqual(len(report['cards']), len(ref['cards']))
        self.assertEqual(sum(sum(c.values()) for c in report['packSlotStatusCounts'].values()), len(ref['cards']))
        self.assertEqual(report['semanticSource'], 'none')


if __name__ == '__main__':
    unittest.main()
