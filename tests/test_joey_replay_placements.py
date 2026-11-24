import csv
import pathlib
import sys
import unittest

ROOT=pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/"scripts"))
from joey_replay_placements import decode_placement
from export_joey_replay_scenario import project_slice


def record(raw,resource,packed,flags):
    return {"record_index":0,"offset_hex":"0x0","raw_opcode_hex":hex(raw),
            "argument_1":resource,"argument_2":packed,"argument_3":flags}


class PlacementTests(unittest.TestCase):
    def test_real_records(self):
        with (ROOT/"docs/evidence/replays/relinquished_commands.csv").open(newline="") as f:
            rows=list(csv.DictReader(f))
        for row in rows:
            for k in ("record_index","argument_1","argument_2","argument_3"):
                row[k]=int(row[k])
        self.assertEqual(decode_placement(rows[9]).setup_command(),"move-id 1068 1 hand 3 spell 0 set")
        self.assertEqual(decode_placement(rows[12]).setup_command(),"move-id 1114 1 hand 4 monster 0 set")
        self.assertEqual(decode_placement(rows[35]).setup_command(),"move-id 1351 0 hand 3 monster 0 attack")
        active=decode_placement(rows[21])
        self.assertTrue(active.face_up)
        with self.assertRaisesRegex(ValueError,"not projected"):
            project_slice(rows,21,1)
        with self.assertRaisesRegex(ValueError,"record 10"):
            project_slice(rows,9,4)  # 0D not silently skipped
        self.assertEqual(project_slice(rows,12,1)[-1],"move-id 1114 1 hand 4 monster 0 set")

    def test_flags_and_destinations(self):
        self.assertEqual(decode_placement(record(0xce,24,0x403,0x101)).setup_command(),
                         "move-id 24 0 hand 3 monster 4 defense")
        for args in [(0xce,24,0x500,1),(0xce,24,0,2),(0x90ce,24,0,1),
                     (0xce,0,0,1),(0xcf,24,0x400,0),(0xcf,24,0x500,2)]:
            with self.assertRaises(ValueError): decode_placement(record(*args))
        for args in [(0xce,24,0,0),(0xcf,24,0xa00,0),(0xcf,24,0x500,0x100)]:
            with self.assertRaises(ValueError): decode_placement(record(*args)).setup_command()

if __name__=="__main__": unittest.main()
