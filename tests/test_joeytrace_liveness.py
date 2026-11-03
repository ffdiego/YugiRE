"""Offline regression tests for JoeyTrace Windows process-liveness checks."""
from pathlib import Path
from types import SimpleNamespace
from unittest import TestCase, main
from unittest.mock import patch
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
import joeytrace


class ProcessLivenessTests(TestCase):
    @staticmethod
    def make_probe(wait_result):
        probe = object.__new__(joeytrace.WinProcessProbe)
        probe.handle = 123
        probe.kernel32 = SimpleNamespace(WaitForSingleObject=lambda handle, milliseconds: wait_result)
        probe.ctypes = SimpleNamespace(
            get_last_error=lambda: 5,
            WinError=lambda code: OSError(code, "WaitForSingleObject failed"),
        )
        return probe

    def test_running_process_is_alive(self):
        self.assertTrue(self.make_probe(0x00000102).alive())

    def test_terminated_process_is_not_alive(self):
        self.assertFalse(self.make_probe(0x00000000).alive())

    def test_failed_wait_is_an_error_not_a_process_exit(self):
        with self.assertRaisesRegex(OSError, "WaitForSingleObject failed"):
            self.make_probe(0xFFFFFFFF).alive()

    def test_missing_handle_is_not_alive(self):
        p = self.make_probe(0x00000102)
        p.handle = None
        self.assertFalse(p.alive())

    def test_attach_requests_synchronize_permission(self):
        p = object.__new__(joeytrace.WinProcessProbe)
        p.handle = None
        p._main_module = lambda pid: (0x00400000, Path("joey_pc.exe"))
        accesses = []
        p.kernel32 = SimpleNamespace(
            OpenProcess=lambda access, inherit, pid: accesses.append(access) or 123
        )
        with patch.object(joeytrace, "_sha256_file", return_value=joeytrace.EXPECTED_EXE_SHA256):
            p.attach(8344)
        self.assertEqual(p.pid, 8344)
        self.assertTrue(accesses[0] & 0x00100000)
        self.assertTrue(accesses[0] & 0x0010)


if __name__ == "__main__":
    main()
