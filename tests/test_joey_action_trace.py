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
