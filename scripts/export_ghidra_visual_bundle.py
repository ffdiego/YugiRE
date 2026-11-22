#!/usr/bin/env python3
"""Collect many visual decompilations from the existing Ghidra project.

Outputs a ZIP in scratch/ for a private handoff; no EXE, DAT, or save is copied.
One headless Ghidra process at a time, with at most ten functions per run.
"""
from __future__ import annotations

import argparse
import datetime as dt
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[1]
INVENTORY = ROOT / "docs/architecture/native-renderer-inventory.md"
EXPECTED_SHA256 = "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de"
VA = re.compile(r"(?<![0-9A-Fa-f])[0-9A-Fa-f]{8}(?![0-9A-Fa-f])")
EXTRAS = [
    "0045D040", "0045D170", "0045D150", "0045CF80", "0045D4F0",
    "0044DD00", "005BC700", "005B9CA0", "005B9470", "0045C140",
    "0045C280", "0045C590", "0045C8B0", "00418A80", "00422C00",
    "00422840", "00422E30", "004511D0", "00413BC0", "00450260",
    "00449330", "00481040", "0041BE80", "0044C3E0", "00450C30",
]


def targets() -> list[str]:
    result: set[str] = set(EXTRAS)
    for line in INVENTORY.read_text(encoding="utf-8").splitlines():
        if not line.startswith("| ") or line.startswith("| ---") or line.startswith("| Elemento"):
            continue
        columns = [part.strip() for part in line.split("|")[1:-1]]
        if len(columns) != 6:
            continue
        # Update and Draw, not vtable constants from the Constructor column.
        for section in (columns[2], columns[3]):
            result.update(addr.upper() for addr in VA.findall(section))
    return sorted(result)


def choose_targets(explicit: list[str] | None, limit: int) -> list[str]:
    """Use exact VAs for a focused batch, leaving the default inventory unchanged."""
    if explicit is None:
        return targets()[:limit]
    selected = []
    for value in explicit:
        address = value.upper()
        if not re.fullmatch(r"[0-9A-F]{8}", address):
            raise ValueError(f"Invalid target VA: {value!r}; expected eight hex digits")
        selected.append(address)
    result = sorted(set(selected))
    if len(result) > limit:
        raise ValueError(f"Too many explicit VAs: {len(result)} exceeds max-functions={limit}")
    return result


def query_arguments(addresses: list[str], disassemble: bool = False,
                    transient: bool = False,
                    instruction_count: int = 100) -> list[str]:
    """Keep disassembly observational; transient function edits roll back."""
    if not 1 <= instruction_count <= 1024:
        raise ValueError("Instruction count must be between 1 and 1024")
    result = [("tempfunc:" if transient else "") + address for address in addresses]
    if disassemble:
        result.extend(f"ins:{address}:{instruction_count}" for address in addresses)
    return result


def memory_queries(ranges: list[str] | None) -> list[str]:
    """Split read-only native-memory evidence into QueryArchive's 512-byte chunks.

    Each range uses VA:BYTE_COUNT (e.g. 00A52EE8:2048). No bytes are
    checked into git; query output remains in a private scratch/ ZIP.
    """
    commands = []
    spans = []
    for item in ranges or []:
        match = re.fullmatch(r"([0-9a-fA-F]{8}):([1-9][0-9]*)", item)
        if not match:
            raise ValueError(f"Invalid memory range {item!r}; expected VA:decimal_bytes")
        address, count = int(match[1], 16), int(match[2])
        if count > 4096 or address + count - 1 > 0xFFFFFFFF:
            raise ValueError(f"Memory range exceeds 4096 bytes or VA space: {item!r}")
        if any(address < end and start < address + count for start, end in spans):
            raise ValueError(f"Overlapping memory range: {item!r}")
        spans.append((address, address + count))
        for offset in range(0, count, 512):
            commands.append(f"bytes:{address + offset:08X}:{min(512, count - offset)}")
    return commands


def validate_memory_dump(path: Path, commands: list[str]) -> bool:
    """Fail closed if a Ghidra memory read is incomplete or malformed."""
    expected = {}
    for command in commands:
        _, address, length = command.split(":")
        expected[address.lower()] = int(length)
    actual = {}
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        if not line.startswith("BYTES "):
            continue
        match = re.fullmatch(r"BYTES ([0-9a-fA-F]{8}) count=(\d+) (.*)", line)
        if not match:
            return False
        address = match[1].lower()
        chunk = match[3].split()
        if (address in actual or int(match[2]) != len(chunk) or
                any(not re.fullmatch(r"[0-9A-Fa-f]{2}", byte) for byte in chunk)):
            return False
        actual[address] = len(chunk)
    return actual == expected


