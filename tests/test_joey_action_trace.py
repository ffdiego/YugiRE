import json
import pathlib
import sys
import tempfile
import unittest

ROOT=pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/"scripts"))
from compare_joey_action_trace import compare, native_rows, canonical


def row(seq,command,args,mapping="mapped",outcome="Applied"):
    return {"sequence":seq,"command":command,"origin":"Effect","outcome":outcome,
            "side":args[0]>>15 if args is not None else 0,"mapping":mapping,"args":args,"detail":""}


def document(rows,complete=True):
    return {"schema":"yugire-action-projection-v1","scope":"synthetic","complete":complete,"rows":rows}


class TraceTests(unittest.TestCase):
    def test_batch_normalization_and_mismatch(self):
        left=[{"index":2,"args":[0x8061,0,5,0]},{"index":3,"args":[0x43,200,0,1]}]
        right=[row(i,"DrawCommand",[0x8061,1,1,0]) for i in range(1,6)]
        right.append(row(6,"DamageCommand",[0x43,200,0,1]))
        report=compare(left,document(right),0,2,0,6,"ydr-record")
        self.assertEqual(report["status"],"projected_match")
        self.assertEqual([r["nativeIndex"] for r in report["nativeProjected"]],[2]*5+[3])
        right[-1]["args"][1]=300
        report=compare(left,document(right),0,2,0,6,"ydr-record")
        self.assertEqual(report["status"],"mismatch")
        self.assertEqual(report["differences"][0]["csharp"]["csharpSequence"],6)

    def test_legal_turn_draw_uses_the_ordinary_draw_mapping(self):
        native = [{"index": 0, "args": [0x8061, 1, 1, 0]}]
        legal_draw = row(1, "TurnDrawCommand", [0x8061, 1, 1, 0])
        report = compare(native, document([legal_draw]), 0, 1, 0, 1, "ydr-record")
        self.assertEqual(report["status"], "projected_match")

        invalid = row(1, "TurnDrawCommand", [0x43, 200, 0, 1])
        with self.assertRaisesRegex(ValueError, "command does not match"):
            compare(native, document([invalid]), 0, 1, 0, 1, "ydr-record")

    def test_archived_turn_advance_keeps_ending_side_and_cleanup_gap(self):
        kind, records = native_rows(ROOT / "docs/evidence/replays/relinquished_commands.csv")
        for side, start in ((1, 14), (0, 66)):
            expected = [(side << 15) | 0x03, 0, 0, 0]
            advance = row(1, "AdvanceTurnCommand", expected)
            with self.subTest(side=side):
                report = compare(records, document([advance]), start, 2, 0, 1, kind)
                self.assertEqual(report["status"], "inconclusive")
                self.assertEqual(report["differences"], [])
                self.assertEqual([entry["nativeIndex"] for entry in report["nativeProjected"]], [start + 1])
                self.assertEqual(len(report["nativeUnmapped"]), 1)
                with self.assertRaisesRegex(ValueError, "command does not match"):
                    compare(records, document([row(1, "EndTurnCommand", expected)]), start, 2, 0, 1, kind)
        self.assertIsNone(canonical([0x8003, 0, 1, 0]))

    def test_unknowns_and_incomplete_are_not_match(self):
        left=[{"index":0,"args":[0x43,200,0,1]},{"index":1,"args":[0x72,342,0,0]}]
        right=[row(1,"DamageCommand",[0x43,200,0,1]),row(2,"SummonCommand",None,"unmapped")]
        r=compare(left,document(right),0,2,0,2,"ydr-record")
        self.assertEqual(r["status"],"inconclusive")
        self.assertEqual(len(r["nativeUnmapped"]),1);self.assertEqual(len(r["csharpUnmapped"]),1)
        self.assertEqual(compare(left,document(right,False),0,1,0,1,"ydr-record")["status"],"inconclusive")
        self.assertIsNone(canonical([0x10043,200,0,1]))
        self.assertIsNone(canonical([0x9043,200,0,1]))

    def test_existing_yugilab_capture(self):
        kind,rows=native_rows(ROOT/"src/tests/Joey.Tests/Fixtures/lab-capture-prefix.jsonl")
        self.assertEqual(kind,"producer-command-ordinal")
        n=next(i for i,r in enumerate(rows) if r["sequence"]==7)
        report=compare(rows,document([row(1,"DrawCommand",[0x61,1,1,0])]),n,1,0,1,kind)
        self.assertEqual(report["status"],"projected_match")
        self.assertEqual(report["nativeProjected"][0]["nativeSequence"],7)

    def test_placement_metadata_and_difference(self):
        left=[{"index":12,"args":[0x80ce,1114,4,0x100]}]
        right=row(1,"SetCommand",[0x80ce,1114,4,0x100])
        right["placement"]={"source":{"side":1,"zone":1,"occurrence":{"value":9}},
            "resourceId":1114,"handIndex":4,"destination":2,"slot":0,
            "faceDown":True,"defense":True,"paidTributes":0}
        self.assertEqual(compare(left,document([right]),0,1,0,1,"ydr-record")["status"],"projected_match")
        left[0]["args"][2]=0x104
        self.assertEqual(compare(left,document([right]),0,1,0,1,"ydr-record")["status"],"mismatch")
        right["placement"]["handIndex"]=3
        with self.assertRaisesRegex(ValueError,"metadata"): compare(left,document([right]),0,1,0,1,"ydr-record")
        self.assertIsNone(canonical([0xcf,1071,0x502,1]))
        self.assertIsNone(canonical([0xce,1114,4,0x101]))

    def test_historical_direct_attack_with_gaps(self):
        import csv
        with (ROOT/"docs/evidence/replays/relinquished_commands.csv").open(newline="") as f:
            rows=list(csv.DictReader(f))
        left=[{"index":int(x["record_index"]),"args":[int(x["raw_opcode_hex"],16),
               *[int(x[f"argument_{i}"]) for i in (1,2,3)]]} for x in rows[956:962]]
        attack=row(1,"DeclareDirectAttackCommand",[0x8034,0,1,0])
        attack["directAttack"]={"attacker":{"side":1,"zone":2,"occurrence":{"value":1}},
             "resourceId":24,"slot":0,"requestedDamage":1400}
        right=[attack,row(2,"DamageCommand",[0x43,1400,0,1])]
        report=compare(left,document(right),0,6,0,2,"ydr-record")
        self.assertEqual(report["status"],"inconclusive")
        self.assertEqual(report["differences"],[])
        self.assertEqual(len(report["nativeUnmapped"]),4)
        self.assertEqual([x["nativeIndex"] for x in report["nativeProjected"]],[956,961])
        right[1]["args"][1]=1300
        self.assertEqual(compare(left,document(right),0,6,0,2,"ydr-record")["status"],"mismatch")

    def test_battle_removal_fixture(self):
        import csv
        with (ROOT/"docs/evidence/replays/relinquished_commands.csv").open(newline="") as f:
            rows=list(csv.DictReader(f))
        left=[{"index":int(x["record_index"]),"args":[int(x["raw_opcode_hex"],16),
               *[int(x[f"argument_{i}"]) for i in (1,2,3)]]} for x in rows[49:51]]
        snapshot=row(1,"SnapshotBattleRemovalCommand",[0x803c,0,1,0])
        finish=row(2,"FinishBattleRemovalCommand",[0x807c,0,0,1])
        for x in (snapshot,finish):
            x["battleRemoval"]={"target":{"side":1,"zone":2,"occurrence":{"value":7}},
                "resourceId":1114,"slot":0,"role":1}
        self.assertEqual(compare(left,document([snapshot,finish]),0,2,0,2,"ydr-record")["status"],"projected_match")
        self.assertIsNone(canonical([0x807c,0,1,1])) # banish variant
        self.assertIsNone(canonical([0x33,0,1,1])) # wrong defender side

    def test_replay_command_csv_preserves_the_complete_window(self):
        kind, rows = native_rows(ROOT / "docs/evidence/replays/relinquished_commands.csv")
        self.assertEqual(kind, "archived-ydr-csv")
        self.assertEqual([record["index"] for record in rows[281:302]], list(range(281, 302)))
        self.assertEqual([record["index"] for record in rows[281:302] if canonical(record["args"])],
                         [281, 290, 296, 301])

    def test_replay_command_csv_rejects_missing_or_out_of_range_records(self):
        columns = "record_index,raw_opcode_hex,argument_1,argument_2,argument_3\n"
        with tempfile.TemporaryDirectory() as tmp:
            fixture = pathlib.Path(tmp) / "commands.csv"
            for data, error in (("0,0x61,1,1,0\n2,0x61,1,1,0\n", "sequence gap"),
                                ("0,0x61,1,1,65536\n", "unsigned argument words"),
                                ("0,0x61,1,1,0,extra\n", "malformed replay command CSV row")):
                fixture.write_text(columns + data, encoding="utf-8")
                with self.subTest(data=data), self.assertRaisesRegex(ValueError, error):
                    native_rows(fixture)

    def test_live_capture_requires_complete_lossless_footer_with_exact_event_count(self):
        header = {"kind": "metadata", "mode": "live_duels", "formatVersion": 2}
        events = [
            {"kind": "command", "seq": 1, "args": [97, 1, 1, 0]},
            {"kind": "marker", "seq": 2, "label": "fixture-end"},
        ]
        footer = {"kind": "capture_summary", "complete": True, "overwritten": 0, "eventsWritten": 2}
        with tempfile.TemporaryDirectory() as tmp:
            path = pathlib.Path(tmp) / "live.jsonl"

            def read(rows):
                path.write_text("\n".join(json.dumps(row) for row in rows), encoding="utf-8")
                return native_rows(path)

            self.assertEqual(read([header, *events, footer]), (
                "producer-command-ordinal", [{"index": 0, "sequence": 1, "args": [97, 1, 1, 0]}]))
            with self.assertRaisesRegex(ValueError, "missing.*footer"):
                read([header, *events])
            with self.assertRaisesRegex(ValueError, "event count"):
                read([header, events[0], footer])
            for bad_footer in (dict(footer, complete=False), dict(footer, overwritten=1)):
                with self.subTest(bad_footer=bad_footer), self.assertRaisesRegex(ValueError, "loss/incomplete"):
                    read([header, *events, bad_footer])
            with self.assertRaisesRegex(ValueError, "after footer"):
                read([header, *events, footer, {"kind": "marker", "seq": 3}])

    def test_footerless_capture_requires_lossless_snapshot_counts(self):
        header = {"kind": "metadata", "seen": 2, "retained": 2, "overwritten": 0}
        event = {"kind": "command", "seq": 1, "args": [97, 1, 1, 0]}
        with tempfile.TemporaryDirectory() as tmp:
            path = pathlib.Path(tmp) / "snapshot.jsonl"

            def read(rows):
                path.write_text("\n".join(json.dumps(row) for row in rows), encoding="utf-8")
                return native_rows(path)

            with self.assertRaisesRegex(ValueError, "event counts"):
                read([header, event])
            with self.assertRaisesRegex(ValueError, "sequence gap"):
                read([header, dict(event, seq=2)])
            with self.assertRaisesRegex(ValueError, "overwritten or unknown"):
                read([dict(header, overwritten=1), event])
            with self.assertRaisesRegex(ValueError, "duplicate capture metadata"):
                read([header, event, header])

    def test_csharp_gap_outside_comparison_window_is_rejected(self):
        native = [{"index": 0, "args": [0x61, 1, 1, 0]}]
        draw = row(1, "DrawCommand", [0x61, 1, 1, 0])
        corrupt = row(3, "DrawCommand", [0x61, 1, 1, 0])
        for rows in ([draw, corrupt], [corrupt]):
            with self.subTest(rows=rows), self.assertRaisesRegex(ValueError, "C# sequence gap"):
                compare(native, document(rows), 0, 1, 0, 1, "ydr-record")

    def test_csharp_row_count_detects_a_missing_suffix(self):
        native = [{"index": 0, "args": [0x61, 1, 1, 0]}]
        doc = document([row(1, "DrawCommand", [0x61, 1, 1, 0])])
        for bad_count in (2, True, None):
            with self.subTest(count=bad_count), self.assertRaisesRegex(ValueError, "C# trace row count"):
                compare(native, dict(doc, rowCount=bad_count), 0, 1, 0, 1, "ydr-record")
        report = compare(native, dict(doc, rowCount=1), 0, 1, 0, 1, "ydr-record")
        self.assertEqual(report["status"], "projected_match")

    def test_bad_capture_and_fabricated_mapping(self):
        with tempfile.TemporaryDirectory() as tmp:
            p=pathlib.Path(tmp)/"capture.jsonl"
            p.write_text('\n'.join(json.dumps(x) for x in [
                {"kind":"metadata","overwritten":0},{"kind":"command","seq":1,"args":[97,1,1,0]},
                {"kind":"command","seq":3,"args":[97,1,1,0]}]))
            with self.assertRaisesRegex(ValueError,"gap"): native_rows(p)
        with self.assertRaises(ValueError):
            compare([{"index":0,"args":[0x43,200,0,1]}],
                document([row(1,"SummonCommand",[0x43,200,0,1])]),0,1,0,1,"ydr-record")

if __name__=="__main__": unittest.main()
