#!/usr/bin/env python3
"""Copy Joey's decompilation SQLite and add audited visual-vtable knowledge.

No EXE/DAT is bundled, the original SQLite is read-only, and unknown slots stay
unresolved until the Ghidra raw-memory TSV is provided.
"""
import argparse
import csv
import json
import re
import sqlite3
from contextlib import closing
from pathlib import Path

SHA = "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de"
TABLES = (
    ("005DABD8", "duel.selection", "0043A060", ""),
    ("005DACC4", "duel.dialog_button", "0043E2D0", ""),
    ("005DABA8", "duel.chain", "0041F5C0", "0041F870"),
    ("005DB374", "duel.chain_disabled", "00452F20", "004531C0"),
    ("005DA44C", "duel.persistent_effect", "0040DAC0", "0040E260"),
    ("005DB3C4", "duel.layerfont_a", "00455D90", ""),
    ("005DB3D4", "duel.layerfont_b", "00456100", ""),
)
DOC = "docs/research/visual-unmapped-families-2026-11-21.md"
RESOLUTIONS = {"function_entry", "inside_function", "executable_unknown", "nonexecutable", "unmapped", "null", "unreadable"}
FIELDS = ["vtable_address", "slot_index", "slot_address", "target_address", "function_address", "resolution", "executable"]


def va(v):
    v = v.strip().upper()
    if not re.fullmatch(r"[0-9A-F]{8}", v):
        raise ValueError(f"Invalid VA {v!r}")
    return v


def known_functions(db):
    return sorted((int(a, 16), int(a, 16) + n, a) for a, n in db.execute("SELECT address,size FROM functions"))


def owner_of(address, functions):
    if not address:
        return None
    value = int(address, 16)
    return next((a for start, end, a in functions if start <= value < end), None)


def init(db):
    db.executescript("""
      CREATE TABLE visual_subsystems (name TEXT PRIMARY KEY, source TEXT NOT NULL);
      CREATE TABLE visual_vtables (address TEXT PRIMARY KEY, subsystem TEXT NOT NULL,
        constructor TEXT NOT NULL, source TEXT NOT NULL,
        FOREIGN KEY(subsystem) REFERENCES visual_subsystems(name));
      CREATE TABLE visual_virtual_methods (vtable_address TEXT NOT NULL,
        slot_index INTEGER NOT NULL, slot_address TEXT NOT NULL, role TEXT NOT NULL,
        target_address TEXT, function_address TEXT, resolution TEXT NOT NULL,
        evidence_source TEXT NOT NULL,
        PRIMARY KEY(vtable_address,slot_index));
      CREATE TABLE visual_analysis_notes (entity_address TEXT NOT NULL, entity_type TEXT NOT NULL,
        proposed_name TEXT, note TEXT NOT NULL, confidence TEXT NOT NULL,
        evidence_type TEXT NOT NULL, source TEXT NOT NULL,
        PRIMARY KEY(entity_address,entity_type,note));
      CREATE VIEW visual_unresolved_slots AS
        SELECT v.subsystem,m.* FROM visual_virtual_methods m
        JOIN visual_vtables v ON v.address=m.vtable_address
        WHERE m.resolution NOT IN ('function_entry','inside_function');
      CREATE INDEX visual_method_target ON visual_virtual_methods(target_address);
    """)
    functions = known_functions(db)
    for address, subsystem, ctor, draw in TABLES:
        db.execute("INSERT INTO visual_subsystems VALUES (?,?)", (subsystem, DOC))
        db.execute("INSERT INTO visual_vtables VALUES (?,?,?,?)", (address, subsystem, ctor, DOC))
        db.execute("INSERT INTO visual_analysis_notes VALUES (?,?,?,?,?,?,?)",
                   (ctor, "function", None, f"Constructor associated with vtable {address}",
                    "high", "static_decompilation", DOC))
        if draw:
            db.execute("INSERT INTO visual_analysis_notes VALUES (?,?,?,?,?,?,?)",
                       (draw, "function", None,
                        f"Candidate Draw of {subsystem}; +0xC pointer not yet demonstrated",
                        "medium", "repository_inventory", DOC))
        for i in range(4):
            slot = f"{int(address,16)+4*i:08X}"
            row = db.execute("SELECT target_function FROM pointers WHERE address=?", (slot,)).fetchone()
            target = va(row[0]) if row else None
            owner = owner_of(target, functions)
            classification = "function_entry" if target and target == owner else (
                "inside_function" if owner else "indexed_unknown" if target else "not_exported")
            db.execute("INSERT INTO visual_virtual_methods VALUES (?,?,?,?,?,?,?,?)",
                       (address, i, slot, "Update" if i==2 else "Draw" if i==3 else f"slot{i}",
                        target, owner, classification,
                        "legacy_pointers_index" if target else "not_recorded"))


