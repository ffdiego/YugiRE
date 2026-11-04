"""Pure Python console parser regression tests (no Joey or Frida process)."""
from __future__ import annotations

import io
from contextlib import redirect_stdout
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from patch_dual_rules import handle_command


class FakeApi:
    def __init__(self):
        self.draws = []
        self.cancels = 0
        self.reads = 0

    def status(self):
        self.reads += 1
        return {"entries": [], "console": {
            "batchesQueued": 0, "pending": None, "last": None, "gate": None
        }}

    def queuedraw(self, side, count):
        self.draws.append((side, count))
        return {"id": len(self.draws), "side": side, "count": count, "state": "pending"}

    def canceldraw(self):
        self.cancels += 1
        return {"state": "cancelled"}


class ConsoleCommandTests(unittest.TestCase):
    def setUp(self):
        self.api = FakeApi()

    def invoke(self, text: str, side: int = 1) -> bool:
        with redirect_stdout(io.StringIO()):
            return handle_command(text, self.api, side)

    def test_draw_five_defaults_to_player_side_one(self):
        self.assertTrue(self.invoke("draw 5"))
        self.assertEqual(self.api.draws, [(1, 5)])

    def test_explicit_cpu_side_and_default_override(self):
        self.assertTrue(self.invoke("draw 5 0"))
        self.assertTrue(self.invoke("draw 5", side=0))
        self.assertEqual(self.api.draws, [(0, 5), (0, 5)])

    def test_reject_non_five_and_bad_side(self):
        for command in ("draw", "draw 1", "draw 6", "draw 5 2", "draw 5 -1",
                        "draw 5 1 0", "draw abc", "wat"):
            self.assertTrue(self.invoke(command))
        self.assertEqual(self.api.draws, [])

    def test_cancel_status_help_stop(self):
        self.assertTrue(self.invoke(""))
        self.assertTrue(self.invoke("status"))
        self.assertTrue(self.invoke("cancel"))
        self.assertTrue(self.invoke("help"))
        self.assertEqual(self.api.reads, 2)
        self.assertEqual(self.api.cancels, 1)
        for command in ("stop", "quit", "exit"):
            self.assertFalse(self.invoke(command))


if __name__ == "__main__":
    unittest.main()
