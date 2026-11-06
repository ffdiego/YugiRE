"""Start or attach to Joey for the YugiRE Native Rules laboratory.

Default behavior stays compatible with patch_dual_rules.py: attach to an
existing process. --spawn boots the exact verified EXE in observer-only mode,
installs passive Frida hooks before the first resume, and keeps the console.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import sys

import patch_dual_rules as legacy
from pe_preflight import probe


HERE = Path(__file__).resolve().parent


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--spawn", action="store_true",
                        help="Spawn Joey suspended and capture initialization")
    parser.add_argument("--exe", type=Path,
                        help="Path to the original joey_pc.exe (required with --spawn)")
    parser.add_argument("--observe-only", action="store_true",
                        help="Never load the DLL or modify native callbacks")
    parser.add_argument("--pid", type=int, default=None,
                        help="Only valid when attaching to an existing process")
    parser.add_argument("--console-side", type=int, choices=(0, 1), default=1)
    parser.add_argument("--speed-value", type=int, default=None,
                        help="Experimental pre-boot override of -speedy 60 (e.g. 120), "
                             "only with --spawn --observe-only")
    return parser


def preflight_spawn(args: argparse.Namespace, parser: argparse.ArgumentParser) -> Path:
    if args.pid is not None:
        parser.error("--spawn cannot be combined with --pid")
    if not args.observe_only:
        parser.error("--spawn currently requires --observe-only")
    if args.exe is None:
        parser.error("--spawn requires --exe PATH")
    if args.speed_value is not None and not 61 <= args.speed_value <= 240:
        parser.error("--speed-value must be an integer from 61 to 240")
    try:
        exe = args.exe.expanduser().resolve(strict=True)
        if not exe.is_file() or exe.name.casefold() != "joey_pc.exe":
            parser.error("--exe must point to an existing joey_pc.exe")
        details = probe(exe)
    except (OSError, ValueError) as exc:
        parser.error(f"Unable to validate Joey executable: {exc}")
    if not details["passed"]:
        print(json.dumps(details["checks"], indent=2))
        parser.error("EXE fingerprint mismatch (PE32 x86 / base / SHA256)")
    return exe


def run_spawn(exe: Path, console_side: int, speed_value: int | None = None) -> int:
    try:
        import frida
    except ImportError as exc:
        raise RuntimeError("Frida is not installed in this Python environment") from exc

    device = frida.get_local_device()
    pid = None
    session = None
    script = None
    resumed = False
    observer_armed = False
    exit_code = 0
    try:
        pid = device.spawn([str(exe), "-speedy"], cwd=str(exe.parent))
        print(f"[native-rules] Spawned suspended Joey PID {pid} (-speedy)", flush=True)
        session = device.attach(pid)
        source = (HERE / "yugire_dual_loader.js").read_text(encoding="utf-8")
        script = session.create_script(source)
        script.on("message", lambda msg, data:
                  print(f"[frida] {msg}", flush=True)
                  if msg.get("type") == "error" else None)
        script.load()
        api = script.exports_sync

        # The Frida agent must NOT validate uninitialized card records here.
        loaded = api.bootstrapinfo()
        loaded_path = Path(loaded["modulePath"])
        loaded_checks = probe(loaded_path)
        if not loaded_checks["passed"]:
            raise RuntimeError("Loaded Joey module did not pass SHA256/PE preflight")
        if loaded_path.resolve() != exe:
            raise RuntimeError("Loaded module path does not match --exe")

        api.armstartupobserver()
        observer_armed = True
        api.tracestart(20000)
        api.tracedemostart()
        api.tracemark("startup-hooks-armed")
        if speed_value is not None:
            # Arm BEFORE resume: Joey writes 60 early in its argument parser.
            # The Frida hook runs just after that write and replaces 60
            # with the requested value while startup is still in progress.
            state = api.armspeedoverride(speed_value)
            print(f"[native-rules] Experimental -speedy override armed: "
                  f"60 -> {speed_value} ({state})", flush=True)
        print("[native-rules] Passive hooks ready; resuming Joey.", flush=True)
        device.resume(pid)
        resumed = True
        print("[native-rules] Recording from startup. Use 'validate' after the menu "
              "is ready, 'trace dump' to export, or 'stop' to detach.", flush=True)
        legacy.show_help(console_side)
        while True:
            try:
                raw = input("[native-rules] > ").strip()
                if raw.casefold() == "validate":
                    print(f"[native-rules] {api.validateruntime()}", flush=True)
                    continue
                if raw.casefold() == "speed status":
                    print(f"[native-rules] Speed setting: {api.speedstatus()}", flush=True)
                    continue
                if raw.casefold() == "speed restore":
                    print(f"[native-rules] Speed setting: {api.stopspeedoverride()}",
                          flush=True)
                    continue
                if not legacy.handle_command(raw, api, console_side):
                    break
            except (KeyboardInterrupt, EOFError):
                print("\n[native-rules] Closing observer.", flush=True)
                break
            except Exception as exc:
                print(f"[console] Command failed: {exc}", flush=True)
    except Exception as exc:
        exit_code = 2
        print(f"[native-rules] ERROR: {exc}", flush=True)
    finally:
        if script is not None and observer_armed:
            try:
                api = script.exports_sync
                status = api.tracestatus()
                if status.get("hasUnsaved", status.get("retained", 0) > 0):
                    legacy.save_command_trace(api)
                elif status["active"]:
                    api.tracestop()
            except Exception as exc:
                exit_code = 2
                print(f"[trace] Could not save startup capture: {exc}", flush=True)
            try:
                script.exports_sync.restore()
            except Exception as exc:
                exit_code = 2
                print(f"[native-rules] Restore error: {exc}", flush=True)
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
        if pid is not None and not resumed:
            # A failed early attach must not leave an invisible suspended process.
            try:
                device.kill(pid)
            except Exception as exc:
                print(f"[native-rules] Failed to kill suspended Joey {pid}: {exc}",
                      flush=True)
        # Do not kill a resumed game when closing its passive observer.
    return exit_code


def main(argv: list[str] | None = None) -> int:
    original = list(sys.argv[1:] if argv is None else argv)
    parser = build_parser()
    if not original or (not any(a == "--spawn" for a in original)):
        if "--exe" in original:
            parser.error("--exe requires --spawn")
        if "--speed-value" in original:
            parser.error("--speed-value requires --spawn")
        return legacy.main(original)
    args, unknown = parser.parse_known_args(original)
    if unknown:
        parser.error("Unrecognized options with --spawn: " + " ".join(unknown))
    exe = preflight_spawn(args, parser)
    return run_spawn(exe, args.console_side, args.speed_value)


if __name__ == "__main__":
    raise SystemExit(main())
