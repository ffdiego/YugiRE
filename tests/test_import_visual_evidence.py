import json
from pathlib import Path
import sqlite3
import sys
import tempfile
import unittest
import zipfile

sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"scripts"))
from import_visual_evidence import EXPECTED_SHA256, run, report_data


class VisualEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name)
        self.source=self.root/"old.sqlite"
        with sqlite3.connect(self.source) as db:
            db.execute("CREATE TABLE metadata(key TEXT,value TEXT)")
            db.execute("INSERT INTO metadata VALUES('executable_sha256',?)",(EXPECTED_SHA256,))
            db.execute("CREATE TABLE visual_transient_decompilations(address TEXT PRIMARY KEY,name TEXT,signature TEXT,code TEXT,bundle_sha256 TEXT,source_entry TEXT,provenance TEXT)")
            db.execute("INSERT INTO visual_transient_decompilations VALUES('00400000','old','','old','','','old')")
        self.before=self.source.read_bytes()

    def bundle(self, name, retry=False, invalid_sha=False, shifted=False):
        path=self.root/name
        header="SHA256: "+("0"*64 if invalid_sha else EXPECTED_SHA256)+"\n"
        va="00400000" if retry else "00400010"
        report=("retry-000-"+va) if retry else "batch-000"
        funcs=f"FUNCTION FUN_{va} @{va}\n CALLER 00500000 x\nvoid f()\n{{\n return;\n}}\n"
        selected=[va]
        if not retry:
            selected.append("00400020")
            funcs+="FUNCTION FUN_00400020 @00400020\nvoid g()\n{\n return;\n}\n"
        manifest=dict(schemaVersion=1,executableSha256=EXPECTED_SHA256,
            selected=selected,decompiled=selected,missing=[],
            memoryRanges=["00500000:2"],memoryComplete=True,
            instructionWindows=["00400010:2"],instructionWindowsComplete=True,
            attempts=[dict(name=n,ok=True) for n in [report,"memory-read","instruction-windows"]])
        with zipfile.ZipFile(path,"w") as z:
            z.writestr("manifest.json",json.dumps(manifest))
            z.writestr(report+".txt",header+funcs)
            z.writestr("memory-read.txt",header+"BYTES 00500000 count=2 12 AB\n")
            start="00400011" if shifted else "00400010"
            z.writestr("instruction-windows.txt",header+"INS_WINDOW_BEGIN 00400010 count=2\n"
                +"INS "+start+" NOP\nINS 00400012 RET\nINS_WINDOW_END 00400010 actual=2\n")
        return path

    def test_cumulative_batch_retry_and_provenance(self):
        a=self.bundle("a.zip")
        b=self.bundle("b.zip",retry=True)
        first=self.root/"first.sqlite"
        self.assertEqual(run(self.source,[a],first)["visual_evidence_functions"],2)
        second=self.root/"second.sqlite"
        result=run(first,[a,b],second)
        self.assertEqual(result["newBundles"],1)
        self.assertEqual(result["visual_evidence_bundles"],2)
        self.assertEqual(result["visual_evidence_functions"],3)
        self.assertEqual(result["visual_evidence_memory"],2)
        self.assertEqual(result["visual_evidence_instructions"],4)
        with sqlite3.connect(second) as db:
            self.assertEqual(db.execute("SELECT code FROM visual_transient_decompilations WHERE address='00400000'").fetchone(),("old",))
            self.assertEqual(db.execute("SELECT provenance FROM visual_evidence_functions WHERE address='00400000'").fetchone(),("ghidra_transient",))
            self.assertEqual(db.execute("PRAGMA integrity_check").fetchone(),("ok",))
        self.assertEqual(self.source.read_bytes(),self.before)
        with self.assertRaises(ValueError):
            run(first,[a],second)

    def test_fingerprint_and_exact_window_rejected(self):
        for i,b in enumerate([self.bundle("bad.zip",invalid_sha=True),self.bundle("shifted.zip",shifted=True)]):
            out=self.root/f"rejected{i}.sqlite"
            with self.assertRaises(ValueError):run(self.source,[b],out)
            self.assertFalse(out.exists())
        self.assertEqual(self.source.read_bytes(),self.before)

    def test_failed_decomp_not_indexed_and_shifted_batch_not_normalized(self):
        header="SHA256: "+EXPECTED_SHA256+"\n"
        from import_visual_evidence import parse_functions
        seen,funcs=parse_functions(header+"FUNCTION FUN_00400000 @00400000\nDecompiler failed\n",{"00400000"})
        self.assertEqual(seen,{"00400000"})
        self.assertEqual(funcs,[])
        mem,ins=report_data(header+"INS_WINDOW_BEGIN 00400010 count=1\nINS 00400011 NOP\nINS_WINDOW_END 00400010 actual=1\n")
        self.assertEqual(ins,{})
        self.assertEqual(mem,{})

if __name__ == "__main__":
    unittest.main()
