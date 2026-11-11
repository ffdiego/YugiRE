"""Offline corpus provenance/pairing tests; no native execution or guessed context."""
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
from build_ai_combat_corpus import build_corpus, build_file, read_card_ids


class CombatCorpusTests(unittest.TestCase):
    def fixture(self):
        events = [
            {"kind": "ai_probe", "phase": "enter", "function": "select_normal", "callId": 1,
             "threadId": 7, "side": 0, "pass": -1, "handCount": 1, "handIndices": [550], "handWords": [550]},
            {"kind": "ai_probe", "phase": "leave", "function": "computed_stats", "callId": 3,
             "threadId": 7, "side": 0, "rawArgs": [0, 0], "withinSelector": True,
             "outputHex": "260212056C07000084030000"},
            {"kind": "ai_probe", "phase": "leave", "function": "computed_stats", "callId": 4,
             "threadId": 7, "side": 1, "rawArgs": [1, 0], "withinSelector": True,
             "outputHex": "18000F05A40600007E040000"},
            {"kind": "ai_probe", "phase": "leave", "function": "combat_event", "callId": 2,
             "threadId": 7, "side": 0, "rawArgs": [0, 0, 0], "withinSelector": True,
             "outputHex": "0808C800"},
            {"kind": "ai_probe", "phase": "leave", "function": "select_normal", "callId": 1,
             "threadId": 7, "side": 0, "selectedHandIndex": 0, "selectedCardIndexAtEntry": 550},
        ]
        for seq, row in enumerate(events, 1):
            row["seq"] = seq
        return [{"kind": "metadata", "seen": len(events), "retained": len(events),
                 "overwritten": 0, "exeSha256": None}, *events]

    def build(self, records=None):
        return build_corpus(self.fixture() if records is None else records, "synthetic.jsonl", "synthetic-sha",
                            {550: 531, 24: 77})

    def test_pairs_inputs_without_inferring_context_from_output(self):
        result = self.build()
        self.assertEqual(result["coverage"], {"nativeCombatBuffers": 1, "pairedStats": 1,
                                              "missingOrAmbiguousStats": 0, "fullyObservedContexts": 0})
        event = result["events"][0]
        self.assertEqual(event["attacker"]["attack"], 1900)
        self.assertEqual(event["defender"]["attack"], 1700)
        self.assertEqual(event["attacker"]["resource_id"], 531)
        self.assertEqual(event["expectedSignedScore"], 200)
        self.assertEqual(event["knownLowWordComparisonMask"], 0x0FFA)
        self.assertIsNone(event["defenderPosition"])
        self.assertIsNone(event["initialDescriptorLowWord"])
        self.assertFalse(event["fullyObservedContext"])
        self.assertIsNone(result["metadataExeSha256"])

    def test_missing_rival_not_filled_from_an_earlier_getter(self):
        rows = self.fixture()
        # The rival buffer is outside F40's entry interval: allocated before parent.
        rows[3]["callId"] = 0
        event = self.build(rows)["events"][0]
        self.assertEqual(event["pairing"], "incomplete_computed_stats")
        self.assertIsNone(event["defender"])
        self.assertTrue(any("defender evaluation stats" in text for text in event["additionalUnknownContext"]))

    def test_wrong_argument_pair_and_extra_children_are_ambiguous(self):
        rows = self.fixture()
        rows[3]["rawArgs"] = [1, 4]
        self.assertIsNone(self.build(rows)["events"][0]["defender"])
        rows = self.fixture()
        extra = deepcopy(rows[3]); extra["callId"] = 5
        rows.insert(4, extra)
        rows[0].update(seen=6, retained=6)
        for seq, row in enumerate(rows[1:], 1):
            row["seq"] = seq
        self.assertEqual(self.build(rows)["coverage"]["pairedStats"], 0)

    def test_other_threads_cannot_supply_the_missing_buffer(self):
        rows = self.fixture()
        rows[3]["threadId"] = 8
        # The strict selector audit rejects output outside the recorded thread.
        with self.assertRaises(ValueError):
            self.build(rows)

    def test_integrity_and_read_failures_fail_closed(self):
        for mutate in [lambda rows: rows[0].update(overwritten=1),
                       lambda rows: rows[0].update(seen=9),
                       lambda rows: rows[3].update(seq=100),
                       lambda rows: rows[2].update(readError="unreadable")]:
            rows = self.fixture(); mutate(rows)
            with self.assertRaises(ValueError):
                self.build(rows)

    def test_capture_hash_checked_before_extraction(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "synthetic.jsonl"
            raw = "\n".join(json.dumps(row) for row in self.fixture()).encode()
            path.write_bytes(raw)
            with self.assertRaises(ValueError):
                build_file(path, None)
            with self.assertRaises(ValueError):
                build_file(path, "0" * 64)
            result = build_file(path, hashlib.sha256(raw).hexdigest())
            self.assertEqual(result["coverage"]["pairedStats"], 1)

    def test_resource_ids_match_pinned_dat_when_available(self):
        dat = ROOT.parent / "passion/data.dat"
        if not dat.is_file():
            self.skipTest("Original DAT is optional")
        ids = read_card_ids(dat)
        corpus = json.loads((ROOT / "src/tests/Joey.Tests/Fixtures/ai-combat-differential.json").read_text())
        for event in corpus["events"]:
            for name in ("attacker", "defender"):
                value = event[name]
                if value is not None:
                    self.assertEqual(value["resource_id"], ids[value["internal_card_index"]])

    def test_versioned_fixture_matches_original_when_available(self):
        corpus = json.loads((ROOT / "src/tests/Joey.Tests/Fixtures/ai-combat-differential.json").read_text())
        source = ROOT / "scratch/traces" / corpus["sourceCapture"]
        if not source.is_file():
            self.skipTest("Original passive capture is optional")
        # Resource IDs were read independently from the pinned DAT during build.
        ids = {}
        for event in corpus["events"]:
            for name in ("attacker", "defender"):
                if event[name] is not None:
                    ids[event[name]["internal_card_index"]] = event[name]["resource_id"]
        raw = source.read_bytes()
        self.assertEqual(hashlib.sha256(raw).hexdigest(), corpus["sourceCaptureSha256"])
        rebuilt = build_corpus([json.loads(line) for line in raw.decode().splitlines()],
                               source.name, corpus["sourceCaptureSha256"], ids)
        self.assertEqual(rebuilt, corpus)
        self.assertEqual(corpus["coverage"]["pairedStats"], 14)
        self.assertEqual(corpus["coverage"]["fullyObservedContexts"], 0)


if __name__ == "__main__":
    unittest.main()
