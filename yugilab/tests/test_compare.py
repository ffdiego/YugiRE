"""Portable offline comparison tests. No real Joey/Frida needed."""
import json
from pathlib import Path
import tempfile
import unittest
from zipfile import ZipFile

from yugilab.compare import compare_runs, main
from yugilab.seed_math import derive_duel_seed


def events_for(base=12345, index=0, shift=0, marker=0, ce_source=23,
               unknown_caller=False, wrapper86=None):
    seed = derive_duel_seed(base, index)
    return [
        {"kind": "demo_init", "function": "scene_config", "phase": "enter", "requestedSceneMode": 7},
        {"kind": "demo_init", "function": "rng_seed_candidate", "phase": "enter", "duelEntrySeedCall": True,
         "candidateSeedRaw": marker, "seedOverrideApplied": True, "effectiveSeedRaw": seed,
         "globalDuelIndex": index},
        {"kind": "demo_init", "function": "deck_shuffle", "phase": "leave",
         "side": 0, "deck": {"indices": [1 + index, 2], "words": [0x10001 + index, 0x20002]}},
        {"kind": "demo_init", "function": "deck_shuffle", "phase": "leave",
         "side": 1, "deck": {"indices": [4, 5], "words": [0x10004, 0x20005]}},
        {"kind": "ai_probe", "function": "selector", "phase": "leave", "chosen": 1},
        {"kind": "demo_executor", "function": "place", "side": 0, "handIndex": 1},
        {"kind": "command", "opcodeMasked": "0x00CE", "opcodeRaw": "0x00CE",
         "sideBit15": 0, "caller": ("0x574804" if unknown_caller else "0x5abeab"),
         "args": [0xce, (10 + shift) * 65536 + 23, 0, 0],
         "ceNativeState": {"context": 0x8001, "source": ce_source, "stage": 37}},
        {"kind": "command", "opcodeMasked": "0x0086", "opcodeRaw": "0x0086",
         "sideBit15": 0, "caller": "0x574804", "args": [0x86, 0x0445, 0, 0],
         **({"wrapper86Caller": wrapper86} if wrapper86 is not None else {})},
        {"kind": "command", "opcodeMasked": "0x0004", "opcodeRaw": "0x0004",
         "sideBit15": 0, "caller": "0x5bc545", "args": [0x70004, 1, 0, 0],
         "terminalNativeState": {"life": [0, 7000]}},
    ]


def write_capture(z, member, indices, base=12345, shift=0, partial=False, ce_source=23,
                  unknown_caller=False, wrapper86=None):
    rows = [{"kind": "metadata", "formatVersion": 2,
             "requestedDuels": len(indices), "speedValue": 600, "deckPlans": {"0": {"main": [1]*40}},
             "rngSeedOverride": {"baseSeed": base, "globalDuelStartIndex": indices[0],
                                 "algorithm": "mix32-v1"}}]
    for index in indices:
        rows.extend(events_for(base, index, shift, marker=9 if shift else 8,
                               ce_source=ce_source, unknown_caller=unknown_caller,
                               wrapper86=wrapper86))
    for seq, e in enumerate(rows[1:], 1):
        e["seq"] = seq
        e["timestampMs"] = seq + 1000 + shift
        e["threadId"] = 200 + shift
    rows.append({"kind": "capture_summary", "complete": not partial, "overwritten": 0,
                 "capturedDuels": len(indices), "eventsWritten": len(rows)-1,
                 "rngSeedOverridesApplied": len(indices)})
    z.writestr(member, '\n'.join(json.dumps(e) for e in rows) + '\n')


class CompareTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.a = Path(self.tmp.name) / 'first.zip'
        self.b = Path(self.tmp.name) / 'second.zip'
        with ZipFile(self.a, 'w') as z:
            write_capture(z, 'capture1.jsonl', [0, 1])
        with ZipFile(self.b, 'w') as z:
            write_capture(z, 'capture1.jsonl', [0, 1], shift=10)

    def test_matches_observable_with_raw_high_difference(self):
        report = compare_runs(self.a, self.b)
        self.assertEqual(report['totalDuelsCompared'], 2)
        self.assertTrue(report['observedMatch'])
        self.assertFalse(report['rawCommandArgsMatch'])
        self.assertEqual(report['totalChangedRawCommands'], 2)
        self.assertEqual(report['totalHigh16OnlyChangedCommands'], 2)
        self.assertTrue(report['duels'][0]['checks']['sameDefaultBranchQueuePackets'])
        self.assertEqual(
            report['duels'][0]['counts']['defaultBranchQueuePacketsLeft'], 1)
        self.assertEqual(report['totalUnverifiedRawDifferenceCommands'], 0)
        self.assertEqual(report['highWordOrigins'],
                         {'verified_card_id_pointer_residue': 2})
        self.assertEqual([x['globalDuelIndex'] for x in report['duels']], [0, 1])
        self.assertEqual(main([str(self.a), str(self.b)]), 0)
        self.assertEqual(main([str(self.a), str(self.b), '--strict-raw']), 1)
        self.assertEqual(main([str(self.a), str(self.b), '--strict-unverified']), 0)

    def test_strict_unverified_fails_on_same_low_word_at_unknown_callsite(self):
        with ZipFile(self.a, 'w') as z:
            write_capture(z, 'a.jsonl', [0, 1], unknown_caller=True)
        with ZipFile(self.b, 'w') as z:
            write_capture(z, 'b.jsonl', [0, 1], shift=10,
                          unknown_caller=True)
        report = compare_runs(self.a, self.b)
        self.assertTrue(report['observedMatch'])
        self.assertEqual(report['totalUnverifiedRawDifferenceCommands'], 2)
        self.assertEqual(report['highWordOrigins'], {'unresolved_high16': 2})
        self.assertEqual(main([str(self.a), str(self.b), '--strict-unverified']), 1)

    def test_footer_must_be_complete(self):
        with ZipFile(self.b, 'w') as z:
            write_capture(z, 'bad.jsonl', [0,1], partial=True)
        with self.assertRaisesRegex(ValueError, 'Incomplete'):
            compare_runs(self.a, self.b)

    def test_swapped_uneven_worker_allocation(self):
        with ZipFile(self.a, 'w') as z:
            write_capture(z,'w1.jsonl',[0,1])
            write_capture(z,'w2.jsonl',[2])
        with ZipFile(self.b, 'w') as z:
            write_capture(z,'w1.jsonl',[0])
            write_capture(z,'w2.jsonl',[1,2])
        report=compare_runs(self.a, self.b)
        self.assertEqual(report['totalDuelsCompared'],3)
        self.assertEqual([x['globalDuelIndex'] for x in report['duels']],[0,1,2])

    def test_new_passive_wrapper_86_caller_is_compared_when_recorded(self):
        with ZipFile(self.a, 'w') as z:
            write_capture(z, 'a.jsonl', [0, 1], wrapper86="0x5797ad")
        with ZipFile(self.b, 'w') as z:
            write_capture(z, 'b.jsonl', [0, 1], shift=10,
                          wrapper86="0x5b85b0")
        result = compare_runs(self.a, self.b)
        self.assertFalse(result['observedMatch'])
        self.assertFalse(result['duels'][0]['checks']['same86WrapperCallers'])
        self.assertEqual(result['duels'][0]['counts']['wrapper86CallersLeft'],
                         ['0x5797ad'])
        self.assertEqual(result['duels'][0]['counts']['wrapper86CallersRight'],
                         ['0x5b85b0'])

    def test_backward_compatible_with_old_captures_without_wrapper_caller(self):
        result = compare_runs(self.a, self.b)
        self.assertTrue(result['duels'][0]['checks']['same86WrapperCallers'])
        self.assertEqual(result['duels'][0]['counts']['wrapper86CallersLeft'],
                         [None])

    def test_changed_native_placement_descriptor_is_divergent(self):
        with ZipFile(self.b, 'w') as z:
            write_capture(z, 'capture1.jsonl', [0, 1], shift=10, ce_source=42)
        result = compare_runs(self.a, self.b)
        self.assertFalse(result['observedMatch'])
        self.assertFalse(result['duels'][0]['checks']['sameCeNativeStates'])

    def test_incompatible_seed_rejected(self):
        with ZipFile(self.b,'w') as z:
            write_capture(z,'capture1.jsonl',[0,1],base=12346)
        with self.assertRaisesRegex(ValueError,'Incompatible'):
            compare_runs(self.a, self.b)

    def test_missing_seed_rejected(self):
        with ZipFile(self.b, 'w') as z:
            z.writestr('bad.jsonl','{"kind":"metadata"}\n')
        with self.assertRaisesRegex(ValueError,'requires --seed'):
            compare_runs(self.a,self.b)


if __name__ == '__main__':
    unittest.main()
