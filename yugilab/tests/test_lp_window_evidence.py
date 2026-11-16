"""Synthetic validation of imported native LP windows, not rule parity."""
from __future__ import annotations
import copy
import unittest

from yugilab.lp_window_evidence import correlate_rain_lp_windows


class RainWindowEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.capture = dict(file="duels-a.jsonl", sha256="a"*64, events=200,
                            terminalCommands=2, integrityOk=True)
        self.examples = {1315: [
            dict(file="duels-a.jsonl", duelIndex=0, seq=335, kind="CF")
        ]}
        self.windows = dict(schema="yugire-native-cf-windows-v1",
                            captures=[dict(file="duels-a.jsonl", sha256="a"*64,
                                           events=200, terminalCommands=2)],
                            windows=[dict(capture="duels-a.jsonl",
                                sourceSha256="a"*64, resourceId=1315,
                                cfSeq=335, duelIndex=0,
                                pairedLp42Candidate=dict(
                                    kind="PairedNative0x42Commands",
                                    caller="0x578823", commandSeq=[338,339],
                                    targetSides=[0,1], amounts=[1000,1000],
                                    beforeCfLpSample=dict(seq=311,
                                        lifePoints=[6600,2600], commandsBetween=21),
                                    firstLpSampleAfterPair=dict(seq=340,
                                        lifePoints=[7600,3600]),
                                    netLpDeltaBetweenSamples=[1000,1000],
                                    otherLpCommandsBetweenSamples=[]),
                                lpChangesWithinWindow=[
                                    dict(delta=[-300,0], beforeSeq=346, afterSeq=348)]
                            )])

    def test_bound_net_gain_not_garbage_discard_trigger(self):
        found = correlate_rain_lp_windows(self.windows,[self.capture], self.examples)
        self.assertEqual(len(found[1315]),1)
        case=found[1315][0]
        self.assertEqual(case["observedNetDelta"],[1000,1000])
        self.assertEqual(case["pairCommandSeq"],[338,339])
        self.assertEqual(case["commandsBetweenPreviousSampleAndCf"],21)
        self.assertNotIn("Skull",str(found))
        self.assertIn("not",case["limitation"])

    def test_rejects_provenance_mismatch_and_forged_cf(self):
        altered=copy.deepcopy(self.windows)
        altered["captures"][0]["sha256"]="b"*64
        with self.assertRaisesRegex(ValueError,"provenance"):
            correlate_rain_lp_windows(altered,[self.capture], self.examples)
        altered=copy.deepcopy(self.windows)
        altered["windows"][0]["cfSeq"]=1200
        with self.assertRaisesRegex(ValueError,"no matching native CF"):
            correlate_rain_lp_windows(altered,[self.capture], self.examples)
        altered=copy.deepcopy(self.windows)
        altered["windows"][0]["sourceSha256"]="b"*64
        with self.assertRaisesRegex(ValueError,"source hash"):
            correlate_rain_lp_windows(altered,[self.capture], self.examples)

    def test_does_not_promote_unmeasured_or_confounded_lp(self):
        altered=copy.deepcopy(self.windows)
        altered["windows"][0]["pairedLp42Candidate"]["firstLpSampleAfterPair"]=None
        self.assertEqual(
            correlate_rain_lp_windows(altered,[self.capture],self.examples),{})
        altered=copy.deepcopy(self.windows)
        altered["windows"][0]["pairedLp42Candidate"]["otherLpCommandsBetweenSamples"]=[337]
        self.assertEqual(
            correlate_rain_lp_windows(altered,[self.capture],self.examples),{})
        altered=copy.deepcopy(self.windows)
        altered["windows"][0]["pairedLp42Candidate"]["netLpDeltaBetweenSamples"]=[500,1000]
        self.assertEqual(
            correlate_rain_lp_windows(altered,[self.capture],self.examples),{})

    def test_no_repeated_window_and_capture_not_integral(self):
        altered=copy.deepcopy(self.windows)
        altered["windows"].append(copy.deepcopy(altered["windows"][0]))
        with self.assertRaisesRegex(ValueError,"Duplicate"):
            correlate_rain_lp_windows(altered,[self.capture],self.examples)
        altered=copy.deepcopy(self.capture)
        altered["integrityOk"]=False
        with self.assertRaisesRegex(ValueError,"provenance"):
            correlate_rain_lp_windows(self.windows,[altered],self.examples)


if __name__=="__main__":
    unittest.main()
