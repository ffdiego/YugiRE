"""Synthetic integration fixtures; no executable or DAT needed."""
import copy
import json
from pathlib import Path
import tempfile
import unittest

from yugilab.compare_battle_calculations import compare
from yugilab.tests.test_state_interactions import player, native, observation


def fixture(direct=False, attacker=1):
    target = 1 - attacker
    sides = [player(side, 8000, 0) for side in range(2)]
    sides[attacker]["fieldSlots"][3].update(cardIndex=81, positionWord=2, occurrenceWord=17)
    sides[attacker]["fieldSlots"][0].update(cardIndex=81, positionWord=2, occurrenceWord=33)
    if not direct:
        sides[target]["fieldSlots"][2].update(cardIndex=81, positionWord=3, occurrenceWord=91)
    selector = attacker | target << 1 | (4 if direct else 0) | 3 << 8 | 2 << 11
    blocks = [None, None]
    for index, side, slot, pose in [(attacker, attacker, 3, 0), (target, target, 2, 0x20)]:
        blocks[index] = dict(blockIndex=index, descriptorWord=0xAB10 | slot << 1 | side | pose,
                             cardIndexWord=0 if direct and index == target else 81,
                             calculatedAttack=1200, calculatedDefense=800, damageWord=0)
    raw = dict(schema="joey-command-entry-battle-attributes-v1",
               samplePoint="command_producer_onEnter_0x31", nonAtomic=True,
               selectorWord=selector, blocks=blocks)
    rows = [
        dict(kind="metadata", mode="live_duels", formatVersion=2,
             stateTrace=dict(enabled=True, schema="joey-command-entry-state-delta-v1", nonAtomic=True),
             battleAttributesTrace=dict(enabled=True)),
        native(1, 0x31, state=observation("baseline", sides=sides)),
        native(2, 0x31), native(3, 0x04),
        dict(kind="capture_summary", complete=True, overwritten=0, capturedDuels=1, eventsWritten=3,
             stateTrace=dict(samples=3, baselines=1, deltas=0, readErrors=0),
             battleAttributesTrace=dict(samples=2, readErrors=0)),
    ]
    for event in rows[1:3]:
        event["battleAttributesObservation"] = copy.deepcopy(raw)

    def actor(side, slot, defense):
        return dict(target=dict(side=side, zone=2, occurrence=dict(value=side+100)),
                    slot=slot, internalIndex=81, resourceId=500, defense=defense,
                    faceDown=False, attack=1200, defenseValue=800)
    calc = dict(attacker=actor(attacker, 3, False),
                defender=None if direct else actor(target, 2, True),
                difference=None if direct else 400, requestedDirectDamage=1200 if direct else None)
    doc = dict(schema="yugire-action-projection-v1", complete=True, rowCount=2,
               rows=[dict(sequence=i, command="DeclareDirectAttackCommand" if direct else
                          "DeclareTargetedAttackCommand", outcome="Applied",
                          battleCalculation=copy.deepcopy(calc)) for i in (1, 2)])
    return rows, doc


class BattleCalculationComparisonTests(unittest.TestCase):
    def run_comparison(self, rows, doc, pairs):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "synthetic.jsonl"
            path.write_text("\n".join(json.dumps(row) for row in rows) + "\n", encoding="utf-8")
            return compare(path, doc, pairs)

    def test_match_both_sides_direct_and_targeted(self):
        for direct in (False, True):
            for attacker in (0, 1):
                rows, doc = fixture(direct, attacker)
                report = self.run_comparison(rows, doc, [(1, 1), (2, 2)])
                self.assertEqual(report["verdict"], "projected_match")
                self.assertEqual(report["matchedPrefix"], 2)
                self.assertEqual(report["nativeBattleSamples"], 2)

    def test_difference_reports_exact_role_side_slot(self):
        rows, doc = fixture()
        doc["rows"][1]["battleCalculation"]["defender"]["defenseValue"] = 900
        doc["rows"][1]["battleCalculation"]["difference"] = 300
        report = self.run_comparison(rows, doc, [(1, 1), (2, 2)])
        self.assertEqual(report["verdict"], "mismatch")
        self.assertEqual(report["matchedPrefix"], 1)
        self.assertEqual(report["firstObservedDifference"]["fields"], [
            dict(role="target", side=0, slot=2, internalIndex=81,
                 field="defenseValue", native=800, csharp=900)])

    def test_actor_difference_does_not_compare_wrong_actor_stats(self):
        rows, doc = fixture()
        doc["rows"][0]["battleCalculation"]["attacker"].update(slot=0, defenseValue=999)
        report = self.run_comparison(rows, doc, [(1, 1)])
        self.assertEqual([x["field"] for x in report["firstObservedDifference"]["fields"]], ["slot"])

    def test_read_error_before_difference_remains_inconclusive(self):
        rows, doc = fixture()
        del rows[1]["battleAttributesObservation"]
        rows[1]["battleAttributesReadError"] = "read failed"
        rows[-1]["battleAttributesTrace"] = dict(samples=1, readErrors=1)
        doc["rows"][1]["battleCalculation"]["attacker"]["defenseValue"] = 900
        report = self.run_comparison(rows, doc, [(1, 1), (2, 2)])
        self.assertEqual(report["verdict"], "inconclusive")
        self.assertEqual(report["matchedPrefix"], 0)
        self.assertEqual(report["gaps"][0]["nativeReason"], "battle_read_error")
        self.assertTrue(report["firstObservedDifference"]["afterGap"])

    def test_missing_and_uncorrelated_actors_are_gaps(self):
        rows, doc = fixture()
        rows[1]["battleAttributesObservation"]["blocks"][1]["descriptorWord"] ^= 1
        doc["rows"][1]["battleCalculation"] = None
        report = self.run_comparison(rows, doc, [(1, 1), (2, 2)])
        self.assertEqual(report["verdict"], "inconclusive")
        self.assertEqual(report["gaps"][0]["nativeActorStatuses"][0], "descriptor_selector_disagree")
        self.assertTrue(report["gaps"][1]["missingCsharpCalculation"])
        self.assertEqual(self.run_comparison(rows, doc, [(3, 1)])["gaps"][0]["nativeReason"],
                         "not_battle_command")

    def test_input_integrity_even_outside_pairs_and_incomplete_trace(self):
        rows, doc = fixture()
        doc["complete"] = False
        self.assertEqual(self.run_comparison(rows, doc, [(1, 1)])["verdict"], "inconclusive")
        with self.assertRaisesRegex(ValueError, "increase strictly"):
            self.run_comparison(rows, doc, [(2, 1), (1, 2)])
        rows[-1]["battleAttributesTrace"]["samples"] = 3
        with self.assertRaisesRegex(ValueError, "counts disagree"):
            self.run_comparison(rows, doc, [(1, 1)])
        rows[-1]["battleAttributesTrace"]["samples"] = 2
        doc["rows"][1]["sequence"] = 3
        with self.assertRaisesRegex(ValueError, "sequence gap"):
            self.run_comparison(rows, doc, [(1, 1)])


if __name__ == "__main__":
    unittest.main()
