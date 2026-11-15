"""Portable unit tests: no proprietary assets, Frida, Joey or .NET required."""
import copy
import unittest

from scripts.prepare_effect_pilot import BURN, prepare


class EffectPilotPreparationTests(unittest.TestCase):
    def setUp(self):
        self.fingerprints = {"exeSha256": "a" * 64}
        self.registry = {"schemaVersion": 1, "fingerprints": self.fingerprints, "cards": []}
        self.attrs = {"registryVersion": 1, "fingerprints": self.fingerprints, "cards": []}
        self.pool = {"schemaVersion": 1, "mode": "SandboxPartialOnly", "cards": []}
        for index in range(1, 46):
            rid = 2000 + index
            self.registry["cards"].append({"identity": {
                "internalIndex": index, "resourceId": rid, "name": f"Monster {index}"}})
            self.attrs["cards"].append({
                "cardRef": f"card:{index}", "availability": {"Joey": True},
                "classCode": 1, "level": 4, "attack": 1000 + index,
                "properties": (1 << 20) | (4 << 25) | (100 << 9)
            })
            self.pool["cards"].append({
                "internalIndex": index, "resourceId": rid, "policy": "BaseMonsterSandbox",
                "note": "Reviewed as sandbox-only basic stats, no effects."
            })
        for index, rid, name in BURN:
            self.registry["cards"].append({"identity": {
                "internalIndex": index, "resourceId": rid, "name": name}})
            self.attrs["cards"].append({
                "cardRef": f"card:{index}", "availability": {"Joey": True},
                "classCode": 21, "level": 0, "attack": 0,
                "properties": 21 << 20
            })

    def test_mixed_native_and_csharp_decks_match(self):
        files = prepare(self.pool, self.registry, self.attrs)
        native0 = files["native-effect-A.json"]["main"]
        native1 = files["native-effect-B.json"]["main"]
        sandbox = files["sandbox-mixed.json"]
        self.assertEqual(native0, sandbox["deck0"])
        self.assertEqual(native1, sandbox["deck1"])
        self.assertEqual(len(native0), 40)
        self.assertEqual(len(native1), 40)
        for deck in (native0, native1):
            self.assertEqual(sum(deck.count(idx) for idx, _, _ in BURN), 10)
            self.assertEqual(len([i for i in deck if i not in {x[0] for x in BURN}]), 30)
            self.assertLessEqual(max(deck.count(i) for i in deck), 3)
        self.assertEqual(len(files["effect-pilot-plan.json"]["effects"]), 5)
        self.assertEqual(files, prepare(self.pool, self.registry, self.attrs))

    def test_only_available_spells_used(self):
        self.attrs["cards"][-1]["availability"]["Joey"] = False
        files = prepare(self.pool, self.registry, self.attrs)
        self.assertEqual(len(files["effect-pilot-plan.json"]["effects"]), 4)
        self.assertEqual(files["effect-pilot-plan.json"]["normalsPerDeck"], 32)
        self.assertNotIn(BURN[-1][0], files["native-effect-A.json"]["main"])

    def test_rejects_unknown_category_and_fingerprint_mismatch(self):
        bad = copy.deepcopy(self.attrs)
        bad["cards"][0]["properties"] |= 1 << 18
        with self.assertRaisesRegex(ValueError, "Unreviewed or non-normal"):
            prepare(self.pool, self.registry, bad)
        bad = copy.deepcopy(self.attrs)
        bad["fingerprints"] = {"exeSha256": "b" * 64}
        with self.assertRaisesRegex(ValueError, "fingerprints disagree"):
            prepare(self.pool, self.registry, bad)

    def test_rejects_unreviewed_pool_and_mismatched_identity(self):
        bad = copy.deepcopy(self.pool)
        bad["mode"] = "UnreviewedCandidates"
        with self.assertRaisesRegex(ValueError, "explicitly reviewed"):
            prepare(bad, self.registry, self.attrs)
        bad = copy.deepcopy(self.registry)
        bad["cards"][0]["identity"]["resourceId"] = 999
        with self.assertRaisesRegex(ValueError, "pool identity"):
            prepare(self.pool, bad, self.attrs)


if __name__ == "__main__":
    unittest.main()
