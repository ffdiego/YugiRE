"""Non-proprietary synthetic tests for the visual SQLite importer."""
import importlib.util
from pathlib import Path
import sqlite3
import tempfile
import unittest

SRC = Path(__file__).resolve().parents[1] / "enrich_joey_sqlite.py"
spec = importlib.util.spec_from_file_location("visual_importer", SRC)
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)

class EnrichTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.base = Path(self.tmp.name)
        self.source = self.base / "source.sqlite"
        self.output = self.base / "out.sqlite"
        with sqlite3.connect(self.source) as db:
            db.executescript("""CREATE TABLE metadata(key TEXT PRIMARY KEY, value TEXT);
            CREATE TABLE functions(address TEXT PRIMARY KEY,size INTEGER);
            CREATE TABLE pointers(address TEXT PRIMARY KEY,target_function TEXT);""")
            db.execute("INSERT INTO metadata VALUES ('executable_sha256',?)", (m.SHA,))
            db.execute("INSERT INTO functions VALUES ('0043A600',100)")
            db.execute("INSERT INTO functions VALUES ('0041F870',100)")
            db.execute("INSERT INTO pointers VALUES ('005DABD8','0043A600')")

    def tsv(self, lines, sha=None):
        p = self.base / "table.tsv"
        p.write_text("\n".join(["# schema=joey-visual-vtables-v1",
            "# executable_sha256="+(sha or m.SHA),"# pointer_size=4",
            "\t".join(m.FIELDS), *lines])+"\n")
        return p

    def test_baseline(self):
        result = m.run(self.source, self.output)
        self.assertEqual(result["status"],{"function_entry":1,"not_exported":27})
        with sqlite3.connect(self.output) as db:
            self.assertEqual(db.execute("SELECT COUNT(*) FROM visual_virtual_methods").fetchone()[0],28)
            self.assertEqual(db.execute("SELECT target_function FROM pointers").fetchone()[0],"0043A600")

    def test_import_and_unreadable_preserves_previous(self):
        t = self.tsv(["005DABD8\t0\t005DABD8\t\t\tunreadable\t",
                      "005DABA8\t3\t005DABB4\t0041F870\t0041F870\tfunction_entry\ttrue"])
        r = m.run(self.source,self.output,t)
        self.assertEqual(r["rawSlotsImported"],2)
        with sqlite3.connect(self.output) as db:
            self.assertEqual(db.execute("SELECT target_address FROM visual_virtual_methods WHERE vtable_address='005DABD8' AND slot_index=0").fetchone()[0],"0043A600")
            self.assertEqual(db.execute("SELECT evidence_source FROM visual_virtual_methods WHERE vtable_address='005DABA8' AND slot_index=3").fetchone()[0],"ghidra_raw_memory")

    def test_conflicting_pointer_rejected(self):
        t = self.tsv(["005DABD8\t0\t005DABD8\t0041F870\t0041F870\tfunction_entry\ttrue"])
        with self.assertRaisesRegex(ValueError,"Conflicting pointer"):
            m.run(self.source,self.output,t)
        self.assertFalse(self.output.exists())

    def test_wrong_sha_rejected(self):
        t = self.tsv(["005DABD8\t0\t005DABD8\t0043A600\t0043A600\tfunction_entry\ttrue"],"0"*64)
        with self.assertRaisesRegex(ValueError,"SHA"):
            m.run(self.source,self.output,t)
        self.assertFalse(self.output.exists())

if __name__ == "__main__": unittest.main()
