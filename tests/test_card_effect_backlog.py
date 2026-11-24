from collections import Counter
from copy import deepcopy
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'scripts'))
from card_effect_backlog import FINGERPRINT_FIELDS, summarize


def taxonomy_fixture():
    rows = []
    for index, (mask, kind, route, families, status) in enumerate([
        (4, 'Spell', 'trivial-candidate', ['draw'], 'Pending'),
        (1, 'Spell', 'trivial-candidate', ['heal'], 'Done'),
        (7, 'Trap', 'queue-or-response', ['discard', 'draw'], 'Partial'),
        (4, 'Spell', 'review', [], 'Pending'),
        (4, 'Normal', 'not-applicable', [], 'Pending'),
    ]):
        rows.append(dict(internalIndex=index, resourceId=10 + index, packMask=mask, kind=kind,
                         route=route, families=families, callbackProfile=None, currentStatus=status))
    return dict(schemaVersion=1, fingerprints=dict.fromkeys(FINGERPRINT_FIELDS, '1' * 64),
                packSha256='2' * 64, routes=dict(Counter(row['route'] for row in rows)), cards=rows)


class BacklogTests(unittest.TestCase):
    def test_uses_live_statuses_and_counts_each_entry_once_per_route(self):
        totals, families, updated = summarize(taxonomy_fixture(), {10: 'Done', 12: 'Partial'}, 7)

        self.assertEqual(updated, 2)
        self.assertEqual(totals['trivial-candidate'], {'Pending': 1})
        self.assertEqual(families['trivial-candidate'], {'heal': {'Pending': 1}})
        self.assertEqual(totals['queue-or-response'], {'Partial': 1})
        self.assertEqual(families['queue-or-response'], {'draw': {'Partial': 1}, 'discard': {'Partial': 1}})
        self.assertEqual(totals['review'], {'Pending': 1})
        self.assertEqual(families['review'], {'unclassified': {'Pending': 1}})

    def test_pack_filter_and_verified_exclusion(self):
        totals, _, updated = summarize(taxonomy_fixture(), {10: 'Verified', 12: 'Partial'}, 4)
        self.assertEqual(updated, 1)
        self.assertEqual(totals['trivial-candidate'], {})
        self.assertEqual(totals['queue-or-response'], {'Partial': 1})

    def test_rejects_raw_rows_and_invalid_statuses_without_echoing_values(self):
        for field, value in (('description', 'private text'), ('currentStatus', 'private text'),
                             ('route', 'private text'), ('families', ['private text']),
                             ('families', ['draw', 'heal']), ('packMask', True)):
            with self.subTest(field=field, value=value):
                taxonomy = taxonomy_fixture()
                taxonomy['cards'][0][field] = value
                with self.assertRaises(ValueError) as error:
                    summarize(taxonomy, {}, 7)
                self.assertNotIn('private text', str(error.exception))
        with self.assertRaisesRegex(ValueError, 'Invalid playability status'):
            summarize(taxonomy_fixture(), {10: 'Complete'}, 7)

    def test_rejects_inconsistent_metadata(self):
        taxonomy = taxonomy_fixture()
        for key, value in (('schemaVersion', 2), ('packSha256', 'invalid'),
                           ('routes', {'trivial-candidate': 2})):
            with self.subTest(key=key):
                broken = deepcopy(taxonomy)
                broken[key] = value
                with self.assertRaises(ValueError):
                    summarize(broken, {}, 7)
        taxonomy['cards'][1]['internalIndex'] = taxonomy['cards'][0]['internalIndex']
        with self.assertRaisesRegex(ValueError, 'duplicate C01 taxonomy record'):
            summarize(taxonomy, {}, 7)


if __name__ == '__main__':
    unittest.main()
