"""Offline synthetic state-diff sequence, no real Joey or Frida."""
from __future__ import annotations
import copy
import json
from pathlib import Path
import tempfile
import unittest

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


if __name__=="__main__":
    unittest.main()
