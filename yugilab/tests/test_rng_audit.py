"""Validate RNG collision detection without Joey, Ghidra, or Frida."""
from __future__ import annotations

import json
from pathlib import Path
import tempfile
import unittest
from zipfile import ZipFile

from yugilab.rng_audit import audit_captures


def trace(pid: int, permutations: list[tuple[list[int], list[int]]]) -> str:
    rows = [
        {"kind": "metadata", "processId": pid,
         "startedUtc": "2026-11-08T18:00:00+00:00"}
    ]
    seq = 1
    for first, second in permutations:
        seq += 1
        rows.append({
            "kind": "demo_init", "phase": "enter", "function": "scene_config",
            "requestedSceneMode": 7, "seq": seq, "timestampMs": 1000 + seq,
        })
        for side, cards in ((1, second), (0, first)):
            seq += 1
            rows.append({
                "kind": "demo_init", "phase": "leave",
                "function": "deck_shuffle", "side": side, "seq": seq,
                "deck": {"count": len(cards), "indices": cards},
                "timestampMs": 1000 + seq,
            })
        seq += 1
        # Extra shuffle inside a duel must not overwrite the first pair.
        rows.append({
            "kind": "demo_init", "phase": "leave", "function": "deck_shuffle",
            "side": 1, "seq": seq,
            "deck": {"indices": [99, 98, 97]},
        })
    rows.append({
        "kind": "capture_summary", "complete": True, "capturedDuels": len(permutations),
        "overwritten": 0,
    })
    return "\n".join(json.dumps(row) for row in rows) + "\n"


class RngAuditTests(unittest.TestCase):
    def test_identical_two_decks_across_pids_are_duplicate(self):
        with tempfile.TemporaryDirectory() as tmp:
            a = Path(tmp) / "first.jsonl"
            b = Path(tmp) / "second.jsonl"
            a.write_text(trace(10, [([1, 2, 3], [7, 8, 9])]), encoding="utf-8")
            b.write_text(trace(20, [([1, 2, 3], [7, 8, 9])]), encoding="utf-8")
            result = audit_captures([a, b])
        self.assertEqual(result["captureCount"], 2)
        self.assertEqual(result["fingerprintedScenes"], 2)
        self.assertEqual(result["uniqueFingerprints"], 1)
        self.assertEqual(result["duplicateObservations"], 1)
        self.assertEqual(result["crossProcessDuplicateGroups"], 1)
        self.assertEqual({m["processId"] for m in
                          result["duplicateGroups"][0]["matches"]}, {10, 20})

    def test_one_deck_changed_is_not_a_duplicate(self):
        with tempfile.TemporaryDirectory() as tmp:
            a = Path(tmp) / "first.jsonl"
            b = Path(tmp) / "second.jsonl"
            a.write_text(trace(10, [([1, 2, 3], [7, 8, 9])]), encoding="utf-8")
            b.write_text(trace(20, [([1, 3, 2], [7, 8, 9])]), encoding="utf-8")
            result = audit_captures([a, b])
        self.assertEqual(result["uniqueFingerprints"], 2)
        self.assertEqual(result["duplicateObservations"], 0)
        self.assertEqual(result["crossProcessDuplicateGroups"], 0)

    def test_zip_is_streamed_without_extraction(self):
        with tempfile.TemporaryDirectory() as tmp:
            path = Path(tmp) / "parallel-example.zip"
            with ZipFile(path, "w") as zipped:
                zipped.writestr("parallel/captures/a.jsonl",
                                trace(10, [([1, 2], [5, 6])]))
                zipped.writestr("parallel/captures/b.jsonl",
                                trace(20, [([1, 2], [5, 6])]))
            result = audit_captures([path])
        self.assertEqual(result["captureCount"], 2)
        self.assertEqual(result["crossProcessDuplicateGroups"], 1)

    def test_each_duel_gets_own_first_shuffle_pair(self):
        with tempfile.TemporaryDirectory() as tmp:
            a = Path(tmp) / "duels.jsonl"
            a.write_text(trace(10, [
                ([1, 2, 3], [7, 8, 9]),
                ([3, 2, 1], [9, 8, 7]),
                ([1, 2, 3], [7, 8, 9])]), encoding="utf-8")
            report = audit_captures([a])
        self.assertEqual(report["totalScenesObserved"], 3)
        self.assertEqual(report["fingerprintedScenes"], 3)
        self.assertEqual(report["uniqueFingerprints"], 2)
        self.assertEqual(report["duplicateObservations"], 1)
        self.assertEqual(report["crossProcessDuplicateGroups"], 0)

    def test_seed_candidate_is_recorded_before_scene_and_correlated(self):
        with tempfile.TemporaryDirectory() as tmp:
            a = Path(tmp) / "seed-candidate.jsonl"
            original = trace(431, [([12, 13], [20, 21])])
            rows = [json.loads(line) for line in original.splitlines()]
            rows.insert(1, {
                "kind": "demo_init", "phase": "enter",
                "function": "rng_seed_candidate", "candidateSeedRaw": 12345,
                "candidateSeedHex": "0x00003039",
                "caller": "0x00408b90", "seq": 0,
            })
            a.write_text("\n".join(json.dumps(row) for row in rows) + "\n",
                         encoding="utf-8")
            report = audit_captures([a])
        self.assertEqual(report["fingerprintedScenes"], 1)
        source = report["sources"][0]
        self.assertEqual(len(source["rngSeedCandidateCalls"]), 1)
        self.assertEqual(source["rngSeedCandidateCalls"][0]["valueRaw"], 12345)
        self.assertEqual(
            source["fingerprints"][0]["seedCandidateBeforeScene"]["valueRaw"],
            12345)

    def test_partial_shuffles_never_count_as_complete_pair(self):
        with tempfile.TemporaryDirectory() as tmp:
            a = Path(tmp) / "partial.jsonl"
            data = trace(10, [([1, 2], [3, 4])])
            rows = [json.loads(line) for line in data.splitlines()]
            rows = [r for r in rows if not (
                r.get("kind") == "demo_init" and
                r.get("function") == "deck_shuffle" and r.get("side") == 0)]
            rows = [r for r in rows if r.get("kind") != "capture_summary"]
            a.write_text("\n".join(json.dumps(r) for r in rows), encoding="utf-8")
            report = audit_captures([a])
        self.assertEqual(report["fingerprintedScenes"], 0)
        self.assertEqual(report["totalScenesObserved"], 1)
        self.assertEqual(len(report["incompleteSources"]), 1)


if __name__ == "__main__":
    unittest.main()
