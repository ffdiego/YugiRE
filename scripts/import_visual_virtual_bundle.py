#!/usr/bin/env python3
"""Merge transient Ghidra visual-method decompilations into a NEW enriched SQLite.

Input ZIP: scripts/export_ghidra_visual_bundle.py output (Ghidra QueryArchive).
The original SQLite and original Ghidra project remain unchanged.
A transiently created function is evidence of decompilable code, not evidence
that the persistent Ghidra project contained a recognized function.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import sqlite3
import zipfile
from contextlib import closing
from pathlib import Path

EXE_SHA = "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de"
FUNC_HEADER = re.compile(r"(?m)^FUNCTION ([^\s]+) @([0-9a-fA-F]{8})\s*$")
CALLER = re.compile(r"(?m)^ CALLER ([0-9a-fA-F]{8})(?:\s|$)")
VA = re.compile(r"[0-9A-F]{8}\Z")
# These roles are based on the new pseudocode and *not* solely on the vtable offset.
ROLE_OVERRIDES = {
    ("005DACC4", 1): ("SetLocalPosition", "0041C030"),
    ("005DACC4", 2): ("GetLocalPosition", "0041C050"),
    ("005DACC4", 3): ("SetPositionWithResourceOffset", "0043E5B0"),
    ("005DB3C4", 2): ("NoOp", "004481F0"),
    ("005DB3D4", 2): ("NoOp", "004481F0"),
    ("005DB3C4", 3): ("UpdateAndDraw", "00455FF0"),
    ("005DB3D4", 3): ("UpdateAndDraw", "00456360"),
}


def _hex(s: str) -> str:
    value = s.upper()
    if not VA.fullmatch(value):
        raise ValueError(f"Invalid function address {s!r}")
    return value


def parse_report(content: str, file_name: str, sha: str) -> tuple[str, str, str, list[str], str]:
    header = re.search(r"(?m)^SHA256: ([a-f0-9]{64})\s*$", content)
    if header is None or header.group(1).lower() != sha:
        raise ValueError(f"Wrong or missing executable SHA in {file_name}")
    headers = list(FUNC_HEADER.finditer(content))
    if len(headers) != 1:
        raise ValueError(f"Expected exactly one function in {file_name}: got {len(headers)}")
    match = headers[0]
    name, address = match.group(1), _hex(match.group(2))
    remainder = content[match.end():].lstrip('\r\n')
    callers = [_hex(a) for a in CALLER.findall(remainder)]
    body = re.sub(r"^(?: CALLER .*\r?\n)+", "", remainder).strip() + "\n"
    if not ('{' in body and '\n}' in body) or not re.search(r"\bFUN_[0-9a-f]{8}\b", name, re.I):
        raise ValueError(f"Decompilation not recognizable in {file_name}")
    signature = next((line.strip() for line in body.splitlines() if line.strip().startswith(('void ', 'int ', 'undefined', 'uint ', 'char ', 'bool ', 'long ', 'short ', 'float ', 'double '))), '')
    return address, name, signature, callers, body


def read_bundle(bundle: Path):
    digest = hashlib.sha256(bundle.read_bytes()).hexdigest()
    with zipfile.ZipFile(bundle) as z:
        if 'manifest.json' not in z.namelist():
            raise ValueError('Missing manifest.json')
        if z.getinfo('manifest.json').file_size > 1_000_000:
            raise ValueError('Oversized manifest')
        manifest = json.loads(z.read('manifest.json'))
        if (manifest.get('schemaVersion') != 1 or
                manifest.get('executableSha256', '').lower() != EXE_SHA):
            raise ValueError('Unsupported bundle schema or wrong executable SHA')
        decompiled = {_hex(x) for x in manifest.get('decompiled', [])}
        selected = {_hex(x) for x in manifest.get('selected', [])}
        if not decompiled or not decompiled.issubset(selected):
            raise ValueError('Malformed selected/decompiled manifest lists')
        functions = {}
        size_total = 0
        for name in z.namelist():
            if not re.fullmatch(r'(?:retry-\d{3}-[0-9a-fA-F]{8}|batch-\d{3})\.txt', name):
                continue
            size = z.getinfo(name).file_size
            size_total += size
            if size > 1_000_000 or size_total > 15_000_000:
                raise ValueError('Oversized Ghidra text reports')
            content = z.read(name).decode('utf-8', errors='replace')
            for found in FUNC_HEADER.finditer(content):
                address = _hex(found.group(2))
                if address not in decompiled:
                    continue
                # The current batch selection is defined to contain no independent
                # function entries. Retry reports carry exactly one transient function.
                if name.startswith('batch-'):
                    continue
                result = parse_report(content, name, EXE_SHA)
                if address in functions and functions[address][3] != result[4]:
                    raise ValueError(f'Contradictory decompilations for {address}')
                functions[address] = (*result[1:], name)
        if set(functions) != decompiled:
            raise ValueError(f'Incomplete bundle: missing {sorted(decompiled - set(functions))}')
    return digest, functions


def run(source: Path, bundle: Path, output: Path, summary: Path | None = None):
    source, bundle, output = source.resolve(), bundle.resolve(), output.resolve()
    if not source.is_file() or not bundle.is_file() or source == output or output.exists():
        raise ValueError('Source and bundle must exist; output must be a NEW file')
    bundle_sha, extracted = read_bundle(bundle)
    output.parent.mkdir(parents=True, exist_ok=True)
    try:
        with closing(sqlite3.connect(f'file:{source}?mode=ro', uri=True)) as original:
            md = dict(original.execute('SELECT key,value FROM metadata'))
            if md.get('executable_sha256', '').lower() != EXE_SHA:
                raise ValueError('Input database executable SHA mismatch')
            if not original.execute("SELECT name FROM sqlite_master WHERE name='visual_virtual_methods'").fetchone():
                raise ValueError('Input database is missing the visual enrichment schema')
            with closing(sqlite3.connect(output)) as db:
                original.backup(db)
                db.execute('PRAGMA foreign_keys=ON')
                with db:
                    db.executescript('''
                      CREATE TABLE visual_transient_decompilations (
                        address TEXT PRIMARY KEY, name TEXT NOT NULL,
                        signature TEXT NOT NULL, code TEXT NOT NULL,
                        bundle_sha256 TEXT NOT NULL, source_entry TEXT NOT NULL,
                        provenance TEXT NOT NULL
                      );
                      CREATE TABLE visual_method_evidence (
                        vtable_address TEXT NOT NULL, slot_index INTEGER NOT NULL,
                        target_address TEXT NOT NULL, evidence_source TEXT NOT NULL,
                        source_entry TEXT NOT NULL,
                        PRIMARY KEY(vtable_address,slot_index,evidence_source)
                      );
                      CREATE VIEW visual_virtual_decompilation_coverage AS
                        SELECT v.subsystem, m.vtable_address, m.slot_index, m.role,
                               m.target_address, m.resolution,
                               (d.address IS NOT NULL) AS has_transient_decompilation,
                               d.bundle_sha256
                        FROM visual_virtual_methods m
                        JOIN visual_vtables v ON v.address=m.vtable_address
                        LEFT JOIN visual_transient_decompilations d
                          ON d.address=m.target_address;
                    ''')
                    slots = {x[0]: x[1:] for x in db.execute(
                        'SELECT slot_address,vtable_address,slot_index,target_address FROM visual_virtual_methods')}
                    seen_slots = {}
                    for address, (name, signature, callers, code, entry) in extracted.items():
                        db.execute('''INSERT INTO visual_transient_decompilations
                            VALUES (?,?,?,?,?,?,?)''',
                            (address,name,signature,code,bundle_sha,entry,
                             'Ghidra transient createFunction + decompile; project unchanged'))
                        for caller in callers:
                            if caller not in slots:
                                continue
                            table, index, old_target = slots[caller]
                            key = (table,index)
                            if old_target and old_target != address:
                                raise ValueError(f'Slot {key}: SQLite={old_target}, ZIP={address}')
                            if key in seen_slots and seen_slots[key] != address:
                                raise ValueError(f'Slot {key}: two targets in ZIP')
                            seen_slots[key] = address
                            db.execute('''INSERT INTO visual_method_evidence
                                VALUES (?,?,?,?,?)''',
                                (table,index,address,'ghidra_transient_xref',entry))
                            if old_target is None:
                                db.execute('''UPDATE visual_virtual_methods SET
                                    target_address=?, function_address=NULL,
                                    resolution='transient_decompiled',
                                    evidence_source='ghidra_transient_xref'
                                    WHERE vtable_address=? AND slot_index=?''',
                                    (address,table,index))
                    if len(seen_slots) < 1:
                        raise ValueError('No vtable pointer backlinks found')
                    roles_changed = 0
                    for (table, idx), (role, target) in ROLE_OVERRIDES.items():
                        row = db.execute('''SELECT target_address,role FROM visual_virtual_methods
                            WHERE vtable_address=? AND slot_index=?''',(table,idx)).fetchone()
                        if not row or row[0] != target:
                            raise ValueError(f'Role evidence mismatch for {table}+{idx*4:X}: {row}')
                        if row[1] != role:
                            db.execute('''UPDATE visual_virtual_methods SET role=?
                                WHERE vtable_address=? AND slot_index=?''', (role,table,idx))
                            roles_changed+=1
                if db.execute('PRAGMA integrity_check').fetchone()[0] != 'ok':
                    raise ValueError('Output SQLite integrity check failed')
                total = db.execute('SELECT COUNT(*) FROM visual_virtual_methods').fetchone()[0]
                covered = db.execute('''SELECT COUNT(*) FROM visual_virtual_decompilation_coverage
                    WHERE has_transient_decompilation=1''').fetchone()[0]
                filled = db.execute('''SELECT COUNT(*) FROM visual_virtual_methods
                    WHERE evidence_source='ghidra_transient_xref' ''').fetchone()[0]
                counts = dict(db.execute('''SELECT subsystem, SUM(has_transient_decompilation)
                    FROM visual_virtual_decompilation_coverage GROUP BY subsystem''').fetchall())
                audit = {"executableSha256": EXE_SHA, "bundleSha256":bundle_sha,
                         "decompiledUniqueFunctions":len(extracted),
                         "vtableSlotsWithTransientDecompilation":covered,
                         "totalVtableSlots":total,"linkedVtableSlots":len(seen_slots),
                         "filledFromTransientXrefs":filled,
                         "rolesCorrected":roles_changed,"perSubsystem":counts}
        if summary:
            summary.parent.mkdir(parents=True,exist_ok=True)
            summary.write_text(json.dumps(audit,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
        return audit
    except Exception:
        output.unlink(missing_ok=True)
        raise


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source',required=True,type=Path)
    parser.add_argument('--bundle',required=True,type=Path)
    parser.add_argument('--output',required=True,type=Path)
    parser.add_argument('--summary',type=Path)
    args=parser.parse_args(argv)
    try:
        print(json.dumps(run(args.source,args.bundle,args.output,args.summary),ensure_ascii=False))
    except (OSError,ValueError,sqlite3.Error,zipfile.BadZipFile) as exc:
        parser.error(str(exc))

if __name__ == '__main__':
    main()
