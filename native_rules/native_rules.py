"""Start or attach to Joey for the YugiRE Native Rules laboratory.

Default behavior stays compatible with patch_dual_rules.py: attach to an
existing process. --spawn boots the exact verified EXE in observer-only mode,
installs passive Frida hooks before the first resume, and keeps the console.
"""
from __future__ import annotations

import argparse
import json
import time
from pathlib import Path
import sys

import patch_dual_rules as legacy
from pe_preflight import probe


HERE = Path(__file__).resolve().parent


def batch_progress_line(seen: int, target: int, cycle: int,
                        total_cycles: int) -> str:
    """Human-readable event-based progress, not a percentage of duels."""
    percent = min(100.0, 100.0 * seen / target)
    total = (f" | total {100.0 * (cycle - 1 + percent / 100.0) / total_cycles:.1f}%"
             if total_cycles > 0 else "")
    return (f"[lab] cycle {cycle}" +
            (f"/{total_cycles}" if total_cycles > 0 else "/unlimited") +
            f": {percent:.1f}% ({seen}/{target} events){total}")


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
                        help="Experimental pre-boot override of -speedy 60 (e.g. 120 or 1000); "
                             "accepts any positive unsigned 32-bit value")
    parser.add_argument("--rotate-events", type=int, default=None,
                        help="Observer-only batch: save trace, kill OWN spawned Joey, "
                             "and restart at this many events (16..19000)")
    parser.add_argument("--cycles", type=int, default=None,
                        help="Number of spawned capture cycles (requires --rotate-events); "
                             "0 means repeat until interrupted")
    return parser


def preflight_spawn(args: argparse.Namespace, parser: argparse.ArgumentParser) -> Path:
    if args.pid is not None:
        parser.error("--spawn cannot be combined with --pid")
    if not args.observe_only:
        parser.error("--spawn currently requires --observe-only")
    if args.exe is None:
        parser.error("--spawn requires --exe PATH")
    if args.speed_value is not None and not 1 <= args.speed_value <= 0xFFFFFFFF:
        parser.error("--speed-value must fit an unsigned 32-bit integer (1..4294967295)")
    if args.rotate_events is not None and not 16 <= args.rotate_events <= 19000:
        parser.error("--rotate-events must be 16..19000 (leave space in the trace buffer)")
    if args.cycles is not None and args.rotate_events is None:
        parser.error("--cycles requires --rotate-events")
    if args.cycles is not None and args.cycles < 0:
        parser.error("--cycles must be >= 0 (0 means unlimited)")
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


def run_spawn(exe: Path, console_side: int, speed_value: int | None = None,
              rotate_at_events: int | None = None,
              poll_seconds: float = 0.25,
              cycle_number: int = 1, total_cycles: int = 1) -> int:
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
        if rotate_at_events is None:
            print("[native-rules] Recording from startup. Use 'validate' after the menu "
                  "is ready, 'trace dump' to export, or 'stop' to detach.", flush=True)
        else:
            print(f"[lab] Automatic capture to {rotate_at_events} events. "
                  "No interactive commands in batch mode; Ctrl+C stops the lab.",
                  flush=True)
        if rotate_at_events is not None:
            # The trace is a fixed-size ring buffer (max 20000). Poll well
            # below its capacity and STOP hooks before exporting. Never
            # overwrite a previous capture or touch processes we did not spawn.
            last_seen = -1
            last_progress_at = time.monotonic()
            last_print_at = 0.0
            last_print_bucket = -1
            while True:
                state = api.tracestatus()
                now = time.monotonic()
                if state["overwritten"] != 0:
                    raise RuntimeError(
                        f"Trace already lost {state['overwritten']} events; "
                        "cannot safely continue batch")
                if state["seen"] != last_seen:
                    last_seen = state["seen"]
                    last_progress_at = now
                elif now - last_progress_at > 120:
                    raise RuntimeError(
                        "No new trace events for 120 seconds; stopping this batch "
                        "instead of waiting forever (Joey may be blocked/exited)")

                # Show percentages every 5% and an unchanged heartbeat
                # every 5 seconds (e.g. during slow game startup).
                bucket = min(20, int(state["seen"] * 20 / rotate_at_events))
                if bucket > last_print_bucket or now - last_print_at >= 5.0:
                    print(batch_progress_line(state["seen"], rotate_at_events,
                                              cycle_number, total_cycles),
                          flush=True)
                    last_print_at = now
                    last_print_bucket = bucket

                if state["seen"] >= rotate_at_events:
                    print(f"[lab] Threshold reached: {state['seen']} events; "
                          "stopping trace before export.", flush=True)
                    api.tracestop()
                    break
                time.sleep(poll_seconds)
        else:
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
        if pid is not None and (not resumed or rotate_at_events is not None):
            # In interactive mode, leave a resumed game alive. In batch mode,
            # terminate ONLY the PID returned by this invocation of spawn(),
            # and only after attempting to save the trace and detaching Frida.
            try:
                device.kill(pid)
            except Exception as exc:
                exit_code = 2
                print(f"[native-rules] Failed to kill owned Joey PID {pid}: {exc}",
                      flush=True)
        # Do not kill a resumed game when closing its passive interactive observer.
    return exit_code


def run_batch(exe: Path, console_side: int, speed_value: int | None,
              rotate_events: int, cycles: int) -> int:
    """Run bounded, sequential captures; 0 cycles means until Ctrl+C."""
    completed = 0
    try:
        while cycles == 0 or completed < cycles:
            print(f"[lab] Starting capture cycle {completed + 1}" +
                  ("" if cycles == 0 else f"/{cycles}"), flush=True)
            result = run_spawn(exe, console_side, speed_value,
                               rotate_at_events=rotate_events,
                               cycle_number=completed + 1, total_cycles=cycles)
            if result != 0:
                return result  # Never spawn next cycle when export/cleanup failed.
            completed += 1
        print(f"[lab] Finished {completed} capture cycles.", flush=True)
        return 0
    except KeyboardInterrupt:
        print(f"\n[lab] Interrupted after {completed} completed cycles.", flush=True)
        return 130


def main(argv: list[str] | None = None) -> int:
    original = list(sys.argv[1:] if argv is None else argv)
    parser = build_parser()
    if not original or (not any(a == "--spawn" for a in original)):
        if "--exe" in original:
            parser.error("--exe requires --spawn")
        for name in ("--speed-value", "--rotate-events", "--cycles"):
            if name in original:
                parser.error(f"{name} requires --spawn")
        return legacy.main(original)
    args, unknown = parser.parse_known_args(original)
    if unknown:
        parser.error("Unrecognized options with --spawn: " + " ".join(unknown))
    exe = preflight_spawn(args, parser)
    if args.rotate_events is not None:
        return run_batch(exe, args.console_side, args.speed_value,
                         args.rotate_events, args.cycles if args.cycles is not None else 1)
    return run_spawn(exe, args.console_side, args.speed_value)


if __name__ == "__main__":
    raise SystemExit(main())
