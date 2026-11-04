"""Run a cautious Sparks primary-callback replacement on the exact known Joey.

The native callback is kept in the process and calls the original generic
damage-command producer with (target_side, 200), without calling the shared
original Sparks/Ookazi callback. The Python host hash-checks the *loaded*
EXE before installing. Stop cleanly to restore the one edited callback slot.

EXPERIMENTAL: Not yet validated on a running Windows Joey. Back up saves and
use a disposable test installation. The patch only changes process memory.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from pe_preflight import probe


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pid", type=int, required=True, help="PID for joey_pc.exe")
    parser.add_argument("--damage", type=int, choices=(200, 300), default=200,
                        help="Sparks damage amount in LP (default: original 200)")
    args = parser.parse_args()

    try:
        import frida
    except ImportError:
        parser.error("Frida missing; run from experiments/hybrid_probe/.venv-frida")

    js = Path(__file__).with_name("sparks_primary_replacement.js")
    source = js.read_text(encoding="utf-8")
    if args.damage == 300:
        # Modify only the PUSH imm32 in the native x86 code: 200 -> 300.
        # The checked 200-LP baseline script on disk remains untouched.
        old = "    0x68, 0xC8, 0x00, 0x00, 0x00,"
        new = "    0x68, 0x2C, 0x01, 0x00, 0x00,"
        if source.count(old) != 1:
            parser.error("Unexpected native stub layout; refusing to modify damage amount")
        source = source.replace(old, new, 1)
    session = None
    script = None
    installed = False
    exit_code = 0

    try:
        print(f"[sparks] Attaching to PID {args.pid}", flush=True)
        session = frida.attach(args.pid)
        script = session.create_script(source)
        script.on("message", lambda msg, data:
                  print(f"[frida] {msg}", flush=True)
                  if msg.get("type") == "error" else None)
        script.load()
        api = script.exports_sync
        initial = api.status()
        exe = Path(initial["modulePath"])
        checks = probe(exe)
        if not checks["passed"]:
            print("[sparks] Executable hash/PE validation failed; no patch was installed.")
            print(json.dumps(checks["checks"], indent=2))
            return 2

        print(f"[sparks] Exact executable SHA256 confirmed. Patching only Sparks row 66. "
              f"Configured damage: {args.damage} LP.")
        installed_status = api.install()
        installed = True
        print("[sparks] Native replacement installed:")
        print(json.dumps(installed_status, ensure_ascii=False, indent=2), flush=True)
        print(f"[sparks] Play one Sparks in a NORMAL live duel; "
              f"expect {args.damage} LP damage (if not blocked).")
        print("[sparks] Press Enter to query the counter; type stop to restore.")
        while True:
            line = input("[sparks] > ").strip().lower()
            if line in ("stop", "exit", "quit"):
                break
            current = api.status()
            print(f"[sparks] callbackCalls={current['callbackCalls']}; "
                  f"currentPrimaryPointer={current['currentPrimaryPointer']}")
    except KeyboardInterrupt:
        print("\n[sparks] Interrupted by Ctrl+C; restoring pointer.")
    except Exception as exc:
        exit_code = 2
        print(f"[sparks] Failure: {exc}", flush=True)
    finally:
        if installed and script is not None:
            try:
                final = script.exports_sync.restore()
                print("[sparks] Original callback pointer restored; "
                      f"callbackCalls={final['callbackCalls']}", flush=True)
            except Exception as exc:
                exit_code = 2
                print(f"[sparks] RESTORE ERROR: {exc}", flush=True)
                print("[sparks] Close Joey normally before another experiment, "
                      "as the pointer may still be redirected.", flush=True)
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
    return exit_code


if __name__ == "__main__":
    raise SystemExit(main())
