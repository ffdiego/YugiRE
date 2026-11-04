"""Run a reversible Jam Breeding Machine primary-callback replacement.

Must use the same Windows Python venv where Frida is installed.
The loaded Joey executable is hash-checked BEFORE native memory is modified.
Only the primary pointer of resource ID 1426 is redirected, to a native x86
counter stub returning zero, which is equivalent to the observed original stub.
No modification is made to joey_pc.exe on disk.

WARNING: Unexpected force-kill of this host may leave the pointer redirected
until Joey itself exits. The native stub remains allocated/valid meanwhile.
Always stop using 'stop' or Ctrl+C, and close/restart Joey after an abnormal exit.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from pe_preflight import probe


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pid", type=int, required=True, help="PID of joey_pc.exe")
    args = parser.parse_args()

    try:
        import frida
    except ImportError as exc:
        parser.error("Frida is not installed in this Python environment; use .venv-frida")
        raise AssertionError from exc

    agent = Path(__file__).with_name("jam_primary_replacement.js")
    session = None
    script = None
    patch_installed = False
    result = 0

    try:
        print(f"[jam] Attaching to Joey PID {args.pid}", flush=True)
        session = frida.attach(args.pid)
        script = session.create_script(agent.read_text(encoding="utf-8"))
        script.on("message", lambda message, _data: print(f"[frida] {message}", flush=True)
                  if message.get("type") == "error" else None)
        script.load()
        api = script.exports_sync

        status = api.status()
        # Hash the *actual loaded module's path*, not a possibly unrelated
        # executable supplied by the operator.
        exe = Path(status["modulePath"])
        checks = probe(exe)
        if not checks["passed"]:
            print("[jam] Refusing to patch: executable fingerprint check failed.")
            print(json.dumps(checks["checks"], indent=2))
            return 2

        print("[jam] Exact original executable SHA256 confirmed.")
        print("[jam] Installing resource 1426, row 378, primary callback only.")
        status = api.install()
        patch_installed = True
        print("[jam] Installed:\n" + json.dumps(status, indent=2, ensure_ascii=False))
        print("[jam] The native replacement returns zero, exactly as the original stub does.")
        print("[jam] Keep this console open while you play the duel.")
        print("[jam] Press ENTER to read the callback counter; type 'stop' to restore.")
        while True:
            command = input("[jam] > ").strip().lower()
            if command in ("stop", "exit", "quit"):
                break
            status = api.status()
            print(f"[jam] callbackCalls = {status['callbackCalls']} "
                  "(this is NOT the number of created tokens)")
    except KeyboardInterrupt:
        print("\n[jam] Ctrl+C received; restoring original pointer.")
    except Exception as exc:
        result = 2
        print(f"[jam] Error: {exc}")
    finally:
        if script is not None and patch_installed:
            try:
                final = script.exports_sync.restore()
                print("[jam] Pointer restoration confirmed. "
                      f"Callback calls: {final['callbackCalls']}")
            except Exception as exc:
                result = 2
                print(f"[jam] RESTORE FAILED: {exc}")
                print("[jam] Close Joey normally before further tests. "
                      "Do not unload/reload another patch into this game session.")
        if script is not None:
            try:
                script.unload()
            except Exception:
                pass
        if session is not None:
            try:
                session.detach()
            except Exception:
                pass
    return result


if __name__ == "__main__":
    raise SystemExit(main())
