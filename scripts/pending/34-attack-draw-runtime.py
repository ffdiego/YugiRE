#!/usr/bin/env python3
"""V01/V04: record attack Update, Draw and rotated blit requests in a live duel."""
from __future__ import annotations

import argparse
import ctypes
import hashlib
import json
from pathlib import Path
import sys
import time

ROOT = Path(__file__).resolve().parents[2]
EXPECTED = "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de"
AGENT = Path(__file__).with_suffix(".js")


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


def capture(pid: int, exe: Path, seconds: int) -> dict:
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
            script.exports_sync.start()
            print(f"Recording for {seconds} seconds. Perform one targeted or direct attack.", flush=True)
            time.sleep(seconds)
            result = script.exports_sync.stop()
            if errors:
                raise RuntimeError(f"Instrumentation raised an error: {errors[0]}")
            return result
        finally:
            session.detach()
    finally:
        kernel.CloseHandle(handle)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pid", type=int, required=True)
    parser.add_argument("--exe", type=Path, required=True)
    parser.add_argument("--seconds", type=int, default=20)
    parser.add_argument("--output", type=Path,
                        default=ROOT / "scratch/visual-review/native-attack-draw.json")
    args = parser.parse_args()
    if sys.platform != "win32":
        parser.error("Run on Windows with an original duel already open")
    if args.pid <= 0 or not 1 <= args.seconds <= 60:
        parser.error("Expected a positive PID and --seconds in [1, 60]")
    output = args.output.resolve()
    if not output.is_relative_to(ROOT / "scratch") or output.exists():
        parser.error("Write a new output file under scratch/")
    exe = args.exe.resolve(strict=True)
    with exe.open("rb") as binary:
        if hashlib.file_digest(binary, "sha256").hexdigest() != EXPECTED:
            parser.error("Unexpected joey_pc.exe SHA-256")
    try:
        result = capture(args.pid, exe, args.seconds)
    except (OSError, ValueError, RuntimeError, ImportError) as error:
        parser.exit(2, f"{error}\n")
    passes = {event["pass"] for event in result["events"] if event["kind"] == "blit-request"}
    complete = result["dropped"] == 0 and {"aim", "flight", "impact", "partial-sword"} <= passes
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("x", encoding="utf-8") as report:
        json.dump({"schemaVersion": 1, "executableSha256": EXPECTED, "complete": complete,
                   **result}, report, indent=2)
        report.write("\n")
    print(f"{result['frames']} scene calls, {len(result['events'])} events, "
          f"{result['dropped']} dropped. {output}")
    return 0 if complete else 1


if __name__ == "__main__":
    raise SystemExit(main())