def run_query(ghidra: Path, work: Path, name: str, arguments: list[str],
              timeout: int) -> tuple[bool, str]:
    output = work / (name + ".txt")
    log = work / (name + ".log")
    console = work / (name + ".console.log")
    with tempfile.TemporaryDirectory(prefix="headless-", dir=work) as directory:
        temporary = Path(directory)
        env = os.environ.copy()
        env["XDG_CONFIG_HOME"] = str(temporary / "config")
        env["GHIDRA_HEADLESS_JAVA_OPTIONS"] = (
            "-Dapplication.settingsdir=" + str(temporary / "settings") +
            " -Dapplication.cachedir=" + str(temporary / "cache")
        )
        command = [
            str(ghidra / "support/analyzeHeadless"),
            str(ROOT / "ghidra-projects"), "Joey", "-process", "joey_pc.exe",
            "-noanalysis", "-readOnly", "-scriptPath", str(ROOT / "scripts"),
            "-postScript", "QueryArchive.java", str(output), *arguments,
            "-log", str(log),
        ]
        try:
            completed = subprocess.run(
                command, cwd=ROOT, env=env, stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT, text=True, errors="replace", timeout=timeout
            )
            console.write_text(completed.stdout, encoding="utf-8")
            ok = completed.returncode == 0 and output.is_file() and output.stat().st_size > 0
            if ok and not output.read_text(encoding="utf-8", errors="replace").startswith(
                    "SHA256: " + EXPECTED_SHA256):
                return False, "Unexpected executable fingerprint"
            return ok, "ok" if ok else "Headless failed; inspect " + console.name
        except subprocess.TimeoutExpired as exc:
            console.write_text(str(exc), encoding="utf-8")
            return False, "Timed out"


def extract_functions(path: Path) -> set[str]:
    text = path.read_text(encoding="utf-8", errors="replace") if path.is_file() else ""
    return {x.upper() for x in re.findall(r"(?im)^FUNCTION\s+\S+\s+@([0-9a-f]{8})\s*$", text)}


