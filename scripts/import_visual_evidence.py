#!/usr/bin/env python3
"""Append private Ghidra visual bundles to a NEW copy of the knowledge SQLite.

Unlike the historical import_visual_virtual_bundle.py, this accepts cumulative
input, multi-function batch reports, transient retry reports, instruction
windows and memory chunks. Never modify the source or overwrite evidence.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import sqlite3
import zipfile
from contextlib import closing

from export_ghidra_visual_bundle import EXPECTED_SHA256, memory_queries, instruction_windows

VA = re.compile(r"[0-9A-Fa-f]{8}\Z")
REPORT = re.compile(r"(?:batch-\d{3}|retry-\d{3}-[0-9a-fA-F]{8}|memory-read|instruction-windows)\Z")
FUNCTION = re.compile(r"(?m)^FUNCTION (\S+) @([0-9a-fA-F]{8})\s*$")
CALLER = re.compile(r"^ CALLER ([0-9a-fA-F]{8})(?:\s|$)")
BEGIN = re.compile(r"INS_WINDOW_BEGIN ([0-9a-fA-F]{8}) count=(\d+)\Z")
END = re.compile(r"INS_WINDOW_END ([0-9a-fA-F]{8}) actual=(\d+)\Z")
INS = re.compile(r"INS ([0-9a-fA-F]{8}) (.+)\Z")
BYTES = re.compile(r"BYTES ([0-9a-fA-F]{8}) count=(\d+) (.+)\Z")
MAX_ZIP = 32 * 1024 * 1024
MAX_REPORT = 4 * 1024 * 1024
MAX_TEXT = 32 * 1024 * 1024

SCHEMA = """
CREATE TABLE IF NOT EXISTS visual_evidence_bundles(
  bundle_sha256 TEXT PRIMARY KEY, executable_sha256 TEXT NOT NULL,
  manifest_json TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS visual_evidence_reports(
  bundle_sha256 TEXT NOT NULL REFERENCES visual_evidence_bundles,
  source_entry TEXT NOT NULL, report TEXT NOT NULL,
  PRIMARY KEY(bundle_sha256,source_entry)
);
CREATE TABLE IF NOT EXISTS visual_evidence_functions(
  bundle_sha256 TEXT NOT NULL, source_entry TEXT NOT NULL,
  address TEXT NOT NULL, name TEXT NOT NULL, code TEXT NOT NULL,
  callers_json TEXT NOT NULL, provenance TEXT NOT NULL,
  PRIMARY KEY(bundle_sha256,source_entry,address),
  FOREIGN KEY(bundle_sha256,source_entry)
    REFERENCES visual_evidence_reports(bundle_sha256,source_entry)
);
CREATE INDEX IF NOT EXISTS visual_evidence_functions_address
  ON visual_evidence_functions(address);
CREATE TABLE IF NOT EXISTS visual_evidence_memory(
  bundle_sha256 TEXT NOT NULL, source_entry TEXT NOT NULL,
  address TEXT NOT NULL, bytes BLOB NOT NULL,
  PRIMARY KEY(bundle_sha256,source_entry,address),
  FOREIGN KEY(bundle_sha256,source_entry)
    REFERENCES visual_evidence_reports(bundle_sha256,source_entry)
);
CREATE TABLE IF NOT EXISTS visual_evidence_instructions(
  bundle_sha256 TEXT NOT NULL, source_entry TEXT NOT NULL,
  window_address TEXT NOT NULL, ordinal INTEGER NOT NULL,
  address TEXT NOT NULL, instruction TEXT NOT NULL,
  PRIMARY KEY(bundle_sha256,source_entry,window_address,ordinal),
  FOREIGN KEY(bundle_sha256,source_entry)
    REFERENCES visual_evidence_reports(bundle_sha256,source_entry)
);
CREATE INDEX IF NOT EXISTS visual_evidence_instructions_address
  ON visual_evidence_instructions(address);
"""


def address(value):
    if not isinstance(value, str) or not VA.fullmatch(value):
        raise ValueError(f"Invalid VA: {value!r}")
    return value.upper()


def va_set(values):
    if not isinstance(values, list):
        raise ValueError("Expected a VA list")
    result = {address(x) for x in values}
    if len(result) != len(values):
        raise ValueError("Duplicate VA in manifest")
    return result


def report_data(text, strict_windows=None, strict_memory=None):
    """Parse exact windows and memory, rejecting partial/shifted reads.

    Ordinary batch disassembly may start at a following instruction when
    the target is missing. Preserve that report, but do not index its
    shifted window as evidence of the requested entry.
    """
    lines = text.splitlines()
    if not lines or lines[0].lower() != "sha256: " + EXPECTED_SHA256:
        raise ValueError("Wrong or missing report fingerprint")
    memory, windows = {}, {}
    active, requested, rows = None, 0, []
    for line in lines:
        if line.startswith("BYTES "):
            m = BYTES.fullmatch(line)
            if not m:
                raise ValueError("Malformed memory record")
            va = address(m[1])
            tokens = m[3].split()
            if (va in memory or len(tokens) != int(m[2]) or not 1 <= len(tokens) <= 512
                    or any(not re.fullmatch(r"[0-9A-Fa-f]{2}", t) for t in tokens)):
                raise ValueError("Duplicate or malformed memory chunk")
            memory[va] = bytes.fromhex(" ".join(tokens))
        elif line.startswith("INS_WINDOW_BEGIN "):
            m = BEGIN.fullmatch(line)
            if not m or active is not None:
                raise ValueError("Malformed or nested instruction window")
            active, requested, rows = address(m[1]), int(m[2]), []
            if active in windows or not 1 <= requested <= 1024:
                raise ValueError("Duplicate or oversized instruction window")
        elif line.startswith("INS "):
            m = INS.fullmatch(line)
            if not m or active is None:
                raise ValueError("Instruction outside a window")
            va = address(m[1])
            if rows and int(va,16) <= int(rows[-1][0],16):
                raise ValueError("Non-increasing instruction addresses")
            rows.append((va,m[2]))
        elif line.startswith("INS_WINDOW_END "):
            m = END.fullmatch(line)
            if not m or active is None or address(m[1]) != active or int(m[2]) != len(rows) or len(rows) > requested:
                raise ValueError("Malformed instruction window end")
            windows[active] = (requested, rows)
            active = None
    if active is not None:
        raise ValueError("Unterminated instruction window")
    if strict_memory is not None and {k:len(v) for k,v in memory.items()} != strict_memory:
        raise ValueError("Missing, extra or shortened memory read")
    if strict_windows is not None:
        if {k:v[0] for k,v in windows.items()} != strict_windows:
            raise ValueError("Missing or unexpected instruction window")
        if any(not rows or rows[0][0] != va for va,(_,rows) in windows.items()):
            raise ValueError("Shifted or empty exact instruction window")
    exact = {va:rows for va,(_,rows) in windows.items() if rows and rows[0][0] == va}
    return memory,exact


def parse_functions(text, selected):
    headers = list(FUNCTION.finditer(text))
    seen, functions = set(), []
    for i,m in enumerate(headers):
        va = address(m[2])
        if va not in selected:
            continue  # report can contain an enclosing, unrequested function
        if va in seen:
            raise ValueError("Duplicate function header in report")
        seen.add(va)
        end = headers[i+1].start() if i+1 < len(headers) else len(text)
        body = text[m.end():end].strip("\r\n").splitlines()
        callers = []
        while body and body[0].startswith(" CALLER "):
            match = CALLER.match(body.pop(0))
            if match:
                callers.append(address(match[1]))
        code = "\n".join(body).strip()
        # Ghidra emits a FUNCTION header even if decompileFunction fails.
        # The raw report remains available, but never index an error as C.
        if "{" in code and re.search(r"(?m)^}\s*$",code):
            functions.append((va,m[1],code+"\n",callers))
    return seen,functions


def read_bundle(path: Path):
    if path.stat().st_size > MAX_ZIP:
        raise ValueError("Bundle exceeds private import limit")
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    with zipfile.ZipFile(path) as z:
        names = z.namelist()
        if len(names) != len(set(names)) or "manifest.json" not in names:
            raise ValueError("Duplicate ZIP entries or missing manifest")
        if z.getinfo("manifest.json").file_size > 1_000_000:
            raise ValueError("Oversized manifest")
        manifest = json.loads(z.read("manifest.json"))
        if (manifest.get("schemaVersion") != 1 or
                str(manifest.get("executableSha256","")).lower() != EXPECTED_SHA256):
            raise ValueError("Unsupported schema or wrong executable SHA")
        selected = va_set(manifest.get("selected",[]))
        decompiled = va_set(manifest.get("decompiled",[]))
        missing = va_set(manifest.get("missing",[]))
        if decompiled & missing or decompiled | missing != selected:
            raise ValueError("Inconsistent selected/decompiled/missing lists")
        expected_memory = {address(c.split(":")[1]):int(c.split(":")[2])
                           for c in memory_queries(manifest.get("memoryRanges",[]))}
        expected_windows = {address(c.split(":")[1]):int(c.split(":")[2])
                            for c in instruction_windows(manifest.get("instructionWindows",[]))}
        reports, seen_names, headers = [],set(),set()
        total = 0
        for attempt in manifest.get("attempts",[]):
            name = attempt.get("name","")
            if not REPORT.fullmatch(name) or name in seen_names:
                raise ValueError("Unexpected or duplicate report name")
            seen_names.add(name)
            if attempt.get("ok") is not True:
                continue  # failed reports not evidence; manifest retains failure
            entry = name+".txt"
            if entry not in names:
                raise ValueError("Successful report missing from ZIP")
            size = z.getinfo(entry).file_size
            total += size
            if size > MAX_REPORT or total > MAX_TEXT:
                raise ValueError("Oversized visual reports")
            text = z.read(entry).decode("utf-8", errors="strict")
            mem,ins = report_data(text,
                expected_windows if name == "instruction-windows" else None,
                expected_memory if name == "memory-read" else None)
            if name != "memory-read" and mem:
                raise ValueError("Unexpected memory outside memory report")
            found,funcs = parse_functions(text,selected)
            headers.update(found)
            provenance = "ghidra_transient" if name.startswith("retry-") else "ghidra_existing"
            reports.append((entry,text,mem,ins,funcs,provenance))
        if headers != decompiled:
            raise ValueError("Manifest function headers not supported by successful reports")
        entries = {r[0] for r in reports}
        if expected_memory and ("memory-read.txt" not in entries or manifest.get("memoryComplete") is not True):
            raise ValueError("Requested memory not complete")
        if expected_windows and ("instruction-windows.txt" not in entries or manifest.get("instructionWindowsComplete") is not True):
            raise ValueError("Requested instruction windows not complete")
        if not reports:
            raise ValueError("No successful reports")
    return digest,manifest,reports


def run(source: Path, bundles: list[Path], output: Path):
    source,output = source.resolve(),output.resolve()
    if not source.is_file() or source == output or output.exists() or not bundles:
        raise ValueError("Source must exist and output must be a NEW file")
    # Validate all input before allocating output, and never extract ZIP paths.
    parsed = [read_bundle(p.resolve()) for p in bundles]
    output.parent.mkdir(parents=True,exist_ok=True)
    fd = os.open(output,os.O_CREAT|os.O_EXCL|os.O_WRONLY,0o600)
    os.close(fd)
    try:
        with closing(sqlite3.connect(source.as_uri()+"?mode=ro",uri=True)) as original:
            md = dict(original.execute("SELECT key,value FROM metadata"))
            if md.get("executable_sha256","").lower() != EXPECTED_SHA256:
                raise ValueError("Input database executable SHA mismatch")
            with closing(sqlite3.connect(output)) as db:
                original.backup(db)
                db.execute("PRAGMA foreign_keys=ON")
                db.executescript(SCHEMA)
                legacy = db.execute("SELECT 1 FROM sqlite_master WHERE type='table' AND name='visual_transient_decompilations'").fetchone()
                added = 0
                with db:
                    for digest,manifest,reports in parsed:
                        if db.execute("SELECT 1 FROM visual_evidence_bundles WHERE bundle_sha256=?",(digest,)).fetchone():
                            continue  # idempotent, preserve all earlier provenance
                        db.execute("INSERT INTO visual_evidence_bundles VALUES(?,?,?)",
                            (digest,EXPECTED_SHA256,json.dumps(manifest,ensure_ascii=False)))
                        for entry,text,mem,ins,funcs,provenance in reports:
                            db.execute("INSERT INTO visual_evidence_reports VALUES(?,?,?)",(digest,entry,text))
                            for va,data in mem.items():
                                db.execute("INSERT INTO visual_evidence_memory VALUES(?,?,?,?)",(digest,entry,va,data))
                            for win,rows in ins.items():
                                db.executemany("INSERT INTO visual_evidence_instructions VALUES(?,?,?,?,?,?)",
                                    [(digest,entry,win,i,va,line) for i,(va,line) in enumerate(rows)])
                            for va,name,code,callers in funcs:
                                db.execute("INSERT INTO visual_evidence_functions VALUES(?,?,?,?,?,?,?)",
                                    (digest,entry,va,name,code,json.dumps(callers),provenance))
                                if legacy and provenance == "ghidra_transient":
                                    # Compatibility with prior queries. Never replace an older
                                    # transient decompilation; alternatives remain in the new table.
                                    signature = next((l.strip() for l in code.splitlines() if l.strip() and not l.strip().startswith("/*")),"")
                                    db.execute("INSERT OR IGNORE INTO visual_transient_decompilations VALUES(?,?,?,?,?,?,?)",
                                        (va,name,signature,code,digest,entry,"Ghidra transient createFunction + decompile; project unchanged"))
                        added += 1
                if db.execute("PRAGMA integrity_check").fetchone()[0] != "ok" or db.execute("PRAGMA foreign_key_check").fetchone():
                    raise ValueError("Output SQLite integrity check failed")
                counts={table:db.execute("SELECT COUNT(*) FROM "+table).fetchone()[0] for table in
                    ("visual_evidence_bundles","visual_evidence_functions","visual_evidence_memory","visual_evidence_instructions")}
                return {"newBundles":added,**counts}
    except BaseException:
        output.unlink(missing_ok=True)
        raise


def main(argv=None):
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source",required=True,type=Path)
    parser.add_argument("--bundle",required=True,action="append",type=Path)
    parser.add_argument("--output",required=True,type=Path)
    args=parser.parse_args(argv)
    try:
        print(json.dumps(run(args.source,args.bundle,args.output)))
    except (OSError,ValueError,TypeError,KeyError,sqlite3.Error,zipfile.BadZipFile) as exc:
        parser.error(str(exc))


if __name__ == "__main__":
    main()
