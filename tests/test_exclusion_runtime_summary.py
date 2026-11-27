"""Offline contract checks for V26 lifetime detection."""
import importlib.util
from pathlib import Path
import unittest

SCRIPT = Path(__file__).resolve().parents[1] / "scripts/pending/34-attack-draw-runtime.py"
SPEC = importlib.util.spec_from_file_location("visual_runtime", SCRIPT)
runtime = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(runtime)


class ExclusionRuntimeSummaryTests(unittest.TestCase):
    def fixture(self):
        start = dict(kind="exclusion-construct", id=1, counter=64, active=True,
                     descriptor=0, returnRva=0x1234, hiddenArgumentZero=True)
        return [start, dict(kind="exclusion-gate", id=1, blocks=False)] + [
            dict(kind="exclusion-draw", id=1, before=64+i*8, counter=72+i*8,
                 active=i<23, descriptor=0, hiddenArgumentZero=True)
            for i in range(24)
        ]

    def test_full_draw_lifetime(self):
        self.assertTrue(runtime.observed_exclusion(self.fixture()))

    def test_incomplete_or_wrong_identity(self):
        self.assertFalse(runtime.observed_exclusion(self.fixture()[:-1]))
        self.assertFalse(runtime.observed_exclusion(self.fixture()[1:]))
        wrong = self.fixture()
        wrong[-1]["descriptor"] = 2
        self.assertFalse(runtime.observed_exclusion(wrong))

    def test_blocking_and_hidden_argument(self):
        blocking = self.fixture()
        blocking[1]["blocks"] = True
        self.assertFalse(runtime.observed_exclusion(blocking))
        leaked = self.fixture()
        leaked[0]["hiddenArgumentZero"] = False
        self.assertFalse(runtime.observed_exclusion(leaked))

    def test_movement_not_evidence(self):
        self.assertFalse(runtime.observed_exclusion([
            dict(kind="exclusion-context-transfer", source=0, destination=30)
        ]))


if __name__ == "__main__":
    unittest.main()
