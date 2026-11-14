"""Completeness of the investigation table, independent of behavior hypotheses."""
import collections
import json
import re
import unittest
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]


class P025InventoryTests(unittest.TestCase):
    def test_all_41_registrations_keep_distinct_identity_fields(self):
        catalog=json.loads((ROOT/'docs/evidence/card-callback-catalog.json').read_text())
        expected=[(r['row'],i['index'],r['resource_id'],i['name'])
            for r in catalog['records'] if r['profile']=='P025' for i in r['identities']]
        document=(ROOT/'docs/cards/p025-preliminary.md').read_text()
        matches=re.findall(r'^\| ([A-E]) \| (\d+) \| (\d+) \| (\d+) \| ([^|]+) \|',document,re.M)
        actual=[(int(row),int(index),int(resource),name.strip()) for _,row,index,resource,name in matches]
        self.assertEqual(len(actual),41)
        self.assertEqual(len(set(actual)),41)
        self.assertEqual(sorted(actual),sorted(expected))
        self.assertEqual(collections.Counter(m[0] for m in matches),dict(A=15,B=6,C=10,D=6,E=4))


if __name__=='__main__':unittest.main()
