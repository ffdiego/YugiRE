import importlib
import json
import pathlib
import struct
import sys
import tempfile
import unittest

sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/"scripts"))
from export_joey_replay_scenario import project_slice, opening_checkpoint, main
from inspect_joey_replay import read_file


def replay_bytes(records, decks=((24,24,25,26,27),(30,31,32,33,34)), other=()):
    data=bytearray(b"YGR\x00"+b"PRIVATE!"+bytes(16))
    for side, deck in enumerate(decks):
        for group in (deck, (), other if side==1 else ()):
            data.extend(struct.pack("<H",len(group)))
            for rid in group: data.extend(struct.pack("<H",rid))
    data.extend(struct.pack("<I",len(records)))
    for rec in records: data.extend(struct.pack("<4H",*rec))
    return data


class ReplayScenarioTests(unittest.TestCase):
    def test_strict_projection_and_raw_flags(self):
        with tempfile.TemporaryDirectory() as tmp:
            path=pathlib.Path(tmp)/"test.ydr"
            path.write_bytes(replay_bytes([(0x8061,1,1,0),(0x0043,200,0,1),(0x8042,500,0,1),
                                           (0x9043,200,0,1),(0x0043,200,0,0),(0x00CE,24,0,0)]))
            records,_=read_file(path)
            self.assertEqual([x for x in project_slice(records,0,3) if not x.startswith("#")],
                             ["draw 1 1","damage 200 0","heal 500 1"])
            for start in (3,4,5):
                with self.assertRaisesRegex(ValueError,f"record {start}"):
                    project_slice(records,start,1)
            with self.assertRaises(ValueError): project_slice(records,5,2)

    def test_opening_order_duplicate_inventory_and_privacy(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=pathlib.Path(tmp); path=root/"test.ydr"; order=root/"order.json"; out=root/"scenario.txt"
            records=[(0x14,0,0,0),(0x10,0,0,0),(0x8061,0,5,0),(0x0061,0,5,0)]
            path.write_bytes(replay_bytes(records))
            order.write_text(json.dumps({"0":[27,24,24,25,26],"1":[34,33,32,31,30]}))
            args=[str(path),"--start","2","--count","2","--opening-order",str(order),"--output",str(out)]
            self.assertEqual(main(args),0)
            script=out.read_text()
            self.assertNotIn("PRIVATE",script)
            self.assertNotIn(b"PRIVATE!".hex().upper(),script)
            self.assertIn("spawn 27 0 deck\nspawn 24 0 deck\nspawn 24 0 deck",script)
            self.assertIn("draw 5 1",script)
            self.assertIn("draw 5 0",script)
            with self.assertRaises(SystemExit): main(args)
            self.assertEqual(out.read_text(),script)
            _,summary=read_file(path)
            with self.assertRaises(ValueError):
                opening_checkpoint(summary,{"0":[27,24,25,25,26],"1":[34,33,32,31,30]})
            path.write_bytes(replay_bytes(records,other=(200,)))
            _,summary=read_file(path)
            with self.assertRaises(ValueError):
                opening_checkpoint(summary,json.loads(order.read_text()))

    def test_unmapped_slice_does_not_create_output(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=pathlib.Path(tmp); path=root/"test.ydr"; setup=root/"setup.txt"; out=root/"scenario.txt"
            path.write_bytes(replay_bytes([(0x0043,200,0,1),(0x0072,336,0,0)]))
            setup.write_text("spawn 24 0 hand\n")
            with self.assertRaises(SystemExit):
                main([str(path),"--start","0","--count","2","--checkpoint",str(setup),"--output",str(out)])
            self.assertFalse(out.exists())


if __name__=="__main__": unittest.main()
