"""Offline unit tests of live duel collection without a real Frida process."""
from __future__ import annotations

from contextlib import redirect_stdout
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from duel_capture import LiveDuelCapture, collect_duels, enrich


def command(seq: int, opcode: int) -> dict:
    return {"kind": "command", "seq": seq, "timestampMs": 1000 + seq,
            "caller": "0x5bc545", "args": [opcode, 0, 0, 0]}


class FakeLiveAgent:
    def __init__(self, events: list[dict], duels: int):
        self.events = events.copy()
        self.all_events = events.copy()
        self.session = 1
        self.seen = len(events)
        self.duels = duels
        self.drained = 0
        self.dropped = 0
        self.active = True
        self.acked_final = False

    def tracestatus(self):
        return {"session": self.session, "active": self.active,
                "seen": self.seen, "retained": len(self.events),
                "overwritten": self.dropped, "drained": self.drained,
                "completedDuels": self.duels}

    def tracelivepeek(self, count=256):
        assert self.active
        return {"session": self.session, "events": self.events[:count]}

    def traceliveack(self, session, first, last):
        assert session == self.session
        expected = list(range(first, last + 1))
        assert [e["seq"] for e in self.events[:len(expected)]] == expected
        self.events = self.events[len(expected):]
        self.drained += len(expected)
        return {"session": session, "acknowledged": len(expected)}

    def tracestop(self):
        self.active = False
        return self.tracestatus()

    def traceevents(self, offset, count):
        assert not self.active
        return self.events[offset:offset + count]

    def traceacknowledge(self, session, seen):
        assert session == self.session and seen == self.seen
        self.acked_final = True


class LiveDuelCaptureTests(unittest.TestCase):
    def test_live_drain_then_finalize_preserves_exact_event_order(self):
        events = [command(i, 0x0004 if i in (13, 45, 74) else 0x0001)
                  for i in range(1, 75)]
        agent = FakeLiveAgent(events[:50], 2)
        with tempfile.TemporaryDirectory() as directory:
            capture = LiveDuelCapture(agent, 4001, 3, {"0": {"main": [1] * 40}},
                                      2400, Path(directory))
            try:
                live_status = capture.drain()
                self.assertEqual(live_status["drained"], 50)
                self.assertEqual(live_status["retained"], 0)
                # New events are appended to the native agent, but the single
                # JSONL file keeps seq values continuous.
                agent.events = events[50:]
                agent.seen = len(events)
                agent.duels = 3
                agent.tracestop()
                summary = capture.finish(agent.tracestatus(), True)
                self.assertTrue(summary["complete"])
                self.assertEqual(summary["capturedDuels"], 3)
                self.assertEqual(summary["eventsWritten"], 74)
                self.assertTrue(agent.acked_final)
            finally:
                capture.close()
            lines = [json.loads(line) for line in capture.path.read_text(
                encoding="utf-8").splitlines()]
            self.assertEqual(lines[0]["kind"], "metadata")
            self.assertEqual(lines[-1]["kind"], "capture_summary")
            self.assertEqual([v["seq"] for v in lines[1:-1]],
                             list(range(1, 75)))
            self.assertEqual([v["opcodeMasked"] for v in lines[1:-1]
                              if v["opcodeMasked"] == "0x0004"],
                             ["0x0004"] * 3)

    def test_detects_noncontiguous_trace_without_marking_complete(self):
        agent = FakeLiveAgent([command(1, 1), command(3, 4)], 1)
        with tempfile.TemporaryDirectory() as directory:
            capture = LiveDuelCapture(agent, 555, 1, {}, None, Path(directory))
            try:
                with self.assertRaisesRegex(RuntimeError, "Non-contiguous"):
                    capture.drain()
            finally:
                capture.close()
            content = capture.path.read_text(encoding="utf-8")
            self.assertNotIn('"complete": true', content)

    def test_detects_overwrite_even_when_events_retained(self):
        agent = FakeLiveAgent([command(1, 4)], 1)
        agent.dropped = 1
        with tempfile.TemporaryDirectory() as directory:
            capture = LiveDuelCapture(agent, 556, 1, {}, None, Path(directory))
            try:
                with self.assertRaisesRegex(RuntimeError, "LOST"):
                    capture.drain()
            finally:
                capture.close()

    def test_collect_duels_stops_on_target(self):
        agent = FakeLiveAgent([command(i, 4) for i in range(1, 4)], 3)
        with tempfile.TemporaryDirectory() as directory:
            capture = LiveDuelCapture(agent, 557, 3, {}, 2400, Path(directory))
            try:
                with redirect_stdout(io.StringIO()) as output:
                    collect_duels(capture, poll_seconds=0.01, idle_seconds=1)
                self.assertIn("3/3 duels (100.0%)", output.getvalue())
                self.assertEqual(capture.written, 3)
            finally:
                capture.close()

    def test_cancel_before_draining_preserves_partial_capture(self):
        agent = FakeLiveAgent([command(1, 1)], 0)
        with tempfile.TemporaryDirectory() as directory:
            capture = LiveDuelCapture(agent, 558, 3, {}, 2400, Path(directory))
            try:
                with self.assertRaises(KeyboardInterrupt):
                    collect_duels(capture, should_cancel=lambda: True)
                # The launcher owns stop + finalize; this function must not
                # silently claim completion when a coordinator cancels it.
                self.assertFalse(capture.finished)
                self.assertEqual(capture.written, 0)
            finally:
                capture.close()

    def test_terminal_opcode_masks_bit15(self):
        self.assertEqual(enrich(command(1, 0x8004))["opcodeMasked"], "0x0004")


if __name__ == "__main__":
    unittest.main()
