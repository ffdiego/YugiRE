"""Test DAT selection metadata import without requiring Ghidra or data.dat."""
import json
import sqlite3
import tempfile
import unittest
from pathlib import Path

from scripts.import_native_selection_dat import (
    SHA, REQUIRED, collect, enrich, canonical_path, manifest_rows, unpack_scroll,
)


def fixture():
    manifests = [{"name": name, "latin1": "list_select0.bmp\t000095 // comment\r\n"}
                 for name in sorted(REQUIRED)]
    manifests.append({"name": r"j\duel\list\select_scroll.txt",
                      "latin1": "null.bmp\t213839248\nsele_box.bmp\t000001\n"})
    manifests[0]["latin1"] += "select_scroll.txt\t150\n"
    return {"schemaVersion": 1, "manifests": manifests, "images": [
        {"name": r"j\duel\list\list_select0.bmp", "width": 451,
         "height": 191, "decodedBytes": 259052, "error": None},
        {"name": r"j\duel\list\null.bmp", "width": 1,
         "height": 1, "decodedBytes": 58, "error": None},
        {"name": r"j\duel\list\sele_box.bmp", "width": 41,
         "height": 12, "decodedBytes": 1544, "error": None},
    ]}


class SelectionDatImportTests(unittest.TestCase):
    def test_native_packed_field_splitting(self):
        self.assertEqual(unpack_scroll(213839248), (21, 38, 39, 248))
        self.assertEqual(unpack_scroll(212617260), (21, 26, 17, 260))
        self.assertEqual(unpack_scroll(242447336), (24, 24, 47, 336))
        self.assertEqual(unpack_scroll(224435224), (22, 44, 35, 224))

    def test_manifest_parser_preserves_zero_padded_values(self):
        rows = manifest_rows(
            "// comment\r\nsele_yaji_l2.bmp\t001000\t// comment\nlist_myself.bmp\n"
        )
        self.assertEqual(rows, [
            (2, "sele_yaji_l2.bmp", "001000", 1000),
            (3, "list_myself.bmp", None, None),
        ])

    def test_all_references_resolve_and_scroll_descriptor_is_typed(self):
        manifests, images, entries, scrolls = collect(fixture())
        self.assertEqual(len(manifests), len(REQUIRED) + 1)
        self.assertEqual(len(images), 3)
        self.assertEqual(len(entries), len(REQUIRED) + 3)
        self.assertIn(
            ("j\\duel\\list\\select_scroll.txt", 213839248, 21, 38, 39, 248, "0043BA30"),
            scrolls,
        )

    def test_unresolved_resource_and_path_traversal_rejected(self):
        data = fixture()
        data["images"].pop()
        with self.assertRaisesRegex(ValueError, "Missing resource references"):
            collect(data)
        with self.assertRaises(ValueError):
            canonical_path(r"j\duel\list\..\other.bmp")
        with self.assertRaises(ValueError):
            manifest_rows("not_a_resource 123")

    def test_backup_keeps_original_and_old_tables(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder)
            original, output, audit = path/"old.sqlite", path/"new.sqlite", path/"audit.json"
            with sqlite3.connect(original) as db:
                db.execute("CREATE TABLE metadata(key TEXT PRIMARY KEY,value TEXT)")
                db.execute("INSERT INTO metadata VALUES ('executable_sha256',?)", (SHA,))
                db.execute("CREATE TABLE visual_transient_decompilations(address TEXT, code TEXT)")
                db.execute("INSERT INTO visual_transient_decompilations VALUES ('0043B280','example')")
            old_bytes = original.read_bytes()
            result = enrich(original, output, fixture(), audit)
            self.assertEqual(result["scrollDescriptors"], 1)
            self.assertEqual(old_bytes, original.read_bytes())
            self.assertEqual(json.loads(audit.read_text())["unresolvedResourceReferences"], 0)
            with sqlite3.connect(output) as db:
                self.assertEqual(db.execute("PRAGMA integrity_check").fetchone()[0], "ok")
                self.assertEqual(db.execute("SELECT code FROM visual_transient_decompilations").fetchone()[0], "example")
                self.assertEqual(db.execute("SELECT COUNT(*) FROM visual_dat_entries").fetchone()[0], len(REQUIRED) + 3)
                self.assertEqual(db.execute("SELECT width,height FROM visual_dat_entry_details WHERE resource_path LIKE '%list_select0.bmp' LIMIT 1").fetchone(), (451, 191))
            with self.assertRaisesRegex(ValueError, "NEW file"):
                enrich(original, output, fixture())

    def test_wrong_exe_sha_rejected_and_no_output_left(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder)
            original, output = path/"old.sqlite", path/"new.sqlite"
            with sqlite3.connect(original) as db:
                db.execute("CREATE TABLE metadata(key TEXT PRIMARY KEY,value TEXT)")
                db.execute("INSERT INTO metadata VALUES ('executable_sha256','different')")
            with self.assertRaisesRegex(ValueError, "SHA mismatch"):
                enrich(original, output, fixture())
            self.assertFalse(output.exists())


if __name__ == "__main__":
    unittest.main()
