"""No Joey, Frida or Windows needed; synthetic export-path regression tests."""
import copy
import json
from pathlib import Path
from tempfile import TemporaryDirectory
import unittest
from zipfile import ZipFile

from yugilab.export_summons import (
    collect_fixtures, export, fixture_from_transition, main,
)
from yugilab.seed_math import derive_duel_seed


def sample_transition():
    side = 1
    hand = [3, 221, 12]
    context = 0x4000 | (1 << 6) | (2 << 1) | side
    return {
        "kind": "summon_transition", "phase": "confirmed",
        "evidenceMethod": "executor_enter_ce_producer_update_leave",
        "executorCallId": 9, "ceProducerSeq": 2,
        "side": side, "handIndex": 1, "sourceCardIndex": 221,
        "fieldSlot": 2, "ceContext": context, "ceStage": 37,
        "ceResourceId": 268, "ticksAfterExecutor": 17,
        "before": {
            "side": side, "handIndices": hand,
            "handWords": [0x1000003, 0x20000DD, 0x300000C],
            "monsters": [1052, 0, 0, 0, 0],
            "opponentMonsters": [887, 0, 0, 0, 0],
            "lifePoints": 8000, "playerFlags": 0,
            "duelPhaseRaw": 5,
        },
        "after": {
            "side": side, "handIndices": [3, 12],
            "handWords": [0x1000003, 0x300000C],
            "monsters": [1052, 0, 221, 0, 0],
            "opponentMonsters": [887, 0, 0, 0, 0],
            "lifePoints": 8000, "playerFlags": 0x20000,
            "duelPhaseRaw": 5,
        },
    }


def make_zip(path, transition=None, *, complete=True, base_seed=12345):
    with ZipFile(path, "w") as archive:
        rows = [{
            "kind": "metadata", "formatVersion": 2, "requestedDuels": 1,
            "speedValue": 600, "deckPlans": {},
            "rngSeedOverride": {
                "baseSeed": base_seed, "globalDuelStartIndex": 0,
                "algorithm": "mix32-v1"
            }
        }]
        rows.append({
            "kind": "demo_init", "function": "rng_seed_candidate",
            "phase": "enter", "duelEntrySeedCall": True,
            "seedOverrideApplied": True, "globalDuelIndex": 0,
            "effectiveSeedRaw": derive_duel_seed(base_seed, 0)
        })
        if transition is not None:
            rows.append({
                "kind": "command", "opcodeMasked": "0x00CE",
                "opcodeRaw": "0x00CE", "args": [0xCE, 268, 0, 0]
            })
            rows.append(transition)
        rows.append({
            "kind": "command", "opcodeMasked": "0x0004",
            "opcodeRaw": "0x0004", "args": [4, 0, 0, 0]
        })
        for i, row in enumerate(rows[1:], 1):
            row["seq"] = i
        rows.append({
            "kind": "capture_summary", "complete": complete,
            "capturedDuels": 1, "requestedDuels": 1,
            "overwritten": 0, "rngSeedOverridesApplied": 1,
            "eventsWritten": len(rows) - 1
        })
        archive.writestr("worker-01/duels.jsonl",
                         "\n".join(json.dumps(r) for r in rows) + "\n")


