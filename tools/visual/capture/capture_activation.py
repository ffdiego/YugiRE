"""Windows-only, bounded observer of one natural blue spell/trap activation."""
import argparse
import hashlib
import json
from pathlib import Path
import sys
import threading
import time

DAT_SHA = '7e8841ceb015861dacf898d4a62a909e399da14bb7d69c40de3afa3a4610e1ed'
EXE_SHA = 'db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de'


def fingerprint(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--pid', type=int, required=True)
    parser.add_argument('--exe', type=Path, required=True)
    parser.add_argument('--dat', type=Path, required=True)
    parser.add_argument('--seconds', type=int, default=60)
    parser.add_argument('--max-events', type=int, default=1024)
    parser.add_argument('--output-dir', type=Path, required=True)
    args = parser.parse_args()
    if sys.platform != 'win32':
        parser.error('Capture requires Windows; no game is launched by this tool')
    if not 1 <= args.seconds <= 120 or not 128 <= args.max_events <= 4096 or args.pid <= 0:
        parser.error('Invalid bounds (seconds 1..120, max-events 128..4096, PID positive)')
    root = Path(__file__).resolve().parents[3] / 'scratch'
    output = args.output_dir.resolve()
    if not output.is_relative_to(root.resolve()):
        parser.error('Capture output must be inside ignored repository scratch/')
    if fingerprint(args.exe) != EXE_SHA:
        parser.error('Executable SHA256 mismatch')
    if fingerprint(args.dat) != DAT_SHA:
        parser.error('DAT SHA256 mismatch')
    import frida  # Existing Windows environment only; no automatic installation.
    output.mkdir(parents=True, exist_ok=True)
    path = output / f'activation-{time.time_ns()}.jsonl'
    lock = threading.Lock()
    errors = []
    session = script = None
    footer = dict(finished=False, detached=False, readErrors=0, overflow=False, emitted=0)
    try:
        with path.open('x', encoding='utf-8') as stream:
            def write(row):
                with lock:
                    stream.write(json.dumps(row) + '\n')
                    stream.flush()
            def on_message(message, data):
                if message['type'] == 'send':
                    write(message['payload'])
                else:
                    errors.append(message)
            session = frida.attach(args.pid)
            script = session.create_script(Path(__file__).with_name('activation_observer.js').read_text())
            script.on('message', on_message)
            script.load()
            api = script.exports_sync
            identity = api.identity()
            if (identity['platform'] != 'windows' or identity['arch'] != 'ia32' or
                    Path(identity['path']).resolve() != args.exe.resolve() or fingerprint(identity['path']) != EXE_SHA):
                raise ValueError('Attached process does not match the attested executable')
            write(dict(kind='metadata', schema='joey-activation-v1', nativeExecution=True,
                       exeSha256=EXE_SHA, hostAttestedDatSha256=DAT_SHA, hostAttestedDatPath=str(args.dat.resolve()), pid=args.pid, identity=identity,
                       clock='Windows QueryPerformanceCounter', intervention='attach-only observation',
                       screenshotCapture=False))
            api.arm(args.max_events)
            print('Armed for ONE natural blue spell/trap activation. Activate it normally in Joey.')
            end = time.monotonic() + args.seconds
            try:
                while time.monotonic() < end and not errors:
                    if api.status()['finished']:
                        break
                    time.sleep(0.05)
            except KeyboardInterrupt:
                pass
            finally:
                footer = api.stop()
                # RPC return is ordered after the agent's previous send calls; unload drains script activity.
                script.unload()
                script = None
                footer.update(kind='footer', hostErrors=len(errors),
                              exeUnchanged=fingerprint(args.exe) == EXE_SHA, datUnchanged=fingerprint(args.dat) == DAT_SHA)
                write(footer)
    finally:
        if script is not None:
            try:
                script.exports_sync.stop()
            finally:
                script.unload()
        if session is not None:
            session.detach()
    print(path)
    return 0 if (footer.get('finished') and footer.get('detached') and footer.get('exeUnchanged')
                 and footer.get('datUnchanged') and not footer.get('overflow') and footer.get('readErrors') == 0 and not errors) else 2

if __name__ == '__main__':
    raise SystemExit(main())
