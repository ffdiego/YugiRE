"""Offline synthetic state-diff sequence, no real Joey or Frida."""
from __future__ import annotations
import copy
import json
from pathlib import Path
import tempfile
import unittest

from yugilab.compare_duel_checkpoints import compare, inventory, _window
from yugilab.state_interactions import _state_changes, analyze_capture, summarize


def player(side, lp, grave, *, skull=False, rain=False):
    slots = [dict(slot=i, cardIndex=0, occurrenceWord=0, positionWord=0,
                  flagsWord=0, associationCount=0, associationCountValid=True,
                  associations=[]) for i in range(11)]
    if skull:
        slots[5]["cardIndex"] = 414
        slots[5]["positionWord"] = 2
    if rain:
        slots[6]["cardIndex"] = 969
        slots[6]["positionWord"] = 2
    return dict(side=side, lifePoints=lp, handCount=0, deckCount=28,
                graveyardCount=grave, rawPlayerStatusByte8=0,
                terminalFlags=0, fieldSlots=slots, handIndices=[])


def native(seq, opcode, *, rid=0, state=None):
    e = dict(kind="command", seq=seq, threadId=777, caller="0x57977f",
             args=[opcode, rid, 0, 0], stateSampled=True)
    if state is not None:
        e["stateObservation"] = state
    if opcode == 0xCF:
        e["cfResourceIdCandidate"] = rid
    return e


def observation(kind, **kwargs):
    return dict(kind=kind, nonAtomic=True,
                samplePoint="command_producer_onEnter", **kwargs)


