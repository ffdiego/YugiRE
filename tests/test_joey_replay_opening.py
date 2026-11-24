import csv
import json
import pathlib
import sys
import unittest
import copy

ROOT=pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/"scripts"))
from reconstruct_joey_replay_opening import reconstruct_opening, initial_prefix_checks

class ReplayOpeningTests(unittest.TestCase):
    def fixture(self):
        root=ROOT/"docs/evidence/replays"
        summary=json.loads((root/"relinquished_summary.json").read_text())
        with (root/"relinquished_commands.csv").open(newline="") as f:
            records=list(csv.DictReader(f))
        for row in records:
            for k in ("record_index","argument_1","argument_2","argument_3"):
                row[k]=int(row[k])
        return summary,records

    def test_real_redacted_fixture(self):
        summary,records=self.fixture()
        result=reconstruct_opening(summary)
        self.assertEqual(result["seed"],1793458172)
        self.assertEqual(result["shuffle_first_side"],1)
        self.assertEqual(result["main_deck_order"]["0"][:6],[1142,1062,1071,1425,1351,1346])
        self.assertEqual(result["main_deck_order"]["1"][:6],[730,1114,347,1068,730,1114])
        checks=initial_prefix_checks(records,result)
        self.assertEqual([(r["record_index"],r["recorded_resource"],r["match"]) for r in checks],
                         [(9,1068,True),(12,1114,True)])
        self.assertEqual(len(result["group2_inventory"]["1"]),6)
        for side in (0,1):
            self.assertCountEqual(result["main_deck_order"][str(side)],
                                  summary["deck_groups"][side]["groups"][0]["resource_ids"])

    def test_guard_and_wrong_order(self):
        summary,records=self.fixture()
        result=reconstruct_opening(summary)
        result["main_deck_order"]["1"][3]=1
        self.assertFalse(initial_prefix_checks(records,result)[0]["match"])
        summary["header"]["header_fields_uninterpreted"]["0x0C"]=1000
        with self.assertRaises(ValueError): reconstruct_opening(summary)

    def test_rng_boundary(self):
        summary,_=self.fixture()
        # State after first rand = 0x7fff0000; invert the odd multiplier.
        seed=((0x7fff0000-0x269EC3)*pow(0x343FD,-1,2**32))%(2**32)
        summary["header"]["header_fields_uninterpreted"]["0x18"]=seed
        with self.assertRaisesRegex(ValueError,"boundary"): reconstruct_opening(summary)

if __name__=="__main__": unittest.main()
