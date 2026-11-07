"""Regression tests for suspended startup capture; no Joey or Frida required."""
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


class StartupLauncherTests(unittest.TestCase):
    def test_default_delegates_to_original_attach_launcher(self):
        with patch.object(native_rules.legacy, "main", return_value=17) as main:
            self.assertEqual(native_rules.main(["--observe-only"]), 17)
            main.assert_called_once_with(["--observe-only"])

    def test_spawn_requires_observer_and_exe_and_rejects_pid(self):
        cases = [
            ["--spawn", "--observe-only"],
            ["--spawn", "--exe", "joey_pc.exe"],
            ["--spawn", "--exe", "joey_pc.exe", "--observe-only", "--pid", "123"],
            ["--spawn", "--exe", "joey_pc.exe", "--observe-only",
             "--speed-value", "0"],
            ["--spawn", "--exe", "joey_pc.exe", "--observe-only",
             "--speed-value", "4294967296"],
            ["--spawn", "--exe", "joey_pc.exe", "--observe-only",
             "--speed-value", "-1"],
        ]
        for argv in cases:
            with self.subTest(argv=argv), self.assertRaises(SystemExit):
                with redirect_stdout(io.StringIO()):
                    native_rules.main(argv)

    def test_pre_resume_hooks_then_resume(self):
        events = []
        with tempfile.TemporaryDirectory() as directory:
            exe = Path(directory) / "joey_pc.exe"
            exe.write_bytes(b"synthetic fixture")
            deck_file = Path(directory) / "deck.json"
            deck_file.write_text('{"main": [' + ",".join(["42"] * 40) + ']}',
                                 encoding="utf-8")
            class Api:
                def bootstrapinfo(self):
                    events.append("bootstrapinfo")
                    return {"modulePath": str(exe), "processId": 321}
                def armstartupobserver(self):
                    events.append("armstartupobserver")
                def tracestart(self, capacity):
                    events.append(("tracestart", capacity))
                def tracedemostart(self):
                    events.append("tracedemostart")
                def tracemark(self, label):
                    events.append(("tracemark", label))
                def setdemodecks(self, plans):
                    events.append(("setdemodecks", plans))
                    return {"configured": [True, False]}
                def armmultiinstance(self):
                    events.append("armmultiinstance")
                    return {"armed": True, "remapped": 0}
                def armspeedoverride(self, value):
                    events.append(("armspeedoverride", value))
                    return {"armed": True, "requested": value}
                def tracestatus(self):
                    events.append("tracestatus")
                    return {"active": True, "retained": 0, "hasUnsaved": False}
                def tracestop(self):
                    events.append("tracestop")
                def restore(self):
                    events.append("restore")
            api = Api()
            class Script:
                exports_sync = api
                def on(self, *args):
                    events.append("on")
                def load(self):
                    events.append("load")
                def unload(self):
                    events.append("unload")
            class Session:
                def create_script(self, source):
                    events.append("create_script")
                    self_source = source
                    if "armstartupobserver" not in self_source:
                        raise AssertionError("Startup bootstrap RPC missing")
                    return Script()
                def detach(self):
                    events.append("detach")
            class Device:
                def spawn(self, program, *, cwd):
                    self.program = program
                    self.cwd = cwd
                    events.append("spawn")
                    return 321
                def attach(self, pid):
                    events.append("attach")
                    return Session()
                def resume(self, pid):
                    events.append("resume")
                def kill(self, pid):
                    events.append("kill")
            device = Device()
            fake_frida = types.SimpleNamespace(get_local_device=lambda: device)
            with patch.dict(sys.modules, {"frida": fake_frida}), \
                 patch.object(native_rules, "probe",
                              return_value={"passed": True, "checks": {}}), \
                 patch("builtins.input", return_value="stop"), \
                 redirect_stdout(io.StringIO()):
                code = native_rules.main(
                    ["--spawn", "--exe", str(exe), "--observe-only",
                     "--speed-value", "1000", "--allow-multi-instance",
                      "--deck-0", str(deck_file)])
            self.assertEqual(code, 0)
            self.assertEqual(device.program, [str(exe), "-speedy"])
            self.assertEqual(device.cwd, str(exe.parent))
            ordered = ["spawn", "attach", "create_script", "load",
                       "bootstrapinfo", "armstartupobserver",
                       ("tracestart", 20000), "tracedemostart",
                       ("tracemark", "startup-hooks-armed"),
                       ("setdemodecks", {"0": {"main": [42] * 40,
                                              "second": [], "third": []}}),
                       "armmultiinstance", ("armspeedoverride", 1000), "resume"]
            self.assertEqual([events.index(entry) for entry in ordered],
                             sorted(events.index(entry) for entry in ordered))
            self.assertIn("restore", events)
            self.assertNotIn("kill", events)

    def test_failed_attach_kills_only_suspended_process(self):
        events = []
        class Device:
            def spawn(self, program, *, cwd):
                events.append("spawn")
                return 998
            def attach(self, pid):
                events.append("attach")
                raise RuntimeError("Injected failure")
            def kill(self, pid):
                events.append(("kill", pid))
        fake_frida = types.SimpleNamespace(
            get_local_device=lambda: Device())
        with patch.dict(sys.modules, {"frida": fake_frida}), \
             redirect_stdout(io.StringIO()):
            result = native_rules.run_spawn(Path("C:/joey_pc.exe"), 1)
        self.assertEqual(result, 2)
        self.assertEqual(events, ["spawn", "attach", ("kill", 998)])


if __name__ == "__main__":
    unittest.main()