def load_tsv(path):
    lines, meta = [], {}
    for line in Path(path).read_text(encoding="utf-8").splitlines():
        if line.startswith("# ") and "=" in line:
            k,v = line[2:].split("=", 1)
            meta[k.strip()] = v.strip()
        elif line.strip():
            lines.append(line)
    if (meta.get("schema") != "joey-visual-vtables-v1" or
        meta.get("executable_sha256", "").lower() != SHA or
        meta.get("pointer_size") != "4"):
        raise ValueError("Ghidra export schema/SHA/pointer size mismatch")
    records = csv.DictReader(lines, delimiter="\t")
    if records.fieldnames != FIELDS:
        raise ValueError("Unexpected Ghidra TSV fields")
    values = list(records)
    if not values:
        raise ValueError("Empty Ghidra TSV")
    return values


def merge(db, rows):
    table_set = {v[0] for v in TABLES}
    functions = known_functions(db)
    seen = set()
    for record in rows:
        table = va(record["vtable_address"])
        i = int(record["slot_index"])
        key = table, i
        if table not in table_set or not 0 <= i < 4 or key in seen:
            raise ValueError(f"Unexpected or duplicated slot {key}")
        seen.add(key)
        slot = va(record["slot_address"])
        if int(slot,16) != int(table,16) + i*4:
            raise ValueError(f"Incorrect slot address {key}")
        target = va(record["target_address"]) if record["target_address"] else None
        owner = va(record["function_address"]) if record["function_address"] else None
        resolution = record["resolution"]
        executable = record["executable"]
        if resolution not in RESOLUTIONS:
            raise ValueError(f"Unknown resolution {resolution}")
        if resolution == "unreadable":
            if target or owner or executable:
                raise ValueError("Unreadable slot cannot have pointer evidence")
        elif not target:
            raise ValueError("Ghidra record lacks pointer bytes")
        if resolution == "null" and target != "00000000":
            raise ValueError("Null pointer differs from zero")
        if resolution in ("function_entry","inside_function") and not owner:
            raise ValueError("Function resolution requires owner")
        if resolution == "function_entry" and owner != target:
            raise ValueError("Entry address and owner disagree")
        if owner and owner_of(target,functions) not in (None,owner):
            raise ValueError("Ghidra owner contradicts function range")
        existing = db.execute("SELECT target_address FROM visual_virtual_methods WHERE vtable_address=? AND slot_index=?",key).fetchone()
        if existing is None:
            raise ValueError(f"Unknown slot {key}")
        if existing[0] and target and existing[0] != target:
            raise ValueError(f"Conflicting pointer for {key}: SQLite={existing[0]} Ghidra={target}")
        if resolution == "unreadable" and existing[0]:
            continue
        db.execute("""UPDATE visual_virtual_methods SET target_address=?,function_address=?,
          resolution=?,evidence_source='ghidra_raw_memory'
          WHERE vtable_address=? AND slot_index=?""",
          (target,owner,resolution,table,i))
    return len(seen)


def run(source, output, ghidra_tsv=None, summary=None):
    source, output = Path(source).resolve(), Path(output).resolve()
    if not source.is_file() or source == output or output.exists():
        raise ValueError("Source must exist; output must be a NEW path")
    rows = load_tsv(ghidra_tsv) if ghidra_tsv else []
    output.parent.mkdir(parents=True,exist_ok=True)
    try:
        with closing(sqlite3.connect(source.as_uri()+"?mode=ro",uri=True)) as original:
            fingerprint = original.execute("SELECT value FROM metadata WHERE key='executable_sha256'").fetchone()
            if not fingerprint or fingerprint[0].lower() != SHA:
                raise ValueError("Original SQLite executable SHA mismatch")
            with closing(sqlite3.connect(output)) as enriched:
                original.backup(enriched)
                with enriched:
                    init(enriched)
                    imported = merge(enriched,rows) if rows else 0
                status = dict(enriched.execute("SELECT resolution,COUNT(*) FROM visual_virtual_methods GROUP BY resolution"))
                if enriched.execute("PRAGMA integrity_check").fetchone()[0] != "ok":
                    raise ValueError("Output SQLite integrity check failed")
        audit = {"executableSha256":SHA,"vtables":7,"slots":28,"rawSlotsImported":imported,"status":status}
        if summary:
            Path(summary).parent.mkdir(parents=True,exist_ok=True)
            Path(summary).write_text(json.dumps(audit,indent=2)+"\n",encoding="utf-8")
        return audit
    except Exception:
        output.unlink(missing_ok=True)
        raise


def main(argv=None):
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument("--source",type=Path,required=True)
    p.add_argument("--output",type=Path,required=True)
    p.add_argument("--ghidra-tsv",type=Path)
    p.add_argument("--summary",type=Path)
    a=p.parse_args(argv)
    try:
        print(json.dumps(run(a.source,a.output,a.ghidra_tsv,a.summary)))
        return 0
    except (OSError,ValueError,sqlite3.Error) as e:
        p.error(str(e))

if __name__=="__main__":
    raise SystemExit(main())
