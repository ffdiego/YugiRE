"""Pure Python console parser regression tests (no Joey or Frida process)."""
from __future__ import annotations

import io
from contextlib import redirect_stdout
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from patch_dual_rules import handle_command, save_command_trace


class FakeApi:
    def __init__(self):
        self.draws = []
        self.damages = []
        self.destroys = []
        self.summons = []
        self.sets = []
        self.snapshot = {"sides": [
            {"side": 0, "lifePoints": 8000, "handCount": 4, "deckCount": 30,
             "graveyardCount": 0, "banishedCount": 0, "terminalFlags": 0,
             "monsterIndices": [0, 0, 0, 0, 0],
              "otherFieldIndices": [0, 0, 0, 0, 0, 0],
              "fieldPositionWords": [0] * 11,
              "handIndices": [123, 456, 789]},
            {"side": 1, "lifePoints": 8000, "handCount": 5, "deckCount": 25,
             "graveyardCount": 0, "banishedCount": 0, "terminalFlags": 0,
             "monsterIndices": [0, 0, 0, 0, 0],
              "otherFieldIndices": [0, 0, 0, 0, 0, 0],
              "fieldPositionWords": [0] * 11,
              "handIndices": [123, 456, 789]},
        ]}
        self.cancels = 0
        self.reads = 0
        self.trace_calls = []
        self.trace_active = False
        self.trace_session = 0
        self.trace_records = []
        self.trace_exported = False
        self.prep_active = False
        self.ai_active = False
        self.demo_active = False

    def status(self):
        self.reads += 1
        return {"entries": [], "console": {
            "batchesQueued": 0, "pending": None, "last": None, "gate": None
        }}

    def queuedraw(self, side, count):
        self.draws.append((side, count))
        return {"id": len(self.draws), "side": side, "count": count, "state": "pending"}

    def queuedamage(self, side, amount):
        self.damages.append((side, amount))
        return {"id": 50, "side": side, "amount": amount, "state": "pending"}

    def queuedestroy(self, side, slot):
        self.destroys.append((side, slot))
        return {"id": 60, "side": side, "slot": slot, "expectedIndex": 727,
                "state": "pending"}

    def queuesummon(self, side, index, check_only):
        self.summons.append((side, index, check_only))
        return {"id": len(self.summons), "side": side, "handIndex": index,
                "expectedIndex": 907, "state": "pending", "checkOnly": check_only}

    def queueset(self, side, index, check_only):
        self.sets.append((side, index, check_only))
        return {"id": len(self.sets), "side": side, "handIndex": index,
                "expectedIndex": 221, "state": "pending", "checkOnly": check_only}

    def tracestatus(self):
        n = len(self.trace_records)
        return {"active": self.trace_active, "prepActive": self.prep_active,
                "aiActive": self.ai_active,
                "demoActive": self.demo_active,
                "session": self.trace_session,
                "producer": "0x5B9170", "capacity": 16, "seen": n,
                "retained": n, "overwritten": 0,
                "firstSeq": 1 if n else None,
                "hasUnsaved": n > 0 and not self.trace_exported}

    def tracestart(self, capacity):
        self.trace_calls.append(("start", capacity))
        self.trace_active = True
        self.trace_session += 1
        self.trace_exported = False
        return self.tracestatus()

    def queueaidecision(self, side, check_only):
        self.trace_calls.append(("ai_decision", side, check_only))
        return {"id": 77, "state": "pending", "side": side, "checkOnly": check_only}

    def traceaistart(self):
        if not self.trace_active:
            raise RuntimeError("Start trace first")
        self.trace_calls.append(("ai_on",))
        self.ai_active = True
        return self.tracestatus()

    def traceaistop(self):
        self.trace_calls.append(("ai_off",))
        self.ai_active = False
        return self.tracestatus()

    def tracedemostart(self):
        if not self.trace_active:
            raise RuntimeError("Start trace first")
        self.trace_calls.append(("demo_on",))
        self.demo_active = True
        return self.tracestatus()

    def tracedemostop(self):
        self.trace_calls.append(("demo_off",))
        self.demo_active = False
        return self.tracestatus()

    def traceprepstart(self):
        if not self.trace_active:
            raise RuntimeError("Start trace first")
        self.trace_calls.append(("prep_on",))
        self.prep_active = True
        return self.tracestatus()

    def traceprepstop(self):
        self.trace_calls.append(("prep_off",))
        self.prep_active = False
        return self.tracestatus()

    def tracestop(self):
        self.trace_calls.append(("stop",))
        self.trace_active = False
        self.prep_active = False
        self.ai_active = False
        self.demo_active = False
        return self.tracestatus()

    def tracemark(self, label):
        self.trace_calls.append(("mark", label))
        self.trace_records.append({"kind": "marker", "seq": len(self.trace_records) + 1,
                                   "timestampMs": 1000, "label": label})
        return self.tracestatus()

    def traceevents(self, offset, count):
        self.trace_calls.append(("events", offset, count))
        return self.trace_records[offset:offset+count]

    def traceacknowledge(self, session, seen):
        self.trace_calls.append(("ack", session, seen))
        assert session == self.trace_session
        assert seen == len(self.trace_records)
        self.trace_exported = True
        return self.tracestatus()

    def duelstate(self):
        import copy
        return copy.deepcopy(self.snapshot)

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

    def test_damage_requires_amount_and_explicit_side(self):
        self.assertTrue(self.invoke("damage 1200 0"))
        self.assertTrue(self.invoke("damage 500 1"))
        self.assertEqual(self.api.damages, [(0, 1200), (1, 500)])
        for command in ("damage", "damage 100", "damage 0 0",
                        "damage 10000 0", "damage 1 2", "damage 3.5 0",
                        "damage x 1", "damage 200 0 1"):
            self.assertTrue(self.invoke(command))
        self.assertEqual(len(self.api.damages), 2)

    def test_destroy_requires_explicit_side_and_valid_slot(self):
        self.assertTrue(self.invoke("destroy 0 3"))
        self.assertTrue(self.invoke("destroy 1 0"))
        self.assertEqual(self.api.destroys, [(0, 3), (1, 0)])
        for command in ("destroy", "destroy 0", "destroy 3 0", "destroy 0 5",
                        "destroy 1 -1", "destroy 1 1 1", "destroy x 0",
                        "destroy 0 1.5"):
            self.assertTrue(self.invoke(command))
        self.assertEqual(len(self.api.destroys), 2)

    def test_summon_check_and_execute_parse(self):
        self.assertTrue(self.invoke("summon check 1 4"))
        self.assertTrue(self.invoke("summon 1 3"))
        self.assertEqual(self.api.summons, [(1, 4, True), (1, 3, False)])

    def test_summon_reject_bad_sides_and_arguments(self):
        for command in ("summon", "summon check", "summon 0 4",
                        "summon 1 80", "summon 1 -1", "summon 1 3.5",
                        "summon check 0 4", "summon check 1 abc",
                        "summon check 1 4 extra", "summon 1 4 extra",
                        "summon 2 0", "summon 1"):
            self.assertTrue(self.invoke(command))
        self.assertEqual(self.api.summons, [])

    def test_set_check_and_execute_parse(self):
        self.assertTrue(self.invoke("set check 1 4"))
        self.assertTrue(self.invoke("set 1 3"))
        self.assertEqual(self.api.sets, [(1, 4, True), (1, 3, False)])
        self.assertEqual(self.api.summons, [])

    def test_set_reject_bad_sides_and_arguments(self):
        for command in ("set", "set check", "set 0 4", "set 1 80",
                        "set 1 -1", "set 1 3.5", "set check 0 4",
                        "set check 1 abc", "set check 1 4 extra",
                        "set 1 4 extra", "set 2 0", "set 1"):
            self.assertTrue(self.invoke(command))
        self.assertEqual(self.api.sets, [])

    def test_set_missing_hand_card_is_reported(self):
        class MissingHand(FakeApi):
            def queueset(self, side, index, check_only):
                raise RuntimeError("Error: Selected hand index is not present")
        with redirect_stdout(io.StringIO()) as output:
            self.assertTrue(handle_command("set 1 5", MissingHand(), 1))
        self.assertIn("Cannot select hand index", output.getvalue())

    def test_set_and_summon_can_be_staged_in_independent_calls(self):
        self.assertTrue(self.invoke("set check 1 2"))
        self.assertTrue(self.invoke("summon check 1 2"))
        self.assertEqual(self.api.sets, [(1, 2, True)])
        self.assertEqual(self.api.summons, [(1, 2, True)])

    def test_summon_missing_hand_card_is_reported(self):
        class MissingHand(FakeApi):
            def queuesummon(self, side, index, check_only):
                raise RuntimeError("Error: Selected hand index is not present")
        with redirect_stdout(io.StringIO()) as output:
            self.assertTrue(handle_command("summon 1 5", MissingHand(), 1))
        self.assertIn("Cannot select hand index", output.getvalue())

    def test_destroy_empty_slot_is_reported_without_rpc_traceback(self):
        class EmptyApi(FakeApi):
            def queuedestroy(self, side, slot):
                raise RuntimeError("Error: Target monster slot is empty\\n at queueConsoleDestroy")
        with redirect_stdout(io.StringIO()) as output:
            self.assertTrue(handle_command("destroy 0 4", EmptyApi(), 1))
        self.assertIn("is empty; nothing was queued", output.getvalue())

    def test_state_and_diff_baseline(self):
        cache = {}
        with redirect_stdout(io.StringIO()) as stdout:
            self.assertTrue(handle_command("state", self.api, 1, cache))
        self.assertIn("LP=8000", stdout.getvalue())
        self.assertEqual(cache["previous"]["sides"][0]["deckCount"], 30)
        self.api.snapshot["sides"][1]["lifePoints"] = 7300
        self.api.snapshot["sides"][1]["handCount"] = 8
        with redirect_stdout(io.StringIO()) as stdout:
            self.assertTrue(handle_command("diff", self.api, 1, cache))
        self.assertIn("lifePoints: 8000 -> 7300", stdout.getvalue())
        self.assertIn("handCount: 5 -> 8", stdout.getvalue())
        with redirect_stdout(io.StringIO()) as stdout:
            self.assertTrue(handle_command("diff", self.api, 1, cache))
        self.assertIn("No changes", stdout.getvalue())

    def test_other_field_and_hand_observation(self):
        with redirect_stdout(io.StringIO()) as output:
            handle_command("state", self.api, 1, {})
        self.assertIn("slots 5..9", output.getvalue())
        with redirect_stdout(io.StringIO()) as output:
            handle_command("hand", self.api, 1)
        self.assertIn("side 1", output.getvalue())
        self.assertIn("123", output.getvalue())
        cache = {}
        with redirect_stdout(io.StringIO()):
            handle_command("state", self.api, 1, cache)
        self.api.snapshot["sides"][0]["otherFieldIndices"][5] = 900
        self.api.snapshot["sides"][0]["fieldPositionWords"][10] = 2
        self.api.snapshot["sides"][1]["handIndices"][0] = 902
        with redirect_stdout(io.StringIO()) as output:
            handle_command("diff", self.api, 1, cache)
        self.assertIn("field slot 10: - -> 900", output.getvalue())
        self.assertIn("field slot 10 position", output.getvalue())
        self.assertIn("handIndices", output.getvalue())

    def test_watch_command_dispatch(self):
        class FakeWatcher:
            interval_ms = 250
            active = False
            def running(self):
                return self.active
            def start(self, interval_ms=250):
                self.active = True
                self.interval_ms = interval_ms
            def stop(self):
                self.active = False
        watcher = FakeWatcher()
        with redirect_stdout(io.StringIO()):
            handle_command("watch on 500", self.api, 1, watcher=watcher)
        self.assertTrue(watcher.active)
        self.assertEqual(watcher.interval_ms, 500)
        with redirect_stdout(io.StringIO()):
            handle_command("watch off", self.api, 1, watcher=watcher)
        self.assertFalse(watcher.active)
        with redirect_stdout(io.StringIO()):
            handle_command("watch on 50", self.api, 1, watcher=watcher)
        self.assertFalse(watcher.active)

    def test_ai_native_check_and_one_step_commands(self):
        with redirect_stdout(io.StringIO()) as output:
            self.assertTrue(handle_command("ai check 1", self.api, 1))
            self.assertTrue(handle_command("ai step 1", self.api, 1))
            self.assertTrue(handle_command("ai step 0", self.api, 1))
        self.assertEqual([x for x in self.api.trace_calls if x[0] == "ai_decision"],
                         [("ai_decision", 1, True), ("ai_decision", 1, False)])
        self.assertIn("Usage: ai check 1", output.getvalue())

    def test_ai_console_decisions_are_exported_without_hook_callbacks(self):
        from tempfile import TemporaryDirectory
        import json
        self.api.trace_session = 1
        self.api.trace_records = [
            {"kind": "ai_console", "origin": "yugire_console_native_call",
             "requestId": 3, "side": 1, "checkOnly": True,
             "phase": "start", "handCountAtRequest": 6},
            {"kind": "ai_console", "requestId": 3, "side": 1,
             "phase": "select_enter", "selectorPass": 0},
            {"kind": "ai_console", "requestId": 3, "side": 1,
             "phase": "select_leave", "selectorPass": 0, "selectedHandIndex": 1},
            {"kind": "ai_console", "requestId": 3, "side": 1,
             "phase": "decision", "handIndex": 1, "internalIndex": 221,
             "selectedKind": "summon", "allowed": True, "state": "checked"},
        ]
        with TemporaryDirectory() as directory:
            path = Path(directory) / "ai-console.jsonl"
            with redirect_stdout(io.StringIO()) as output:
                save_command_trace(self.api, path)
            records = [json.loads(row) for row in path.read_text(encoding="utf-8").splitlines()]
        self.assertEqual(len(records), 5)
        self.assertEqual(records[-1]["internalIndex"], 221)
        self.assertEqual(records[-2]["selectedHandIndex"], 1)
        self.assertIn("Console decision records: ", output.getvalue())
        self.assertIn("decision=1", output.getvalue())
        self.assertTrue(self.api.trace_exported)

    def test_demo_lifecycle_and_export(self):
        from tempfile import TemporaryDirectory
        import json
        with redirect_stdout(io.StringIO()) as status:
            self.assertTrue(handle_command("trace on 20000", self.api, 1))
            self.assertTrue(handle_command("trace ai on", self.api, 1))
            self.assertTrue(handle_command("trace demo on", self.api, 1))
            self.assertTrue(handle_command("trace demo off", self.api, 1))
            self.assertTrue(handle_command("trace demo on", self.api, 1))
        self.assertTrue(self.api.demo_active)
        self.assertIn("demoActive=True", status.getvalue())
        self.api.trace_records = [
            {"kind": "demo_executor", "phase": "enter", "callId": 1,
             "side": 0, "sideRaw": 0, "handIndex": 2,
             "handInternalIndex": 221, "modeSelector": 0},
            {"kind": "demo_executor", "phase": "leave", "callId": 1,
             "side": 0, "returnRaw": 0},
            {"kind": "demo_executor", "phase": "enter", "callId": 2,
             "side": 1, "sideRaw": 1, "handIndex": 3,
             "handInternalIndex": 273, "modeSelector": 1}
        ]
        with TemporaryDirectory() as directory:
            target = Path(directory) / "demo.jsonl"
            with redirect_stdout(io.StringIO()) as report:
                save_command_trace(self.api, target)
            rows = [json.loads(line) for line in target.read_text(encoding="utf-8").splitlines()]
        self.assertEqual(len(rows), 4)
        self.assertEqual(rows[1]["handInternalIndex"], 221)
        self.assertEqual(rows[3]["modeSelector"], 1)
        self.assertIn("side0/enter=1", report.getvalue())
        self.assertIn("side1/enter=1", report.getvalue())
        self.assertFalse(self.api.demo_active)
        self.assertFalse(self.api.ai_active)

    def test_trace_ai_probe_captures_native_output_buffers(self):
        from tempfile import TemporaryDirectory
        import json
        with redirect_stdout(io.StringIO()):
            self.assertTrue(handle_command("trace on", self.api, 1))
            self.assertTrue(handle_command("trace ai on", self.api, 1))
        self.assertTrue(self.api.ai_active)
        self.api.trace_records = [
            {"kind": "ai_probe", "phase": "enter", "function": "select_normal",
             "callId": 1, "side": 1, "pass": 0},
            {"kind": "ai_probe", "phase": "leave", "function": "combat_event",
             "callId": 2, "side": 1, "outputHex": "A8096400"},
            {"kind": "ai_probe", "phase": "leave", "function": "computed_stats",
             "callId": 3, "side": 1, "outputHex": "1101060F40060000B0040000"}
        ]
        with TemporaryDirectory() as directory:
            target = Path(directory) / "ai-probe.jsonl"
            with redirect_stdout(io.StringIO()) as stdout:
                save_command_trace(self.api, target)
            rows = [json.loads(line) for line in target.read_text(encoding="utf-8").splitlines()]
        self.assertEqual(rows[2]["outputHex"], "A8096400")
        self.assertEqual(rows[3]["outputHex"], "1101060F40060000B0040000")
        self.assertFalse(self.api.ai_active)
        self.assertTrue(self.api.trace_exported)
        self.assertIn("combat_event/leave=1", stdout.getvalue())

    def test_trace_on_mark_status_off_and_invalid_limit(self):
        for cmd in ("trace on", "trace mark Summon normal",
                    "trace status", "trace off", "trace on 50", "trace off",
                    "trace on 20001", "trace on 0"):
            self.assertTrue(self.invoke(cmd))
        self.assertEqual(self.api.trace_calls[0], ("start", 8192))
        self.assertEqual(self.api.trace_calls[1], ("mark", "Summon normal"))
        self.assertEqual(self.api.trace_calls[2], ("stop",))
        self.assertEqual(self.api.trace_calls[3], ("start", 50))
        self.assertEqual(len([x for x in self.api.trace_calls if x[0] == "start"]), 2)

    def test_prep_probe_console_dispatch(self):
        with redirect_stdout(io.StringIO()):
            self.assertTrue(handle_command("trace on", self.api, 1))
            self.assertTrue(handle_command("trace prep on", self.api, 1))
            self.assertTrue(handle_command("trace status", self.api, 1))
            self.assertTrue(handle_command("trace prep off", self.api, 1))
            self.assertTrue(handle_command("trace prep on", self.api, 1))
            self.assertTrue(handle_command("trace off", self.api, 1))
        self.assertFalse(self.api.prep_active)
        self.assertEqual([x[0] for x in self.api.trace_calls],
                         ["start", "prep_on", "prep_off", "prep_on", "stop"])

    def test_prep_events_are_preserved_by_jsonl_export(self):
        from tempfile import TemporaryDirectory
        import json
        self.api.trace_session = 1
        self.api.trace_records = [
            {"kind": "summon_prep", "phase": "enter", "function": "eligibility",
             "callId": 1, "threadId": 1, "state": {"context": 0x4103}},
            {"kind": "summon_prep", "phase": "leave", "function": "eligibility",
             "callId": 1, "threadId": 1, "returnRaw": 0x11,
             "state": {"context": 0x4103}}
        ]
        with TemporaryDirectory() as directory:
            path = Path(directory) / "prep.jsonl"
            with redirect_stdout(io.StringIO()) as output:
                save_command_trace(self.api, path)
            rows = [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines()]
            self.assertEqual(rows[1]["state"]["context"], 0x4103)
            self.assertEqual(rows[2]["returnRaw"], 0x11)
            self.assertIn("eligibility/enter=1", output.getvalue())
            self.assertTrue(self.api.trace_exported)

    def test_trace_dump_jsonl(self):
        from tempfile import TemporaryDirectory
        import json
        self.api.trace_session = 1
        self.api.trace_records = [
            {"kind": "command", "seq": 1, "timestampMs": 1, "threadId": 7,
             "caller": "0x00578123", "args": [0x8079, 1, 14, 2]},
            {"kind": "marker", "seq": 2, "timestampMs": 2, "label": "set monstro"}
        ]
        with TemporaryDirectory() as directory:
            path = Path(directory) / "commands.jsonl"
            with redirect_stdout(io.StringIO()):
                self.assertEqual(save_command_trace(self.api, path), path)
            rows = [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines()]
            self.assertEqual(rows[0]["kind"], "metadata")
            self.assertEqual(rows[1]["opcodeMasked"], "0x0079")
            self.assertEqual(rows[1]["sideBit15"], 1)
            self.assertEqual(rows[1]["argsHex"][0], "0x00008079")
            self.assertEqual(rows[2]["label"], "set monstro")
            self.assertNotIn("ceContextLow17Hex", rows[1])
            self.assertTrue(self.api.trace_exported)
            with self.assertRaises(FileExistsError):
                save_command_trace(self.api, path)

    def test_ce_decode_only_at_validated_callsite(self):
        from tempfile import TemporaryDirectory
        import json
        self.api.trace_session = 1
        self.api.trace_records = [
            {"kind": "command", "seq": 1, "caller": "0x5abeab",
             "args": [0x200080CE, 0x00D10065, 0, 1]},
            {"kind": "command", "seq": 2, "caller": "0x5abeab",
             "args": [0x408080CE, 0x00D10419, 4, 256]},
            {"kind": "command", "seq": 3, "caller": "0x579984",
             "args": [0x000080CE, 0x00000065, 0, 0]}
        ]
        with TemporaryDirectory() as directory:
            output = Path(directory) / "test.jsonl"
            with redirect_stdout(io.StringIO()):
                save_command_trace(self.api, output)
            data = [json.loads(x) for x in output.read_text(encoding="utf-8").splitlines()]
            self.assertEqual(data[1]["ceContextLow17Hex"], "0x04001")
            self.assertEqual(data[1]["ceResourceIdLow16"], 101)
            self.assertEqual(data[2]["ceContextLow17Hex"], "0x08101")
            self.assertEqual(data[2]["ceResourceIdLow16"], 1049)
            self.assertNotIn("ceContextLow17Hex", data[3])

    def test_native_ce_context_and_cf_card_candidates(self):
        from tempfile import TemporaryDirectory
        import json
        self.api.trace_session = 3
        self.api.trace_records = [
            {"kind": "command", "seq": 1, "caller": "0x5abeab",
             "args": [0x208080CE, 0x00D900AE, 4, 1],
             "ceNativeState": {"context": 0x4101, "source": 907, "stage": 37}},
            {"kind": "command", "seq": 2, "caller": "0x57977f",
             "args": [0x80CF, 0x00D9060B, 2560, 1]},
            {"kind": "command", "seq": 3, "caller": "0x5abeab",
             "args": [0x402080CE, 0x00D90419, 1, 256],
             "ceNativeState": {"context": 0x8041, "source": 273, "stage": 37}}
        ]
        with TemporaryDirectory() as directory:
            output = Path(directory) / "extra-fields.jsonl"
            with redirect_stdout(io.StringIO()):
                save_command_trace(self.api, output)
            rows = [json.loads(x) for x in output.read_text(encoding="utf-8").splitlines()]
            summon, fusion_gate, set_card = rows[1:]
            self.assertEqual(summon["ceHandIndexCandidate"], 4)
            self.assertEqual(summon["ceFieldSlotCandidate"], 0)
            self.assertEqual(summon["ceSourceIndexCandidate"], 907)
            self.assertTrue(summon["ceFlag4000"])
            self.assertFalse(summon["ceFlag8000"])
            self.assertEqual(summon["ceStageGroup"], 1)
            self.assertEqual(fusion_gate["cfResourceIdCandidate"], 1547)
            self.assertNotIn("ceSourceIndexCandidate", fusion_gate)
            self.assertEqual(set_card["ceHandIndexCandidate"], 1)
            self.assertEqual(set_card["ceFieldSlotCandidate"], 0)
            self.assertEqual(set_card["ceSourceIndexCandidate"], 273)
            self.assertFalse(set_card["ceFlag4000"])
            self.assertTrue(set_card["ceFlag8000"])
            self.assertEqual(set_card["ceStageGroup"], 1)

    def test_diff_without_baseline(self):
        cache = {}
        with redirect_stdout(io.StringIO()) as stdout:
            self.assertTrue(handle_command("diff", self.api, 1, cache))
        self.assertIn("No baseline", stdout.getvalue())
        self.assertIn("previous", cache)

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
