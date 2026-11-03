"""Regression tests for JoeyTrace console notes and hotkey handling."""
from pathlib import Path
from types import SimpleNamespace
import json
import sys
import tempfile
import unittest

SCRIPTS = Path(__file__).resolve().parents[1] / "scripts"
sys.path.insert(0, str(SCRIPTS))
import joeytrace
from joeytrace_core import TraceSession


class ConsoleInputTests(unittest.TestCase):
    def test_plain_text_attaches_to_previous_marker(self):
        self.assertEqual(
            joeytrace.parse_console_command("Copycat copiou o ATK do Gearfried"),
            ("annotate", "Copycat copiou o ATK do Gearfried"))
        self.assertEqual(
            joeytrace.parse_console_command("note paguei 100 LP"),
            ("annotate", "paguei 100 LP"))

    def test_explicit_new_marker_and_stop(self):
        self.assertEqual(joeytrace.parse_console_command("mark efeito do Momonga"),
                         ("mark", "efeito do Momonga"))
        self.assertEqual(joeytrace.parse_console_command("stop"), ("stop", ""))
        self.assertIsNone(joeytrace.parse_console_command("    "))
        self.assertEqual(joeytrace.parse_console_command("help"), ("help", ""))

    def test_annotate_live_and_report(self):
        with tempfile.TemporaryDirectory() as tmp:
            session = TraceSession(Path(tmp) / "duel", {})
            session.marker(100, source="CTRL+SHIFT+J")
            session.annotate_marker(1, "Messenger of Peace cobrou 100 LP")
            session.annotate_marker(1, "Copycat copiou Gearfried")
            report = session.finish("test_complete", 300)
            original = [json.loads(l) for l in (session.folder / "markers.jsonl").read_text().splitlines()]
            notes = [json.loads(l) for l in (session.folder / "marker-annotations.jsonl").read_text().splitlines()]
            self.assertEqual(original[0]["note"], "")
            self.assertEqual(len(notes), 2)
            self.assertTrue(all(n["marker_index"] == 1 for n in notes))
            self.assertIn("Messenger of Peace", report.read_text(encoding="utf-8"))
            self.assertIn("Copycat", report.read_text(encoding="utf-8"))

    def test_unknown_marker_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            session = TraceSession(Path(tmp) / "duel", {})
            with self.assertRaisesRegex(ValueError, "inexistente"):
                session.annotate_marker(1, "nota")
            session.finish("test_complete", 0)

    def test_hotkeys_are_not_triggered_by_unrelated_keys(self):
        probe = object.__new__(joeytrace.WinProcessProbe)
        down = {0x11, 0x10, 0x4A}
        probe.user32 = SimpleNamespace(
            GetAsyncKeyState=lambda code: 0x8000 if code in down else 0)
        self.assertTrue(probe.hotkey_pressed("ctrl+shift+j"))
        self.assertFalse(probe.hotkey_pressed("f9"))
        down.remove(0x10)
        self.assertFalse(probe.hotkey_pressed("ctrl+shift+j"))
        down.add(0x78)
        self.assertTrue(probe.hotkey_pressed("f9"))


if __name__ == "__main__":
    unittest.main()
