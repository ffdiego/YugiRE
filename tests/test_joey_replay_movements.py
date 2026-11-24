import csv
import json
import pathlib
import sys
import unittest

ROOT=pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/"scripts"))
from joey_replay_movements import decode_movement, MovementContext
from reconstruct_joey_replay_opening import reconstruct_opening
from export_joey_replay_scenario import project_slice


def record(raw,a,b,c,index=0):
    return {"record_index":index,"offset_hex":"0x0","raw_opcode_hex":hex(raw),
            "argument_1":a,"argument_2":b,"argument_3":c}


def side(hand=(),deck=(),monster=None,grave=()):
    return {"hand":list(hand),"deck":list(deck),"grave":list(grave),"banished":[],
            "monster":monster or [None]*5,"spell":[None]*5,"field":[None]}


class MovementTests(unittest.TestCase):
    def fixture(self):
        root=ROOT/"docs/evidence/replays"
        summary=json.loads((root/"relinquished_summary.json").read_text())
        with (root/"relinquished_commands.csv").open(newline="") as f:
            rows=list(csv.DictReader(f))
        for r in rows:
            for k in ("record_index","argument_1","argument_2","argument_3"):
                r[k]=int(r[k])
        return summary,rows

    def test_native_keys_and_two_discard_records(self):
        s,rows=self.fixture()
        opening=reconstruct_opening(s)
        self.assertEqual(opening["main_deck_keys"]["0"][:6],[72,76,52,84,32,2])
        self.assertEqual(opening["main_deck_keys"]["1"][:6],[37,23,67,65,39,21])
        m=decode_movement(rows[27])
        self.assertEqual((m.source_side,m.source_region,m.key,m.destination_side,m.destination_region,m.flags),
                         (1,11,39,1,14,1))
        # Diagnostic hypothesis: initial six minus the two recorded placements.
        # Not an independently observed full checkpoint.
        ctx=MovementContext(s,{"record_index":27,"sides":{"0":side(),"1":side(hand=[37,23,67,39])}},27)
        cmds=project_slice(rows,27,2,ctx)
        self.assertEqual(cmds[1::2],["move-id 730 1 hand 3 grave","move-id 1114 1 hand 1 grave"])
        self.assertEqual(ctx.zones[1,"grave"],[39,23])
        self.assertEqual(ctx.zones[1,"hand"],[37,67])
        with self.assertRaisesRegex(ValueError,"record 27"):
            project_slice(rows,27,2)

    def test_context_updates_draw_placement_and_removal(self):
        s,_=self.fixture()
        # Key 2 = resource 1346, key 4 = 81, both owner 0.
        ctx=MovementContext(s,{"record_index":0,"sides":{"0":side(deck=[2,4]),"1":side()}},0)
        rows=[record(0x61,1,1,0,0),record(0xce,1346,0,1,1),record(0x79,0,1,3,2),
              record(0x8e,(2<<8)|28,22,0,3)]
        self.assertEqual(project_slice(rows,0,4,ctx)[1::2],
                         ["draw 1 0","move-id 1346 0 hand 0 monster 0 attack",
                          "move-id 1346 0 monster 0 grave","move-id 1346 0 grave 0 hand"])
        self.assertEqual(ctx.zones[0,"hand"],[2])
        self.assertEqual(ctx.zones[0,"deck"],[4])

    def test_reject_ambiguous_or_cross_owner(self):
        s,rows=self.fixture()
        with self.assertRaisesRegex(ValueError,"duplicate"):
            MovementContext(s,{"record_index":27,"sides":{"0":side(hand=[39]),"1":side(hand=[39])}},27)
        ctx=MovementContext(s,{"record_index":27,"sides":{"0":side(),"1":side(hand=[23])}},27)
        with self.assertRaisesRegex(ValueError,"source zone"): ctx.project(rows[27])
        ctx=MovementContext(s,{"record_index":0,"sides":{"0":side(monster=[39,None,None,None,None]),"1":side()}},0)
        with self.assertRaisesRegex(ValueError,"different owner"): ctx.project(record(0x79,0,1,3))
        with self.assertRaisesRegex(ValueError,"flags"): ctx.project(record(0x79,0,1,11))
        ctx=MovementContext(s,{"record_index":0,"sides":{"0":side(monster=[2,None,None,None,None]),"1":side()}},0)
        self.assertEqual(ctx.project(record(0x79,0,0x10,2)),"move-id 1346 0 monster 0 banished")

if __name__=="__main__": unittest.main()