def git_head() -> str | None:
    try:
        return subprocess.check_output(
            ["git", "rev-parse", "HEAD"], cwd=ROOT, text=True, stderr=subprocess.DEVNULL
        ).strip()
    except (OSError, subprocess.CalledProcessError):
        return None


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=Path("scratch/ghidra-visual-batch.zip"))
    parser.add_argument("--list-only", action="store_true",
                        help="preview targets without launching Ghidra")
    parser.add_argument("--targets", nargs="+", metavar="VA",
                        help="export only these explicit addresses, e.g. 0044EFD0")
    parser.add_argument("--disassemble", action="store_true",
                        help="include assembly instructions per selected VA")
    parser.add_argument("--instruction-count", type=int, default=100,
                        help="instructions per selected VA (1..1024; default 100)")
    parser.add_argument("--memory", action="append", metavar="VA:BYTES",
                        help="read a native memory range in private 512-byte chunks; "
                             "repeatable, e.g. --memory 00A52EE8:2048")
    parser.add_argument("--max-functions", type=int, default=120)
    parser.add_argument("--batch-size", type=int, default=10)
    parser.add_argument("--retry-missing", action="store_true",
                        help="attempt transient functions for unresolved entries")
    parser.add_argument("--timeout", type=int, default=600, help="seconds per headless invocation")
    args = parser.parse_args(argv)
    if not 1 <= args.batch_size <= 12 or args.max_functions < 1 or args.timeout < 30:
        parser.error("batch-size must be 1..12; max-functions >=1; timeout >=30")
    try:
        selected = choose_targets(args.targets, args.max_functions)
        memory_commands = memory_queries(args.memory)
        if not 1 <= args.instruction_count <= 1024:
            raise ValueError("Instruction count must be between 1 and 1024")
    except ValueError as exc:
        parser.error(str(exc))
    if args.list_only:
        print(f"{len(selected)} target functions; SHA256 expected: {EXPECTED_SHA256}")
        print("\n".join(selected))
        if memory_commands:
            print(f"{len(memory_commands)} memory chunks (read-only):")
            print("\n".join(memory_commands))
        return 0
    ghidra = Path(os.environ.get("GHIDRA_INSTALL_DIR", str(
        ROOT.parent / "tools/ghidra_12.2_PUBLIC"))).expanduser().resolve()
    if not (ghidra / "support/analyzeHeadless").is_file():
        parser.error("Ghidra headless not found; set GHIDRA_INSTALL_DIR")
    output = (ROOT / args.output).resolve() if not args.output.is_absolute() else args.output
    output.parent.mkdir(parents=True, exist_ok=True)
    stamp = dt.datetime.now(dt.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    work = ROOT / "scratch" / ("ghidra-visual-" + stamp)
    work.mkdir(parents=True, exist_ok=True)
    print(f"Exporting {len(selected)} functions to {work}", flush=True)
    acquired: set[str] = set()
    attempts: list[dict] = []
    for index in range(0, len(selected), args.batch_size):
        group = selected[index:index + args.batch_size]
        name = f"batch-{index // args.batch_size:03d}"
        ok, reason = run_query(ghidra, work, name,
                               query_arguments(group, args.disassemble,
                                               instruction_count=args.instruction_count),
                               args.timeout)
        found = extract_functions(work / (name + ".txt"))
        acquired.update(found)
        attempts.append({"name": name, "targets": group, "resolved": sorted(found),
                         "ok": ok, "message": reason})
        print(f"{name}: {len(found)}/{len(group)} decompiled ({reason})", flush=True)
    missing = [address for address in selected if address not in acquired]
    if args.retry_missing:
        for index, address in enumerate(missing):
            name = f"retry-{index:03d}-{address}"
            ok, reason = run_query(
                ghidra, work, name,
                query_arguments([address], args.disassemble, transient=True,
                                instruction_count=args.instruction_count),
                args.timeout)
            found = extract_functions(work / (name + ".txt"))
            acquired.update(found)
            attempts.append({"name": name, "targets": [address], "resolved": sorted(found),
                             "ok": ok, "message": reason})
            print(f"{name}: {len(found)}/1 ({reason})", flush=True)
    missing = [address for address in selected if address not in acquired]
    memory_ok = True
    if memory_commands:
        memory_ok, reason = run_query(
            ghidra, work, "memory-read", memory_commands, args.timeout)
        if memory_ok:
            memory_ok = validate_memory_dump(work / "memory-read.txt", memory_commands)
            if not memory_ok:
                reason = "Incomplete or malformed native memory-read output"
        attempts.append({"name": "memory-read", "targets": memory_commands,
                         "resolved": memory_commands if memory_ok else [],
                         "ok": memory_ok, "message": reason})
        print(f"memory-read: {len(memory_commands)} chunks "
              f"({'ok' if memory_ok else reason})", flush=True)
    metadata = {"schemaVersion": 1, "purpose": "private handoff: native visual decompilation",
                "executableSha256": EXPECTED_SHA256, "gitHead": git_head(),
                "selected": selected,
                "explicitTargets": args.targets is not None,
                "assemblyIncluded": args.disassemble,
                "instructionCount": args.instruction_count,
                "memoryRanges": args.memory or [],
                "memoryChunks": len(memory_commands),
                "memoryComplete": memory_ok,
                "decompiled": sorted(acquired & set(selected)),
                "missing": missing, "attempts": attempts,
                "note": "Decompiler pseudocode is not verified runtime behavior; no DAT/EXE included."}
    (work / "manifest.json").write_text(
        json.dumps(metadata, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )
    (work / "README.txt").write_text(
        "YugiRE Ghidra visual bundle. Upload this ZIP privately in a new chat.\n"
        "Start with manifest.json; review numbered batches and unresolved targets.\n"
        "Every report contains the joey_pc.exe SHA256. No game binaries are included.\n",
        encoding="utf-8"
    )
    with zipfile.ZipFile(output, "w", compression=zipfile.ZIP_DEFLATED,
                         compresslevel=6) as archive:
        for path in sorted(work.iterdir()):
            if path.is_file():
                archive.write(path, arcname=path.name)
    print(f"ZIP: {output} | {len(acquired & set(selected))}/{len(selected)} functions "
          f"| unresolved: {len(missing)}")
    return 0 if not missing and memory_ok else 1


if __name__ == "__main__":
    sys.exit(main())
