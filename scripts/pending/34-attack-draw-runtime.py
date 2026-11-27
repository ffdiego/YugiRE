#!/usr/bin/env python3
"""Record attack blits, selection backgrounds or dispatcher cadence in a live duel."""
from __future__ import annotations

import argparse
import ctypes
import hashlib
import json
from pathlib import Path
import statistics
import sys
import time

ROOT = Path(__file__).resolve().parents[2]
EXPECTED = "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de"
AGENT = Path(__file__).with_suffix(".js")
SCENARIOS = {
    "attack": "Perform one targeted or direct attack.",
    "selection": "Open a card-selection window.",
    "cadence": "Stay in one duel scene during the recording.",
}


def verify_image(pid: int, exe: Path):
    """Keep the exact process handle live through attachment to prevent PID reuse."""
    from ctypes import wintypes

    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.QueryFullProcessImageNameW.argtypes = [
        wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD)]
    kernel.QueryFullProcessImageNameW.restype = wintypes.BOOL
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel.CloseHandle.restype = wintypes.BOOL
    handle = kernel.OpenProcess(0x1000, False, pid)
    if not handle:
        raise ctypes.WinError(ctypes.get_last_error())
    try:
        buffer = ctypes.create_unicode_buffer(32768)
        size = wintypes.DWORD(len(buffer))
        if not kernel.QueryFullProcessImageNameW(handle, 0, buffer, ctypes.byref(size)):
            raise ctypes.WinError(ctypes.get_last_error())
        if not Path(buffer.value).samefile(exe):
            raise ValueError("PID does not belong to the specified original executable")
    except BaseException:
        kernel.CloseHandle(handle)
        raise
    return kernel, handle


def capture(pid: int, exe: Path, seconds: int, mode: str) -> dict:
    import frida

    kernel, handle = verify_image(pid, exe)
    try:
        session = frida.attach(pid)
        try:
            errors = []
            script = session.create_script(AGENT.read_text(encoding="utf-8"))
            script.on("message", lambda message, _: errors.append(message)
                      if message.get("type") == "error" else None)
            script.load()
            if not Path(script.exports_sync.image_path()).samefile(exe):
                raise ValueError("Attached process image changed")
            script.exports_sync.start(mode)
            print(f"Recording for {seconds} seconds. {SCENARIOS[mode]}", flush=True)
            time.sleep(seconds)
            result = script.exports_sync.stop()
            if errors:
                raise RuntimeError(f"Instrumentation raised an error: {errors[0]}")
            return result
        finally:
            session.detach()
    finally:
        kernel.CloseHandle(handle)


def summarize_cadence(result: dict) -> list[dict]:
    """Derive same-thread intervals and check Update/Draw membership by scene call."""
    frequency = result["frequencyHz"]
    threads = {}
    for event in result["events"]:
        by_frame = threads.setdefault(event["threadId"], {})
        by_frame.setdefault(event["frame"], []).append(event)

    summary = []
    for thread_id, by_frame in threads.items():
        pairs = 0
        with_no_dispatch = 0
        out_of_order = 0
        timestamps = []
        for events in by_frame.values():
            order = tuple(event["kind"] for event in events)
            if order == ("scene",):
                with_no_dispatch += 1
            elif order == ("scene", "dispatch-update", "dispatch-draw") and (
                events[0]["ticks"] <= events[1]["ticks"] <= events[2]["ticks"]
                and events[1]["mask"] == events[2]["mask"]
            ):
                pairs += 1
            else:
                out_of_order += 1
            if events[0]["kind"] == "scene":
                timestamps.append(events[0]["ticks"])

        elapsed = [1000 * (end - start) / frequency for start, end in zip(timestamps, timestamps[1:])]
        summary.append({
            "threadId": thread_id,
            "orderedUpdateDrawFrames": pairs,
            "framesWithoutDispatch": with_no_dispatch,
            "unexpectedDispatchFrames": out_of_order,
            "sceneIntervals": len(elapsed),
            "minSceneIntervalMs": min(elapsed, default=None),
            "medianSceneIntervalMs": statistics.median(elapsed) if elapsed else None,
            "maxSceneIntervalMs": max(elapsed, default=None),
        })
    return summary


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pid", type=int, required=True)
    parser.add_argument("--exe", type=Path, required=True)
    parser.add_argument("--seconds", type=int, default=20)
    parser.add_argument("--mode", choices=SCENARIOS, default="attack")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    if sys.platform != "win32":
        parser.error("Run on Windows with an original duel already open")
    if args.pid <= 0 or not 1 <= args.seconds <= 60:
        parser.error("Expected a positive PID and --seconds in [1, 60]")
    default_output = ROOT / "scratch/visual-review" / f"native-{args.mode}-draw.json"
    output = (args.output or default_output).resolve()
    if not output.is_relative_to(ROOT / "scratch") or output.exists():
        parser.error("Write a new output file under scratch/")
    exe = args.exe.resolve(strict=True)
    with exe.open("rb") as binary:
        if hashlib.file_digest(binary, "sha256").hexdigest() != EXPECTED:
            parser.error("Unexpected joey_pc.exe SHA-256")
    try:
        result = capture(args.pid, exe, args.seconds, args.mode)
    except (OSError, ValueError, RuntimeError, ImportError) as error:
        parser.exit(2, f"{error}\n")
    if args.mode == "selection":
        observed = any(event["kind"] == "selection-background" for event in result["events"])
    elif args.mode == "cadence":
        result["cadenceByThread"] = summarize_cadence(result)
        observed = any(row["orderedUpdateDrawFrames"] >= 2 for row in result["cadenceByThread"])
    else:
        passes = {event["pass"] for event in result["events"] if event["kind"] == "blit-request"}
        observed = {"aim", "flight", "impact", "partial-sword"} <= passes
    complete = result["dropped"] == 0 and observed
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("x", encoding="utf-8") as report:
        json.dump({"schemaVersion": 1, "executableSha256": EXPECTED, "mode": args.mode, "complete": complete,
                   **result}, report, indent=2)
        report.write("\n")
    print(f"{result['frames']} scene calls, {len(result['events'])} events, "
          f"{result['dropped']} dropped. {output}")
    return 0 if complete else 1


if __name__ == "__main__":
    raise SystemExit(main())
