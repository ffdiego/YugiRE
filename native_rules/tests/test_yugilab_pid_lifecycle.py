"""Verify a supervised duel run closes ONLY the PID returned by Frida.spawn.

Pure mocked Frida. No Windows game, DLL injection or real processes.
"""
from __future__ import annotations

from contextlib import redirect_stdout
import io
from pathlib import Path
import sys
import tempfile
import types
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import native_rules


class OwnedPidLifecycleTests(unittest.TestCase):
    def run_fake(self, should_fail: bool = False, kill_fails: bool = False,
                 use_windows_handle: bool = False,
                 handle_fails: bool = False,
                 taskkill_fails: bool = False,
                 frida_destroyed: bool = False):
        calls = []
        with tempfile.TemporaryDirectory() as work:
            exe = Path(work) / "joey_pc.exe"
            exe.write_bytes(b"fake; real EXE checked by probe")
            pid = 82917

            class Api:
                def bootstrapinfo(self):
                    return {"modulePath": str(exe), "processId": pid}
                def armstartupobserver(self):
                    calls.append("arm")
                def tracestart(self, count):
                    calls.append("tracestart")
                def tracedemostart(self):
                    calls.append("tracedemostart")
                def tracemark(self, marker):
                    calls.append("mark")
                def tracestatus(self):
                    return {"session": 1, "active": True,
                            "seen": 0, "retained": 0, "overwritten": 0}
                def tracestop(self):
                    calls.append("stop")
                    if frida_destroyed:
                        raise RuntimeError("script has been destroyed")
                    return {"session": 1, "active": False,
                            "seen": 0, "retained": 0, "overwritten": 0}
                def restore(self):
                    calls.append("restore")
                    if frida_destroyed:
                        raise RuntimeError("script has been destroyed")

            class Script:
                exports_sync = Api()
                def on(self, *args):
                    pass
                def load(self):
                    calls.append("load")
                def unload(self):
                    calls.append("unload")

            class Session:
                def create_script(self, source):
                    calls.append("create")
                    return Script()
                def detach(self):
                    calls.append("detach")

            class Device:
                def spawn(self, argv, *, cwd):
                    calls.append(("spawn", pid))
                    return pid
                def attach(self, id):
                    self.outer_test_id = id
                    calls.append(("attach", id))
                    return Session()
                def resume(self, id):
                    calls.append(("resume", id))
                def kill(self, id):
                    calls.append(("kill", id))
                    if kill_fails:
                        raise RuntimeError("device kill failed")

            class Capture:
                def __init__(self, api, id, target, plans, speed):
                    calls.append(("capture", id, target))
                    self.path = Path(work) / "duels.jsonl"
                def finish(self, state, target_met):
                    calls.append(("finish", target_met))
                    return {"capturedDuels": 3 if target_met else 2,
                            "eventsWritten": 1000, "complete": target_met}
                def close(self):
                    calls.append("capture_close")

            def collect(capture, poll_seconds):
                calls.append("collect")
                if should_fail:
                    raise RuntimeError("simulated capture error")

            fake_frida = types.SimpleNamespace(
                get_local_device=lambda: Device(),
                shutdown=lambda: calls.append("frida_shutdown"))

            class Handle:
                def wait(self, seconds):
                    calls.append(("wait_handle", seconds))
                    # Simulate a process still running even after Frida
                    # reported a successful kill request.
                    return False
                def terminate_and_verify(self, seconds):
                    calls.append(("terminate_handle", pid))
                    return not handle_fails
                def taskkill_and_verify(self, seconds):
                    calls.append(("taskkill", pid))
                    return (not taskkill_fails, "simulated taskkill")
                def close(self):
                    calls.append(("close_handle", pid))

            def acquire(id):
                calls.append(("acquire_handle", id))
                return Handle() if use_windows_handle else None

            with patch.dict(sys.modules, {"frida": fake_frida}), \
                 patch.object(native_rules, "acquire_owned_process",
                              side_effect=acquire), \
                 patch.object(native_rules, "probe",
                              return_value={"passed": True, "checks": {}}), \
                 patch.object(native_rules, "LiveDuelCapture", Capture), \
                 patch.object(native_rules, "collect_duels", collect), \
                 redirect_stdout(io.StringIO()) as out:
                result = native_rules.run_spawn(exe, 1, target_duels=3)
            printed = out.getvalue()
        return result, calls, printed

    def test_normal_completion_flushes_and_kills_owned_pid(self):
        result, calls, printed = self.run_fake()
        self.assertEqual(result, 0)
        names = [c[0] if isinstance(c, tuple) else c for c in calls]
        for left, right in [
            ("spawn", "attach"),
            ("tracestart", "capture"),
            ("capture", "resume"),
            ("resume", "collect"),
            ("collect", "stop"),
            ("stop", "finish"),
            ("finish", "capture_close"),
            ("capture_close", "restore"),
            ("restore", "kill"),
            ("kill", "frida_shutdown"),
        ]:
            self.assertLess(names.index(left), names.index(right), (left, right))
        self.assertEqual([x for x in calls if isinstance(x, tuple)
                          and x[0] == "kill"], [("kill", 82917)])
        self.assertIn(("finish", True), calls)
        self.assertIn("Kill requested via Frida", printed)
        self.assertNotIn("unload", calls)
        self.assertNotIn("detach", calls)

    def test_collection_failure_still_flushes_partial_and_kills_pid(self):
        result, calls, _ = self.run_fake(should_fail=True)
        self.assertEqual(result, 2)
        self.assertIn(("finish", False), calls)
        self.assertIn(("kill", 82917), calls)

    def test_failed_pid_kill_is_reported_as_error(self):
        result, calls, text = self.run_fake(kill_fails=True)
        self.assertEqual(result, 2)
        self.assertIn(("kill", 82917), calls)
        self.assertIn("Failed to verify termination of owned Joey PID", text)

    def test_windows_handle_is_authoritative_not_frida_pid_lookup(self):
        result, calls, printed = self.run_fake(use_windows_handle=True)
        self.assertEqual(result, 0)
        self.assertIn(("acquire_handle", 82917), calls)
        self.assertIn(("terminate_handle", 82917), calls)
        self.assertIn(("close_handle", 82917), calls)
        self.assertIn(("kill", 82917), calls)
        self.assertIn("closed (verified by Windows process handle)", printed)
        # Frida is asked to kill while attached, BEFORE native fallback.
        self.assertLess(calls.index(("kill", 82917)),
                        calls.index(("terminate_handle", 82917)))
        self.assertNotIn("unload", calls)
        self.assertNotIn("detach", calls)

    def test_windows_handle_failed_exit_cannot_claim_success(self):
        result, calls, printed = self.run_fake(
            use_windows_handle=True, handle_fails=True,
            taskkill_fails=True)
        self.assertEqual(result, 2)
        self.assertIn(("terminate_handle", 82917), calls)
        self.assertIn(("taskkill", 82917), calls)
        self.assertIn(("close_handle", 82917), calls)
        self.assertNotIn("closed (verified", printed)
        self.assertIn("Failed to verify termination", printed)

    def test_taskkill_fallback_verifies_exit_after_direct_access_denied(self):
        result, calls, printed = self.run_fake(
            use_windows_handle=True, handle_fails=True)
        self.assertEqual(result, 0)
        self.assertIn(("kill", 82917), calls)
        self.assertIn(("terminate_handle", 82917), calls)
        self.assertIn(("taskkill", 82917), calls)
        self.assertIn("Method: taskkill confirmed", printed)

    def test_windows_handle_closes_even_if_capture_fails(self):
        result, calls, printed = self.run_fake(
            use_windows_handle=True, should_fail=True)
        self.assertEqual(result, 2)
        self.assertIn(("terminate_handle", 82917), calls)
        self.assertIn(("close_handle", 82917), calls)
        self.assertIn("closed (verified by Windows process handle)", printed)

    def test_destroyed_frida_script_still_closes_original_windows_process(self):
        # Reproduces worker-09 from parallel-usos_27u: session died before
        # the terminal opcode, so trace finalization and restore both fail.
        result, calls, printed = self.run_fake(
            use_windows_handle=True, should_fail=True, frida_destroyed=True)
        self.assertEqual(result, 2)
        self.assertIn("Could not finalize capture: script has been destroyed",
                      printed)
        self.assertIn("Restore error: script has been destroyed", printed)
        self.assertIn(("terminate_handle", 82917), calls)
        self.assertIn(("close_handle", 82917), calls)
        self.assertIn("closed (verified by Windows process handle)", printed)



if __name__ == "__main__":
    unittest.main()
