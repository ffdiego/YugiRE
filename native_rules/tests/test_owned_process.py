"""Verify stable Win32 HANDLE lifecycle without touching real processes."""
from __future__ import annotations

import ctypes
from pathlib import Path
from types import SimpleNamespace
import sys
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import owned_process


class Function:
    """ctypes-style function that supports argtypes and restype attributes."""
    def __init__(self, callback):
        self.callback = callback
        self.argtypes = None
        self.restype = None

    def __call__(self, *args):
        return self.callback(*args)


class Kernel32Fake:
    def __init__(self, already_exited=False, terminate_succeeds=True,
                 never_exits=False):
        self.exited = already_exited
        self.terminate_succeeds = terminate_succeeds
        self.never_exits = never_exits
        self.calls = []
        self.OpenProcess = Function(self.open_process)
        self.WaitForSingleObject = Function(self.wait_for_single_object)
        self.TerminateProcess = Function(self.terminate_process)
        self.CloseHandle = Function(self.close_handle)

    def open_process(self, rights, inherit, pid):
        self.calls.append(("open", rights, inherit, pid))
        return 912345  # Stable synthetic HANDLE, distinct from the PID.

    def wait_for_single_object(self, handle, timeout):
        self.calls.append(("wait", handle, timeout))
        return (owned_process.WAIT_OBJECT_0 if self.exited
                else owned_process.WAIT_TIMEOUT)

    def terminate_process(self, handle, exit_code):
        self.calls.append(("terminate", handle, exit_code))
        if self.terminate_succeeds and not self.never_exits:
            self.exited = True
        return self.terminate_succeeds

    def close_handle(self, handle):
        self.calls.append(("close", handle))
        return True


class OwnedHandleTests(unittest.TestCase):
    def create(self, dll):
        with patch.object(owned_process.os, "name", "nt"), \
             patch.object(ctypes, "WinDLL", create=True, return_value=dll):
            return owned_process.acquire_owned_process(53210)

    def test_terminate_wait_and_close_refers_to_original_handle(self):
        dll = Kernel32Fake()
        handle = self.create(dll)
        self.assertTrue(handle.terminate_and_verify(seconds=3.0))
        handle.close()
        self.assertEqual([entry for entry in dll.calls if entry[0] == "terminate"],
                         [("terminate", 912345, 1)])
        self.assertEqual([entry for entry in dll.calls if entry[0] == "close"],
                         [("close", 912345)])
        self.assertEqual(dll.calls[0][3], 53210)
        self.assertIn(("wait", 912345, 3000), dll.calls)

    def test_already_exited_does_not_terminate_reused_pid(self):
        dll = Kernel32Fake(already_exited=True)
        handle = self.create(dll)
        self.assertTrue(handle.terminate_and_verify())
        self.assertFalse(any(call[0] == "terminate" for call in dll.calls))
        handle.close()

    def test_does_not_claim_exit_on_wait_timeout(self):
        dll = Kernel32Fake(never_exits=True)
        handle = self.create(dll)
        self.assertFalse(handle.terminate_and_verify(seconds=0.1))
        self.assertIn(("wait", 912345, 100), dll.calls)
        handle.close()

    def test_closing_handle_twice_is_safe(self):
        dll = Kernel32Fake()
        handle = self.create(dll)
        handle.close()
        handle.close()
        self.assertEqual(sum(call[0] == "close" for call in dll.calls), 1)

    def test_taskkill_fallback_checks_exit_of_original_handle(self):
        dll = Kernel32Fake(terminate_succeeds=False)
        handle = self.create(dll)

        def fake_taskkill(cmd, **kwargs):
            self.assertEqual(cmd[1:], ["/PID", "53210", "/F"])
            # Emulate Windows taskkill causing the original HANDLE to signal.
            dll.exited = True
            return SimpleNamespace(returncode=0, stdout="SUCCESS", stderr="")

        with patch.object(owned_process.subprocess, "run",
                          side_effect=fake_taskkill) as run:
            exited, detail = handle.taskkill_and_verify(seconds=0.5)
        self.assertTrue(exited)
        self.assertIn("exitCode=0", detail)
        self.assertEqual(run.call_count, 1)
        self.assertIn(("wait", 912345, 500), dll.calls)
        handle.close()

    def test_taskkill_failure_does_not_fake_process_exit(self):
        dll = Kernel32Fake(never_exits=True)
        handle = self.create(dll)
        with patch.object(owned_process.subprocess, "run",
                          return_value=SimpleNamespace(
                              returncode=5, stdout="", stderr="Access denied")):
            exited, detail = handle.taskkill_and_verify(seconds=0.1)
        self.assertFalse(exited)
        self.assertIn("Access denied", detail)
        handle.close()


if __name__ == "__main__":
    unittest.main()
