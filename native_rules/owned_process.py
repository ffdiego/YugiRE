"""Own and conclusively terminate the exact Win32 process spawned by Frida.

The OS HANDLE is opened once, immediately after Frida.device.spawn() returns.
Subsequent operations use this persistent handle, never a newly looked-up PID.
This avoids PID-reuse risks and makes exit verification independent of a
destroyed Frida session. No extra Python dependencies.
"""
from __future__ import annotations

import os
from pathlib import Path
import subprocess


PROCESS_TERMINATE = 0x0001
PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
SYNCHRONIZE = 0x00100000
WAIT_OBJECT_0 = 0
WAIT_TIMEOUT = 0x102


class OwnedWindowsProcess:
    def __init__(self, pid: int):
        if os.name != "nt":
            raise OSError("Windows process handles are available only on Windows")
        if not isinstance(pid, int) or pid <= 0:
            raise ValueError("Expected a positive Frida-spawned PID")

        import ctypes
        from ctypes import wintypes

        self._ctypes = ctypes
        dll = ctypes.WinDLL("kernel32", use_last_error=True)
        dll.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
        dll.OpenProcess.restype = wintypes.HANDLE
        dll.WaitForSingleObject.argtypes = [wintypes.HANDLE, wintypes.DWORD]
        dll.WaitForSingleObject.restype = wintypes.DWORD
        dll.TerminateProcess.argtypes = [wintypes.HANDLE, wintypes.UINT]
        dll.TerminateProcess.restype = wintypes.BOOL
        dll.CloseHandle.argtypes = [wintypes.HANDLE]
        dll.CloseHandle.restype = wintypes.BOOL

        rights = PROCESS_TERMINATE | PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE
        handle = dll.OpenProcess(rights, False, pid)
        if not handle:
            raise ctypes.WinError(ctypes.get_last_error())
        self._dll = dll
        self._handle = handle
        self.pid = pid

    def wait(self, seconds: float) -> bool:
        if self._handle is None:
            raise RuntimeError("Process handle already closed")
        milliseconds = max(0, min(0xFFFFFFFE, round(seconds * 1000)))
        status = self._dll.WaitForSingleObject(self._handle, milliseconds)
        if status == WAIT_OBJECT_0:
            return True
        if status == WAIT_TIMEOUT:
            return False
        raise self._ctypes.WinError(self._ctypes.get_last_error())

    def terminate_and_verify(self, seconds: float = 5.0) -> bool:
        """Force-terminate this exact process and wait until it is truly exited.

        A pre-existing exit is a success. We never call TerminateProcess on
        a reused PID because the HANDLE refers to the original process object.
        """
        if self.wait(0):
            return True
        if not self._dll.TerminateProcess(self._handle, 1):
            # Save the error immediately: WaitForSingleObject may change
            # GetLastError on the same thread.
            error_code = self._ctypes.get_last_error()
            if self.wait(0):
                return True
            raise self._ctypes.WinError(error_code)
        return self.wait(seconds)

    def taskkill_and_verify(self, seconds: float = 5.0) -> tuple[bool, str]:
        """Last resort: Windows taskkill, on PID still owned by our live HANDLE.

        PID reuse is impossible while the original process is running. We
        check the original handle both before dispatch and after taskkill.
        """
        if self.wait(0):
            return True, "Already exited"
        root = Path(os.environ.get("SystemRoot", r"C:\Windows"))
        exe = root / "System32" / "taskkill.exe"
        if not exe.is_file():
            exe = Path("taskkill.exe")
        result = subprocess.run(
            [str(exe), "/PID", str(self.pid), "/F"],
            capture_output=True, text=True, errors="replace",
            timeout=10, check=False,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        exited = self.wait(seconds)
        detail = (f"taskkill exitCode={result.returncode}; "
                  f"stdout={result.stdout.strip()!r}; "
                  f"stderr={result.stderr.strip()!r}")
        return exited, detail

    def close(self) -> None:
        if self._handle is not None:
            handle, self._handle = self._handle, None
            if not self._dll.CloseHandle(handle):
                raise self._ctypes.WinError(self._ctypes.get_last_error())


def acquire_owned_process(pid: int) -> OwnedWindowsProcess | None:
    """Return a stable handle on Windows; on other OSs use legacy Frida kill.

    Windows acquisition failure is not silently ignored. The caller handles
    cleanup of a spawned-but-not-yet-resumed process with Frida as fallback.
    """
    return OwnedWindowsProcess(pid) if os.name == "nt" else None
