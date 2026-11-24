#!/usr/bin/env python3
"""V01: read the initialized original cosine table from an explicit PID.

Windows with the original game open; requires Frida in the current Python
environment. No hooks, memory writes, or process termination. Output is private.
"""
import argparse
import hashlib
import json
from pathlib import Path
import sys

EXPECTED = "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de"
AGENT = """
rpc.exports = {
  capture() {
    const m = Process.mainModule;
    const initialized = m.base.add(0x6536e8);
    const table = m.base.add(0x652ee8);
    if (Process.arch !== 'ia32' || m.size < 0x6536ec)
      throw new Error('Unexpected original process architecture or image size');
    if (initialized.readU32() !== 1)
      throw new Error('Cosine table has not initialized; enter a duel and retry');
    const values = Array.from({length: 512}, (_, i) => table.add(i*4).readS32());
    if (initialized.readU32() !== 1 ||
        values.some((v, i) => v !== table.add(i*4).readS32()))
      throw new Error('Cosine capture was not stable');
    return {path: m.path, values, initialized: 1};
  }
};
"""

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pid", type=int, required=True)
    parser.add_argument("--exe", type=Path, required=True)
    parser.add_argument("--output", type=Path,
                        default=Path("scratch/visual-review/native-cosine.json"))
    args = parser.parse_args()
    if sys.platform != "win32":
        parser.error("Run on Windows with the original game")
    if args.pid <= 0:
        parser.error("PID must be positive")
    exe = args.exe.resolve(strict=True)
    if hashlib.sha256(exe.read_bytes()).hexdigest() != EXPECTED:
        parser.error("Unexpected joey_pc.exe SHA-256")
    # Verify the selected PID's image path before injecting the read-only agent.
    import ctypes
    from ctypes import wintypes
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.QueryFullProcessImageNameW.argtypes = [
        wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD)]
    kernel.QueryFullProcessImageNameW.restype = wintypes.BOOL
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    handle = kernel.OpenProcess(0x1000, False, args.pid)
    if not handle:
        raise ctypes.WinError(ctypes.get_last_error())
    try:
        buffer = ctypes.create_unicode_buffer(32768)
        size = wintypes.DWORD(len(buffer))
        if not kernel.QueryFullProcessImageNameW(handle, 0, buffer, ctypes.byref(size)):
            raise ctypes.WinError(ctypes.get_last_error())
        if not Path(buffer.value).samefile(exe):
            parser.error("PID does not belong to the specified original executable")
        import frida
        session = frida.attach(args.pid)
        try:
            script = session.create_script(AGENT)
            script.load()
            data = script.exports_sync.capture()
            if not Path(data["path"]).samefile(exe):
                raise ValueError("Attached process image changed")
        finally:
            session.detach()
    finally:
        kernel.CloseHandle(handle)
    values = data["values"]
    if (data["initialized"] != 1 or len(values) != 512 or
            not all(type(v) is int and -(1 << 31) <= v < (1 << 31) for v in values)
            or not any(values)):
        raise ValueError("Invalid runtime cosine table")
    result = dict(schemaVersion=1, exeSha256=EXPECTED, tableVa="00A52EE8",
                  initializedVa="00A536E8", initialized=1, values=values)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    # Never overwrite an existing capture.
    with args.output.open("x", encoding="utf-8") as file:
        json.dump(result, file, indent=2)
        file.write("\n")
    print(args.output)

if __name__ == "__main__":
    main()
