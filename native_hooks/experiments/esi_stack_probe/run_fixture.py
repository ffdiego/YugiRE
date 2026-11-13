#!/usr/bin/env python3
"""Isolated, bounded Windows x86 Frida experiment. Never attaches to Joey."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import queue
import re
import subprocess
import sys
import threading
from datetime import datetime, timezone

HERE = Path(__file__).resolve().parent
EXPECTED_EXE_NAME = "esi_fixture_x86.exe"
MAGIC = "YUGIRE_ESI_FIXTURE_V1"
READY = re.compile(rf"^{MAGIC} READY pid=(\d+) probe=0x([0-9A-Fa-f]{{8}}) al=([02])$")
RESULT = re.compile(
    rf"^{MAGIC} RESULT al=([02]) esi=0x([0-9A-Fa-f]{{8}}) "
    rf"route=(\d+) esp=0x([0-9A-Fa-f]{{8}}) healthy=([01])$"
)
EXPECTED_ESI = 0xD44


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(65536), b''):
            digest.update(block)
    return digest.hexdigest()


def check_fixture_exe(path: Path) -> Path:
    """Guard against accidentally starting/attaching to anything but our x86 PE."""
    path = path.resolve(strict=True)
    if path.name.lower() != EXPECTED_EXE_NAME:
        raise ValueError(f"Refusing any executable except {EXPECTED_EXE_NAME}")
    with path.open('rb') as file:
        mz = file.read(64)
        if len(mz) != 64 or mz[:2] != b'MZ':
            raise ValueError('Expected an MZ executable')
        pe_offset = int.from_bytes(mz[0x3c:0x40], 'little')
        if not 64 <= pe_offset <= 32 * 1024 * 1024:
            raise ValueError('Invalid PE header offset')
        file.seek(pe_offset)
        pe = file.read(6)
    if len(pe) < 6 or pe[:4] != b'PE\x00\x00':
        raise ValueError('Expected a PE executable')
    if int.from_bytes(pe[4:6], 'little') != 0x14c:
        raise ValueError('Expected x86 PE machine type 0x014c, not x64')
    if MAGIC.encode('ascii') not in path.read_bytes():
        raise ValueError('Missing embedded synthetic fixture magic; refusing injection')
    return path


def parse_ready(line: str, expected_pid: int, expected_al: int) -> str:
    match = READY.fullmatch(line.strip())
    if match is None:
        raise ValueError(f"Unexpected fixture READY: {line[:300]!r}")
    pid, probe_hex, al = match.groups()
    if int(pid) != expected_pid or int(al) != expected_al:
        raise ValueError('Fixture PID/AL mismatch; refusing to arm Frida')
    return '0x' + probe_hex.upper()


def parse_result(line: str, expected_al: int) -> dict:
    match = RESULT.fullmatch(line.strip())
    if match is None:
        raise ValueError('Missing/malformed fixture RESULT')
    al, esi, route, esp, healthy = match.groups()
    result = {
        'al': int(al),
        'restoredEsi': int(esi, 16),
        'fallthroughMarker': int(route),
        'espAfterFirstPop': int(esp, 16),
        'reportedHealthy': bool(int(healthy)),
    }
    if result['al'] != expected_al or result['fallthroughMarker'] not in (0, 1):
        raise ValueError('Fixture result outside expected input/branch domain')
    computed = result['restoredEsi'] == EXPECTED_ESI and result['fallthroughMarker'] == (1 if expected_al == 0 else 0)
    if computed != result['reportedHealthy']:
        raise ValueError('Fixture health verdict differs from raw evidence')
    return result


def classify(mode: str, exit_code: int | None, result: dict | None) -> str:
    if result is None:
        return 'inconclusive_crash_or_missing_result'
    if mode == 'object':
        if result['restoredEsi'] != EXPECTED_ESI:
            return 'object_overwrote_saved_esi'
        return 'object_not_reproduced' if result['reportedHealthy'] and exit_code == 0 else 'inconclusive_object'
    return 'pass' if result['reportedHealthy'] and exit_code == 0 else 'fail'


def read_line_with_timeout(stream, timeout: float) -> str:
    response = queue.Queue(maxsize=1)
    threading.Thread(target=lambda: response.put(stream.readline()), daemon=True).start()
    try:
        return response.get(timeout=timeout)
    except queue.Empty as exc:
        raise TimeoutError('Synthetic fixture did not announce READY') from exc


def run(args) -> tuple[dict, int]:
    if os.name != 'nt':
        raise RuntimeError('This runner only executes on Windows')
    if args.mode == 'object' and not args.allow_unsafe_object_control:
        raise ValueError('Object listener control requires --allow-unsafe-object-control (synthetic EXE only)')
    executable = check_fixture_exe(args.exe)
    agent_path = HERE / 'agent.js'
    report = {
        'kind': 'yugire-synthetic-esi-experiment-v1',
        'utc': datetime.now(timezone.utc).isoformat(),
        'mode': args.mode, 'al': args.al,
        'exePath': str(executable), 'exeSha256': sha256_file(executable),
        'agentSha256': sha256_file(agent_path),
        'windowsVersion': platform.platform(),
        'pythonVersion': platform.python_version(),
        'fixturePid': None, 'probeAddress': None,
        'fridaPythonVersion': None, 'fridaMessages': [],
        'stdout': None, 'stderr': None,
        'exitCode': None, 'result': None, 'verdict': 'not_run',
        'failure': None,
    }
    proc = None
    session = None
    try:
        proc = subprocess.Popen([str(executable), str(args.al)],
                                cwd=str(executable.parent), stdin=subprocess.PIPE,
                                stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                encoding='utf-8', errors='replace', bufsize=1)
        report['fixturePid'] = proc.pid
        first = read_line_with_timeout(proc.stdout, args.timeout)
        probe = parse_ready(first, proc.pid, args.al)
        report['probeAddress'] = probe
        if args.mode != 'baseline':
            import frida  # Only load Frida for the two injection conditions.
            report['fridaPythonVersion'] = getattr(frida, '__version__', None)
            session = frida.attach(proc.pid)
            armed = threading.Event()
            observed_hit = threading.Event()
            agent_source = ('const MODE = ' + json.dumps(args.mode) + ';\n'
                            'const TARGET = ' + json.dumps(probe) + ';\n'
                            + agent_path.read_text(encoding='utf-8'))
            script = session.create_script(agent_source)  # Preserve runtime default used by incident.

            def on_message(message, data):
                payload = message.get('payload') if message.get('type') == 'send' else None
                event = payload if isinstance(payload, dict) else {'kind':'frida_error', 'details': str(message)[:1000]}
                report['fridaMessages'].append(event)
                if event.get('kind') == 'armed':
                    armed.set()
                if event.get('kind') == 'hit':
                    observed_hit.set()

            script.on('message', on_message)
            script.load()
            if not armed.wait(args.timeout):
                raise TimeoutError('Frida did not confirm the isolated hook is armed')
        output, stderr = proc.communicate(input='GO\n', timeout=args.timeout)
        report['exitCode'] = proc.returncode
        report['stdout'] = first + output
        report['stderr'] = stderr[-3000:]
        candidates = [line for line in output.splitlines() if line.startswith(MAGIC + ' RESULT')]
        if len(candidates) == 1:
            report['result'] = parse_result(candidates[0], args.al)
        report['verdict'] = classify(args.mode, proc.returncode, report['result'])
        if args.mode != 'baseline':
            observed_hit.wait(min(args.timeout, 2.0))
            kinds = [e.get('kind') for e in report['fridaMessages']]
            if kinds.count('hit') != 1 or 'frida_error' in kinds:
                report['verdict'] = 'inconclusive_unobserved_or_invalid_hook'
            if report['result'] is not None and not any(e.get('kind') == 'armed' and e.get('target', '').lower() == probe.lower() for e in report['fridaMessages']):
                report['verdict'] = 'inconclusive_hook_address_mismatch'
    except Exception as exc:
        report['failure'] = f'{type(exc).__name__}: {exc}'
        if proc is not None:
            if proc.poll() is None:
                proc.kill()
            try:
                output, stderr = proc.communicate(timeout=5)
                report['stdout'] = (report['stdout'] or '') + output
                report['stderr'] = stderr[-3000:]
            except Exception:
                pass
            report['exitCode'] = proc.returncode
        report['verdict'] = 'inconclusive_setup_or_runtime_failure'
    finally:
        if session is not None:
            try:
                session.detach()
            except Exception:
                pass
    expected = report['verdict'] in {'pass', 'object_overwrote_saved_esi', 'object_not_reproduced'}
    return report, (0 if expected else 1)


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--exe', type=Path, required=True, help='Built esi_fixture_x86.exe only')
    parser.add_argument('--mode', choices=('baseline', 'probe', 'object'), default='baseline')
    parser.add_argument('--al', type=int, choices=(0, 2), default=0)
    parser.add_argument('--allow-unsafe-object-control', action='store_true',
                        help='Allow potentially corrupting object listener in disposable synthetic fixture only')
    parser.add_argument('--timeout', type=float, default=15.0)
    parser.add_argument('--output-dir', type=Path, default=HERE.parents[2] / 'scratch' / 'esi_stack_probe')
    args = parser.parse_args(argv)
    if args.timeout <= 0 or args.timeout > 120:
        parser.error('Timeout must be within 0..120 seconds')
    try:
        report, rc = run(args)
    except (ValueError, RuntimeError, FileNotFoundError) as exc:
        parser.error(str(exc))
    args.output_dir.mkdir(parents=True, exist_ok=True)
    output = args.output_dir / ('esi-' + datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
                                + f'-{args.mode}-al{args.al}.json')
    output.write_text(json.dumps(report, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    print(f"verdict={report['verdict']} exit={report['exitCode']} report={output}")
    if report['failure']:
        print(f"failure={report['failure']}", file=sys.stderr)
    return rc


if __name__ == '__main__':
    raise SystemExit(main())