class NativeStateInteractionsTests(unittest.TestCase):
    def fixture(self):
        before = [player(0,6600,0,skull=True,rain=True),
                  player(1,2600,0)]
        removed = copy.deepcopy(before[0]["fieldSlots"][6])
        after = copy.deepcopy(removed)
        after["cardIndex"] = 0
        return [
            dict(kind="metadata", mode="live_duels", formatVersion=2,
                 stateTrace=dict(enabled=True,
                                 schema="joey-command-entry-state-delta-v1",
                                 nonAtomic=True)),
            native(1,0x0D,state=observation("baseline",sides=before)),
            native(2,0xCF,rid=1315),
            native(3,0x42,rid=1000),
            native(4,0x8042,rid=1000,state=observation("delta",
                previousSampleSeq=3, changes=[
                    dict(side=0,lifePoints=dict(before=6600,after=7600),
                         graveyardCount=dict(before=0,after=1),
                         fieldSlots=[dict(slot=6,before=removed,after=after)]),
                    dict(side=1,lifePoints=dict(before=2600,after=3600))])),
            native(5,0x43,rid=300),
            native(6,0x0E,state=observation("delta",
                previousSampleSeq=5,
                changes=[dict(side=0,lifePoints=dict(before=7600,after=7300))])),
            native(7,0x04),
            dict(kind="capture_summary", complete=True, overwritten=0,
                 capturedDuels=1, eventsWritten=7,
                 stateTrace=dict(samples=7,baselines=1,deltas=2,readErrors=0))
        ]

    def write(self, directory, records):
        f = Path(directory) / "duels-state-synthetic.jsonl"
        f.write_text("\n".join(json.dumps(e) for e in records) + "\n",
                     encoding="utf-8")
        return f

    def test_unchanged_command_entry_needs_no_state_observation(self):
        # Valid command samples without stateObservation are expected:
        # the native sampler records only baseline/deltas on change.
        command = native(2, 0xCF, rid=1315)
        self.assertTrue(command["stateSampled"])
        self.assertNotIn("stateObservation", command)
        self.assertEqual(_state_changes(command, 0), [])
        self.assertEqual(_state_changes(
            native(1, 0x0D, state=observation(
                "baseline", sides=[player(0, 8000, 0),
                                   player(1, 8000, 0)])), 0), [])

    def test_event_deltas_and_hypothesis_not_causality(self):
        with tempfile.TemporaryDirectory() as td:
            path = self.write(td,self.fixture())
            result = summarize([path])
            self.assertEqual(len(result["captures"]),1)
            self.assertEqual(result["captures"][0]["stateTrace"]["samples"],7)
            presence = result["captures"][0]["skullFieldPresence"]
            self.assertEqual(presence["presentSamples"], 7)
            self.assertEqual(presence["absentSamples"], 0)
            self.assertEqual(presence["unobservedCommandEntries"], 0)
            temporal = result["graveyardLp300TemporalCorrelations"][0]
            self.assertEqual(temporal["unitGraveyardIncreases"], 1)
            self.assertEqual(temporal["pairedCount"], 1)
            self.assertEqual(temporal["pairs"][0]["graveyardSeq"], 4)
            self.assertEqual(temporal["pairs"][0]["lossSeq"], 6)
            self.assertEqual(temporal["pairs"][0]["conclusion"], "TemporalCorrelationOnly")
            cases=result["rainSkullCases"]
            self.assertEqual(len(cases),1)
            case=cases[0]
            self.assertEqual(case["conclusion"],"Unverified")
            self.assertEqual(case["skullSlotsPresentAtRainCf"][0]["slot"],5)
            self.assertEqual(case["fieldExits"][0]["side"],0)
            self.assertEqual(case["graveCountIncreases"][0]["countDelta"],1)
            self.assertEqual(case["damageCommands300"][0]["targetSide"],0)
            self.assertEqual(case["lpLoss300"][0]["side"],0)
            self.assertNotIn("Confirmed",str(case))
            self.assertEqual(result["captures"][0]["terminalCommands"],1)

    def test_rejects_stale_field_or_lp_before_value(self):
        records=self.fixture()
        records[4]["stateObservation"]["changes"][0]["fieldSlots"][0]["before"]["cardIndex"]=1
        with tempfile.TemporaryDirectory() as td:
            path=self.write(td,records)
            with self.assertRaisesRegex(ValueError,"stale/mismatched"):
                analyze_capture(path)

    def test_rejects_missing_baseline_and_inconsistent_footer(self):
        records=self.fixture()
        del records[1]["stateObservation"]
        with tempfile.TemporaryDirectory() as td:
            path=self.write(td,records)
            with self.assertRaisesRegex(ValueError,"initial baseline"):
                analyze_capture(path)
        records=self.fixture()
        records[-1]["stateTrace"]["deltas"]=99
        with tempfile.TemporaryDirectory() as td:
            path=self.write(td,records)
            with self.assertRaisesRegex(ValueError,"counts do not match"):
                analyze_capture(path)

    def test_does_not_accept_non_state_capture(self):
        records=self.fixture()
        records[0].pop("stateTrace")
        with tempfile.TemporaryDirectory() as td:
            path=self.write(td,records)
            with self.assertRaisesRegex(ValueError,"Not a command-entry"):
                analyze_capture(path)

    def test_rejects_non_contiguous_sampling_context(self):
        records=self.fixture()
        records[3].pop("stateSampled")
        with tempfile.TemporaryDirectory() as td:
            path=self.write(td,records)
            with self.assertRaisesRegex(ValueError,"missing contiguous previous sample"):
                analyze_capture(path)

    def test_compare_explicit_checkpoint_pairs(self):
        def checkpoint(line, life, graveyard_count):
            def side(lp, count):
                return dict(life=lp, hand=[], deck=[{}] * 28, graveyard=[{}] * count,
                            monsters=[None] * 5, spells=[None] * 5, fieldSpell=None)

            sides = [side(life[0], graveyard_count), side(life[1], 0)]
            sides[0]["spells"][0] = dict(card=dict(internalIndex=414), faceDown=False)
            if line is None:
                sides[0]["spells"][1] = dict(card=dict(internalIndex=969), faceDown=False)
            return dict(boundary=dict(line=line, lastExecutedSequence=8 if line else None), sides=sides)

        document = dict(schema="yugire-duel-checkpoints-v1", complete=True, checkpoints=[
            checkpoint(None, (6600, 2600), 0),
            checkpoint(7, (7600, 3600), 1),
            checkpoint(9, (7300, 3600), 1),
        ])
        pairs = [(1, None), (4, 7), (6, 9)]
        with tempfile.TemporaryDirectory() as td:
            path = self.write(td, self.fixture())
            report = compare(path, document, pairs)
            self.assertEqual(report["verdict"], "projected_match")
            self.assertEqual(report["matchedPrefix"], 3)
            self.assertFalse(report["nativeSamplesAreAtomic"])

            document["checkpoints"][2]["sides"][0]["life"] = 7310
            result = compare(path, document, pairs)
            self.assertEqual(result["verdict"], "mismatch")
            self.assertEqual(result["matchedPrefix"], 2)
            self.assertEqual(result["firstObservedDifference"], dict(
                nativeSeq=6, scriptLine=9, lastExecutedSequence=8, afterGap=False,
                fields=[dict(side=0, field="lifePoints", native=7300, csharp=7310)]))

            gap = compare(path, document, [(1, None), (4, 8), (6, 9)])
            self.assertEqual(gap["verdict"], "inconclusive")
            self.assertEqual(gap["matchedPrefix"], 1)
            self.assertEqual(gap["gaps"], [dict(nativeSeq=4, scriptLine=8,
                                               missingNativeSample=False, missingScriptCheckpoint=True)])
            self.assertTrue(gap["firstObservedDifference"]["afterGap"])

            missing_native = compare(path, document, [(1, None), (80, 7)])
            self.assertEqual(missing_native["verdict"], "inconclusive")
            self.assertTrue(missing_native["gaps"][0]["missingNativeSample"])

            document["complete"] = False
            self.assertEqual(compare(path, document, pairs[:2])["verdict"], "inconclusive")
            with self.assertRaisesRegex(ValueError, "increase strictly"):
                compare(path, document, [(1, None), (6, 9), (4, 7)])

    def test_inventory_preserves_unpaired_entries_and_sampling_gaps(self):
        records = self.fixture()
        # An unsampled entry ends the state chain. Restore a baseline at
        # the next entry rather than pretending the old state was observed.
        records[2]["stateSampled"] = False
        records[3]["stateObservation"] = observation(
            "baseline", sides=copy.deepcopy(records[1]["stateObservation"]["sides"]))
        records[-1]["stateTrace"].update(samples=6, baselines=2)
        def side(lp):
            return dict(life=lp, hand=[], deck=[{}] * 28, graveyard=[],
                        monsters=[None] * 5, spells=[None] * 5, fieldSpell=None)
        document = dict(schema="yugire-duel-checkpoints-v1", complete=False, checkpoints=[
            dict(boundary=dict(line=None, lastExecutedSequence=None), sides=[side(6600), side(2600)]),
            dict(boundary=dict(line=4, lastExecutedSequence=8, hasPendingChoice=True),
                 turn=2, phase="Main", sides=[side(7600), side(3600)]),
        ])
        with tempfile.TemporaryDirectory() as td:
            path = self.write(td, records)
            report = inventory(path, document, _window("2:4"))
            self.assertEqual(report["schema"], "yugire-duel-checkpoint-inventory-v1")
            self.assertNotIn("verdict", report)
            self.assertFalse(report["csharpComplete"])
            entries = report["nativeEntries"]
            self.assertEqual([row["nativeSeq"] for row in entries], [2, 3, 4])
            self.assertIsNone(entries[0]["sides"])
            self.assertEqual(entries[0]["args"], records[2]["args"])
            self.assertEqual(entries[1]["sides"][0]["lifePoints"], 6600)
            self.assertEqual(entries[2]["sides"][0]["lifePoints"], 7600)
            self.assertEqual(report["csharpBoundaries"][0]["fieldMonsterAttributes"], [[None] * 5] * 2)
            boundaries = report["csharpBoundaries"]
            self.assertEqual([row["scriptLine"] for row in boundaries], [None, 4])
            self.assertTrue(boundaries[1]["boundary"]["hasPendingChoice"])
            self.assertEqual(boundaries[1]["phase"], "Main")
            self.assertEqual(len(inventory(path, document)["nativeEntries"]), 7)
            with self.assertRaisesRegex(ValueError, "Native window"):
                inventory(path, document, (4, 2))
            records[-1]["stateTrace"]["samples"] = 99
            with self.assertRaisesRegex(ValueError, "counts do not match"):
                inventory(self.write(td, records), document)

    def test_compare_field_slot_identity_and_pose(self):
        records = self.fixture()
        observed = records[1]["stateObservation"]["sides"]
        observed[0]["fieldSlots"][3].update(cardIndex=81, positionWord=0x201)
        observed[1]["fieldSlots"][10].update(cardIndex=82, positionWord=2)
        observed[1]["fieldSlots"][4].update(positionWord=3, occurrenceWord=77)
        checkpoint = dict(boundary=dict(line=None, lastExecutedSequence=None), sides=[
            dict(life=6600, hand=[], deck=[{}] * 28, graveyard=[],
                 monsters=[None, None, None, dict(card=dict(internalIndex=81, occurrence=900),
                                                  faceDown=True, defense=True), None],
                 spells=[dict(card=dict(internalIndex=414), faceDown=False),
                         dict(card=dict(internalIndex=969), faceDown=False), None, None, None],
                 fieldSpell=None),
            dict(life=2600, hand=[], deck=[{}] * 28, graveyard=[], monsters=[None] * 5,
                 spells=[None] * 5, fieldSpell=dict(card=dict(internalIndex=82), faceDown=False)),
        ])
        document = dict(schema="yugire-duel-checkpoints-v1", complete=True, checkpoints=[checkpoint])
        with tempfile.TemporaryDirectory() as td:
            path = self.write(td, records)
            self.assertEqual(compare(path, document, [(1, None)])["verdict"], "projected_match")

            checkpoint["sides"][0]["monsters"][3]["defense"] = False
            checkpoint["sides"][0]["spells"][1] = None
            checkpoint["sides"][1]["fieldSpell"]["faceDown"] = True
            report = compare(path, document, [(1, None)])
            self.assertEqual(report["verdict"], "mismatch")
            self.assertEqual(report["firstObservedDifference"]["fields"], [
                dict(side=0, field="monsters", slot=3,
                     native=dict(internalIndex=81, faceDown=True, defense=True),
                     csharp=dict(internalIndex=81, faceDown=True, defense=False)),
                dict(side=0, field="spells", slot=1,
                     native=dict(internalIndex=969, faceDown=False), csharp=None),
                dict(side=1, field="fieldSpell", native=dict(internalIndex=82, faceDown=False),
                     csharp=dict(internalIndex=82, faceDown=True)),
            ])


if __name__=="__main__":
    unittest.main()
