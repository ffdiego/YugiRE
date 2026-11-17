"""Synthetic discovery tests: never load a real DAT, EXE or Frida process."""
from __future__ import annotations

import copy
import json
from pathlib import Path
import tempfile
import unittest

from yugilab.discovery import analyze, catalog, prepare


class NativeEffectDiscoveryTests(unittest.TestCase):
    def setUp(self):
        fp = {"exeSha256": "a" * 64}
        self.reg = {"schemaVersion": 1, "fingerprints": fp, "cards": []}
        self.attr = {"registryVersion": 1, "fingerprints": fp, "cards": []}
        self.pool = {"schemaVersion": 1, "mode": "SandboxPartialOnly", "cards": []}

        def add(idx, rid, cls, cat, level, attack, cb=True):
            self.reg["cards"].append(dict(
                identity=dict(internalIndex=idx, resourceId=rid, name=f"Card {idx}"),
                callbackLookup=dict(state="Present" if cb else "AbsentInTable")))
            self.attr["cards"].append(dict(
                cardRef=f"card:{idx}", availability={"Joey": True},
                classCode=cls, level=level, attack=attack,
                properties=(cls << 20) | (cat << 18) | (level << 25)))
        for i in range(1, 33):
            add(i, 2000 + i, 4, 0, 4, 1000, cb=False)
            self.pool["cards"].append(dict(internalIndex=i, resourceId=2000 + i,
                policy="BaseMonsterSandbox", note="Scoped sandbox normals only; not native parity."))
        for i in range(100, 137):
            add(i, 1000 + i, 4, 1, 4, 300)
        for i in range(300, 337):
            rid = 1007 if i == 300 else 3000 + i
            add(i, rid, 22 if i == 300 else 21 + i % 3, 0, 0, 0)

    def test_decks_are_rotated_mixed_and_deterministic(self):
        first = prepare(self.reg, self.attr, self.pool, rounds=3, seed=12345)
        self.assertEqual(first, prepare(self.reg, self.attr, self.pool, rounds=3, seed=12345))
        self.assertEqual(len(first["decks"]), 6)
        by_idx = {c["internalIndex"]: c for c in catalog(self.reg, self.attr)}
        for deck in first["decks"].values():
            indexes = deck["main"]
            self.assertEqual(len(indexes), 40)
            self.assertEqual(len(set(indexes)), 40)
            self.assertEqual(sum(by_idx[i]["group"] == "normalControl" for i in indexes), 16)
            self.assertEqual(sum(by_idx[i]["group"] == "effectCategoryMonster" for i in indexes), 12)
            self.assertEqual(sum(by_idx[i]["group"].startswith("nonMonsterClass") for i in indexes), 12)
            self.assertEqual(deck["second"], [])
        self.assertGreater(first["selectedDistinctCards"], 40)
        self.assertFalse(first["nativeParity"])

    def test_fail_closed_registry_and_review_scope(self):
        bad = copy.deepcopy(self.attr)
        bad["fingerprints"]["exeSha256"] = "b" * 64
        with self.assertRaisesRegex(ValueError, "fingerprints"):
            catalog(self.reg, bad)
        bad = copy.deepcopy(self.pool)
        bad["cards"][0]["resourceId"] = 9
        with self.assertRaisesRegex(ValueError, "Unreviewed or non-normal"):
            prepare(self.reg, self.attr, bad)
        bad = copy.deepcopy(self.attr)
        bad["cards"][-1]["properties"] = 31 << 20
        with self.assertRaisesRegex(ValueError, "Class mismatches"):
            catalog(self.reg, bad)
        with self.assertRaisesRegex(ValueError, "Need 34"):
            prepare(self.reg, self.attr, self.pool, normal_count=34,
                    effect_count=6, nonmonster_count=0)

    def make_native_capture(self, path: Path, *, broken=False):
        deck = prepare(self.reg, self.attr, self.pool, rounds=1)["decks"]
        main_a = deck["round-01-A.json"]["main"]
        main_b = deck["round-01-B.json"]["main"]
        records = [
            dict(kind="metadata", mode="live_duels", deckPlans={
                "0": {"main": main_a}, "1": {"main": main_b}}),
            dict(kind="deck_override", seq=1, phase="verified", side=0,
                 actualIndices=main_a),
            dict(kind="deck_override", seq=2, phase="verified", side=1,
                 actualIndices=main_b),
            dict(kind="command", seq=3, caller="0x57977f", threadId=11,
                 args=[0xCF, 1007, 0, 0], cfResourceIdCandidate=1007),
            dict(kind="command", seq=4, caller="0x579984", threadId=11,
                 args=[0x0D, 0, 0, 0]),
            dict(kind="command", seq=5, caller="0x5783f2", threadId=11,
                 args=[0x72, 0, 0, 0]),
            dict(kind="command", seq=6, caller="0x578513", threadId=11,
                 args=[0x8043, 300, 0, 1],
                 lifePointsObserved={"lifePoints": [8000, 8000]}),
            dict(kind="command", seq=7, caller="0x5799ef", threadId=11,
                 args=[0x0E, 0, 0, 0],
                 lifePointsObserved={"lifePoints": [8000, 7700]}),
            dict(kind="command", seq=8, caller="0x5abeab", threadId=11,
                 args=[0xCE, 1100, 0, 0], ceResourceIdLow16=1100),
            dict(kind="command", seq=9, caller="0x5bc545", threadId=11,
                 args=[4, 0, 0, 0]),
            dict(kind="capture_summary", complete=not broken, overwritten=0,
                 capturedDuels=1, eventsWritten=9)
        ]
        path.write_text("\n".join(json.dumps(e) for e in records) + "\n",
                        encoding="utf-8")

    def test_capture_queue_distinguishes_commands_and_lp_evidence(self):
        with tempfile.TemporaryDirectory() as td:
            f = Path(td) / "duels-test.jsonl"
            self.make_native_capture(f)
            result = analyze(self.reg, self.attr, [f])
            self.assertTrue(result["integrityOk"])
            self.assertEqual(result["terminalCommands"], 1)
            self.assertEqual(result["captures"][0]["exactP045LpDeltas"], 1)
            raimei = next(x for x in result["perCard"] if x["resourceId"] == 1007)
            self.assertEqual(raimei["cfCommands"], 1)
            self.assertEqual(raimei["lpDeltaExactCases"], 1)
            self.assertEqual(raimei["evidence"], "ObservedFixedLpDeltaInScope")
            self.assertFalse(raimei["readyForFullCsharpComparison"])
            monster = next(x for x in result["perCard"] if x["resourceId"] == 1100)
            self.assertEqual(monster["evidence"], "ResourceAssociatedCommandsOnly")
            self.assertEqual(monster["lpDeltaExactCases"], 0)

    def test_missing_verified_deck_batch_fails_integrity(self):
        with tempfile.TemporaryDirectory() as td:
            f = Path(td) / "duels-test.jsonl"
            self.make_native_capture(f)
            records = [json.loads(line) for line in f.read_text(encoding="utf-8").splitlines()]
            records[2]["phase"] = "applied"
            f.write_text("\n".join(json.dumps(x) for x in records) + "\n",
                         encoding="utf-8")
            result = analyze(self.reg, self.attr, [f])
            self.assertFalse(result["integrityOk"])
            self.assertTrue(any("Side 1 verified deck batches" in issue
                                for issue in result["captures"][0]["issues"]))

    def test_incomplete_capture_not_accepted(self):
        with tempfile.TemporaryDirectory() as td:
            f = Path(td) / "duels-test.jsonl"
            self.make_native_capture(f, broken=True)
            result = analyze(self.reg, self.attr, [f])
            self.assertFalse(result["integrityOk"])
            self.assertTrue(result["captures"][0]["issues"])


if __name__ == "__main__":
    unittest.main()
