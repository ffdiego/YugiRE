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
    def run_fake(self, should_fail: bool = False, kill_fails: bool = False):
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
                    return {"session": 1, "active": False,
                            "seen": 0, "retained": 0, "overwritten": 0}
                def restore(self):
                    calls.append("restore")

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

            fake_frida = types.SimpleNamespace(get_local_device=lambda: Device())
            with patch.dict(sys.modules, {"frida": fake_frida}), \
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
            ("restore", "unload"),
            ("unload", "detach"),
            ("detach", "kill"),
        ]:
            self.assertLess(names.index(left), names.index(right), (left, right))
        self.assertEqual([x for x in calls if isinstance(x, tuple)
                          and x[0] == "kill"], [("kill", 82917)])
        self.assertIn(("finish", True), calls)
        self.assertIn("Owned Joey PID 82917 closed", printed)

    def test_collection_failure_still_flushes_partial_and_kills_pid(self):
        result, calls, _ = self.run_fake(should_fail=True)
        self.assertEqual(result, 2)
        self.assertIn(("finish", False), calls)
        self.assertIn(("kill", 82917), calls)

    def test_failed_pid_kill_is_reported_as_error(self):
        result, calls, text = self.run_fake(kill_fails=True)
        self.assertEqual(result, 2)
        self.assertIn(("kill", 82917), calls)
        self.assertIn("Failed to kill owned Joey PID", text)


if __name__ == "__main__":
    unittest.main()
