"""Batch capture tests: synthetic Frida/device; does not execute Windows Joey."""
from __future__ import annotations

from contextlib import redirect_stdout, redirect_stderr
import io
from pathlib import Path
import sys
import tempfile
import types
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import native_rules


class CaptureRotationTests(unittest.TestCase):
    def _run_synthetic_cycles(self, cycles: int, fail_export: bool = False):
        events = []
        with tempfile.TemporaryDirectory() as directory:
            exe = Path(directory, "joey_pc.exe")
            exe.write_bytes(b"synthetic test fixture; preflight is mocked")

            class Api:
                def __init__(self, pid):
                    self.pid = pid

                def bootstrapinfo(self):
                    events.append(("bootstrapinfo", self.pid))
                    return {"modulePath": str(exe), "processId": self.pid}

                def armstartupobserver(self):
                    events.append(("arm", self.pid))

                def tracestart(self, capacity):
                    events.append(("start", self.pid, capacity))

                def tracedemostart(self):
                    events.append(("demo", self.pid))

                def tracemark(self, marker):
                    events.append(("mark", self.pid, marker))

                def armmultiinstance(self):
                    events.append(("multi", self.pid))
                    return {"armed": True, "remapped": 0}

                def armspeedoverride(self, value):
                    events.append(("speed", self.pid, value))
                    return {"armed": True}

                def tracestatus(self):
                    events.append(("status", self.pid))
                    # Already past threshold, but still below buffer capacity.
                    return {"overwritten": 0, "seen": 12050,
                            "hasUnsaved": True, "retained": 12050,
                            "active": True}

                def tracestop(self):
                    events.append(("stop", self.pid))

                def restore(self):
                    events.append(("restore", self.pid))

            class Script:
                def __init__(self, pid):
                    self.pid = pid
                    self.exports_sync = Api(pid)

                def on(self, *args):
                    pass

                def load(self):
                    events.append(("load", self.pid))

                def unload(self):
                    events.append(("unload", self.pid))

            class Session:
                def __init__(self, pid):
                    self.pid = pid

                def create_script(self, source):
                    self.script = Script(self.pid)
                    return self.script

                def detach(self):
                    events.append(("detach", self.pid))

            class Device:
                def __init__(self):
                    self.next_pid = 4100

                def spawn(self, argv, *, cwd):
                    self.next_pid += 1
                    pid = self.next_pid
                    events.append(("spawn", pid, list(argv)))
                    return pid

                def attach(self, pid):
                    events.append(("attach", pid))
                    return Session(pid)

                def resume(self, pid):
                    events.append(("resume", pid))

                def kill(self, pid):
                    events.append(("kill", pid))

            def save(api):
                events.append(("save", api.pid))
                if fail_export:
                    raise OSError("disk full")
                return Path(directory) / f"capture-{api.pid}.jsonl"

            fake_frida = types.SimpleNamespace(get_local_device=Device)
            # Use a real device instance so process IDs increase between cycles.
            device = Device()
            fake_frida.get_local_device = lambda: device
            output = io.StringIO()
            with patch.dict(sys.modules, {"frida": fake_frida}), \
                 patch.object(native_rules, "probe",
                              return_value={"passed": True, "checks": {}}), \
                 patch.object(native_rules.legacy, "save_command_trace",
                              side_effect=save), \
                 redirect_stdout(output):
                result = native_rules.run_batch(
                    exe, console_side=1, speed_value=60000,
                    rotate_events=12000, cycles=cycles,
                    allow_multi_instance=True)
            self.last_output = output.getvalue()
        return result, events

    def test_event_progress_percentage_and_finite_total(self):
        line = native_rules.batch_progress_line(7500, 15000, 2, 5)
        self.assertIn("cycle 2/5: 50.0% (7500/15000 events)", line)
        self.assertIn("total 30.0%", line)
        self.assertIn("cycle 1/unlimited", native_rules.batch_progress_line(0, 15000, 1, 0))
        self.assertIn("100.0%", native_rules.batch_progress_line(22000, 15000, 1, 1))
        self.assertNotIn("110.0%", native_rules.batch_progress_line(22000, 15000, 1, 1))

    def test_two_cycles_save_and_detach_before_killing_own_pid(self):
        result, events = self._run_synthetic_cycles(2)
        self.assertEqual(result, 0)
        self.assertIn("cycle 1/2: 100.0%", self.last_output)
        self.assertIn("total 50.0%", self.last_output)
        self.assertIn("cycle 2/2: 100.0%", self.last_output)
        self.assertIn("total 100.0%", self.last_output)
        self.assertEqual([e[1] for e in events if e[0] == "spawn"],
                         [4101, 4102])
        self.assertEqual([e[1] for e in events if e[0] == "kill"],
                         [4101, 4102])
        for pid in (4101, 4102):
            ids = [e[0] for e in events if e[1] == pid]
            for left, right in [("arm", "resume"), ("start", "resume"),
                                ("multi", "resume"), ("speed", "resume"),
                                ("resume", "stop"),
                                ("stop", "save"), ("save", "restore"),
                                ("restore", "unload"), ("unload", "detach"),
                                ("detach", "kill")]:
                self.assertLess(ids.index(left), ids.index(right))

    def test_export_failure_stops_batch_and_does_not_start_second_joey(self):
        result, events = self._run_synthetic_cycles(2, fail_export=True)
        self.assertEqual(result, 2)
        self.assertEqual(len([e for e in events if e[0] == "spawn"]), 1)
        self.assertEqual(len([e for e in events if e[0] == "kill"]), 1)

    def test_reject_too_late_threshold_or_cycles_without_rotation(self):
        invalid = [
            ["--rotate-events", "20000"],
            ["--rotate-events", "0"],
            ["--rotate-events", "19001"],
            ["--cycles", "2"],
            ["--rotate-events", "12000", "--cycles", "-1"],
        ]
        for extra in invalid:
            args = ["--spawn", "--observe-only", "--exe", "joey_pc.exe", *extra]
            parser = native_rules.build_parser()
            parsed = parser.parse_args(args)
            with self.subTest(extra=extra), \
                 redirect_stderr(io.StringIO()), \
                 self.assertRaises(SystemExit):
                native_rules.preflight_spawn(parsed, parser)


if __name__ == "__main__":
    unittest.main()
