"""Install and monitor a C++ x86 Sparks callback in the original Joey process.

Use the same Python venv as earlier Frida experiments. Validates the SHA256
of the *actual loaded* joey_pc.exe and PE32 x86 of the supplied DLL before
modifying any native callback pointer. The DLL remains loaded after restore.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from pe_preflight import parse_pe, probe

HERE = Path(__file__).resolve().parent
DEFAULT_DLL = HERE / "native_sparks" / "build" / "Release" / "joey_rules.dll"


def verify_dll(path: Path) -> None:
    if not path.is_file():
        raise ValueError(f"DLL not found: {path}. Build native_sparks first or use --dll")
    info = parse_pe(path.read_bytes())
    if info["machine"] != 0x14C:
        raise ValueError(f"DLL must be x86 (machine 0x014C): {path}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pid", type=int, required=True, help="PID of joey_pc.exe")
    parser.add_argument("--dll", type=Path, default=DEFAULT_DLL,
                        help="Absolute/local path to joey_rules.dll (x86)")
    parser.add_argument("--damage", type=int, choices=(200, 300), default=200)
    args = parser.parse_args()
    dll_path = args.dll.resolve()

    try:
        verify_dll(dll_path)
    except (ValueError, OSError) as exc:
        parser.error(str(exc))

    try:
        import frida
    except ImportError:
        parser.error("Frida unavailable: use experiments/hybrid_probe/.venv-frida")

    session = None
    script = None
    installed = False
    exit_code = 0
    try:
        print(f"[cpp-sparks] Attaching to PID {args.pid}", flush=True)
        session = frida.attach(args.pid)
        source = (HERE / "native_sparks_loader.js").read_text(encoding="utf-8")
        script = session.create_script(source)
        script.on("message", lambda msg, _data: print(f"[frida] {msg}", flush=True)
                  if msg.get("type") == "error" else None)
        script.load()
        api = script.exports_sync

        original_exe_path = Path(api.status()["modulePath"])
        preflight = probe(original_exe_path)
        if not preflight["passed"]:
            print("[cpp-sparks] EXE SHA256 / PE check FAILED, no patch installed.")
            print(json.dumps(preflight["checks"], indent=2))
            return 2
        print("[cpp-sparks] Original EXE fingerprint confirmed.", flush=True)

        result = api.install(str(dll_path), args.damage)
        installed = True
        print("[cpp-sparks] C++ DLL installed: " + json.dumps(result, indent=2), flush=True)
        print(f"[cpp-sparks] Play one Sparks in a NORMAL live duel. "
              f"Expected damage: {args.damage} LP.")
        print("[cpp-sparks] Press Enter for counters; type 'stop' to restore the pointer.")
        while True:
            command = input("[cpp-sparks] > ").strip().lower()
            if command in ("stop", "quit", "exit"):
                break
            state = api.status()
            print(f"[cpp-sparks] calls={state['callbackCalls']} "
                  f"damageCommands={state['damageCommandsEmitted']} "
                  f"configuredDamage={state['configuredDamage']} "
                  f"pointer={state['currentPrimaryPointer']}")
    except KeyboardInterrupt:
        print("\n[cpp-sparks] Ctrl+C: restoring original pointer.")
    except Exception as exc:
        exit_code = 2
        print(f"[cpp-sparks] ERROR: {exc}", flush=True)
    finally:
        if installed and script is not None:
            try:
                end = script.exports_sync.restore()
                print("[cpp-sparks] Original pointer restored. "
                      f"calls={end['callbackCalls']}, "
                      f"damageCommands={end['damageCommandsEmitted']}", flush=True)
            except Exception as exc:
                exit_code = 2
                print(f"[cpp-sparks] RESTORE FAILED: {exc}", flush=True)
                print("[cpp-sparks] Close Joey normally before another test.", flush=True)
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