class ExportSummonTests(unittest.TestCase):
    def setUp(self):
        self.directory = TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.capture = self.root / "parallel.zip"
        self.target = self.root / "scratch" / "differential"
        make_zip(self.capture, sample_transition())

    def test_export_valid_native_transition_and_idempotent_reexport(self):
        report = export(self.capture, self.target)
        self.assertEqual(report["duels"], 1)
        self.assertEqual(report["exportedFixtures"], 1)
        files = list(self.target.glob("auto-g*.json"))
        self.assertEqual(len(files), 1)
        case = json.loads(files[0].read_text(encoding="utf-8"))
        self.assertEqual(case["id"], "auto-g000000-seq0000003")
        self.assertEqual(case["action"]["ceContext"], 0x4045)
        self.assertEqual(case["before"]["activeHandIndices"], [3, 221, 12])
        self.assertEqual(case["after"]["activeHandIndices"], [3, 12])
        self.assertEqual(case["after"]["knownActiveMonsters"],
                         [1052, None, 221, None, None])
        self.assertEqual(case["action"]["resourceId"], 268)
        self.assertIsNone(case["action"]["nativeEligibility"])
        self.assertEqual(export(self.capture, self.target), report)

    def test_reject_changed_hand_or_other_monster(self):
        for key, replacement in [
            ("handIndices", [3, 221]),
            ("monsters", [1052, 12, 221, 0, 0]),
        ]:
            with self.subTest(key=key):
                event = sample_transition()
                event["seq"] = 3
                event["after"][key] = replacement
                if key == "handIndices":
                    event["after"]["handWords"] = [0x1000003, 0x20000DD]
                with self.assertRaisesRegex(ValueError,
                                             "single selected card|Other field"):
                    fixture_from_transition(event, 0)

    def test_reject_incorrect_ce_context_or_flags(self):
        for change in ["context", "flag", "life", "side"]:
            with self.subTest(change=change):
                event = sample_transition()
                event["seq"] = 3
                if change == "context":
                    event["ceContext"] = 0x8105  # set, not normal summon
                if change == "flag":
                    event["after"]["playerFlags"] = 0
                if change == "life":
                    event["after"]["lifePoints"] = 7900
                if change == "side":
                    event["after"]["side"] = 0
                with self.assertRaises(ValueError):
                    fixture_from_transition(event, 0)

    def test_no_old_capture_optimistic_fixtures(self):
        make_zip(self.capture, None)
        with self.assertRaisesRegex(ValueError, "No verified"):
            export(self.capture, self.target)
        report = export(self.capture, self.target, allow_empty=True)
        self.assertEqual(report["exportedFixtures"], 0)
        self.assertEqual(list(self.target.glob("auto-g*.json")), [])

    def test_bad_transition_is_excluded_with_reasons_not_exported(self):
        event = sample_transition()
        event["after"]["monsters"][3] = 5
        make_zip(self.capture, event)
        cases, report = collect_fixtures(self.capture)
        self.assertEqual(cases, [])
        self.assertEqual(report["observedTransitions"], 1)
        self.assertEqual(len(report["excluded"]), 1)

    def test_incomplete_source_is_rejected(self):
        make_zip(self.capture, sample_transition(), complete=False)
        with self.assertRaisesRegex(ValueError, "Incomplete"):
            export(self.capture, self.target)

    def test_refuse_overwrite_of_different_fixture(self):
        export(self.capture, self.target)
        path = next(self.target.glob("auto-g*.json"))
        path.write_text('{"unrelated":"do not overwrite"}', encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "Refusing to overwrite"):
            export(self.capture, self.target)
        self.assertIn("do not overwrite", path.read_text(encoding="utf-8"))

    def test_empty_hand_after_last_card_is_supported(self):
        event = sample_transition()
        event["seq"] = 3
        event["handIndex"] = 0
        event["sourceCardIndex"] = 221
        event["ceContext"] = 0x4000 | (2 << 1) | 1
        event["before"]["handIndices"] = [221]
        event["before"]["handWords"] = [0x20000DD]
        event["after"]["handIndices"] = []
        event["after"]["handWords"] = []
        output = fixture_from_transition(event, 0)
        self.assertEqual(output["after"]["activeHandCount"], 0)

    def test_unsupported_transition_method_fails_closed(self):
        event = sample_transition()
        event["seq"] = 3
        event["evidenceMethod"] = "guessed_from_timestamps"
        with self.assertRaisesRegex(ValueError, "method"):
            fixture_from_transition(event, 0)

    def test_cli_emits_output_and_fails_on_old_captures(self):
        self.assertEqual(main([str(self.capture),
                               "--output-dir", str(self.target)]), 0)
        other = self.root / "older.zip"
        make_zip(other, None)
        self.assertEqual(main([str(other),
                               "--output-dir", str(self.root / "other")]), 2)


if __name__ == "__main__":
    unittest.main()
