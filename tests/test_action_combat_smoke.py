import importlib.util
from pathlib import Path
import tempfile
import unittest

PATH = Path(__file__).resolve().parents[1] / "scripts/regression/actions-a09-combat-smoke.py"
spec = importlib.util.spec_from_file_location("action_combat_smoke", PATH)
smoke = importlib.util.module_from_spec(spec)
spec.loader.exec_module(smoke)

COMMANDS = [
    "DeclareDirectAttackCommand", "DamageCommand",
    "DeclareTargetedAttackCommand", "SnapshotBattleRemovalCommand",
    "SnapshotBattleRemovalCommand", "FinishBattleRemovalCommand",
    "FinishBattleRemovalCommand"
]
IDLE = "queue busy=False pending=0 active=- decision=- fault=False"


def make_log(graphical):
    rows = []
    for seq, name in enumerate(COMMANDS, 1):
        if graphical:
            rows.append(f"[lab timing] ms={seq * 20} seq={seq} stage=Started command={name}")
        rows.append(f"{seq}: {name} Applied ")
        if graphical:
            rows.append(f"[lab timing] ms={seq * 20 + 10} seq={seq} stage=Completed command={name}")
    return "\n".join(rows + ["side 0 lp=6600", IDLE, "Script completed."]) + "\n"


class CombatSmokeChecks(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.write("headless", make_log(False))
        self.write("classic", make_log(True))
        self.write("instant", make_log(True))

    def write(self, mode, text):
        path = self.root / f"{mode}.txt"
        path.write_text(text, encoding="utf-8")
        return path

    def test_matching_profiles(self):
        smoke.compare(self.root)

    def test_state_difference(self):
        self.write("classic", make_log(True).replace("lp=6600", "lp=6599"))
        with self.assertRaisesRegex(ValueError, "trace/state mismatch"):
            smoke.compare(self.root)

    def test_incomplete_or_stalled(self):
        for text in (make_log(False).replace("Script completed.", ""),
                     make_log(False) + "[lab wait] ms=5000\n"):
            path = self.write("headless", text)
            with self.assertRaises(ValueError):
                smoke.inspect_log(path, False)

    def test_nonapplied_or_missing_removal(self):
        for text in (make_log(False).replace("Applied", "Rejected", 1),
                     make_log(False).replace("FinishBattleRemovalCommand", "DestroyCommand", 1),
                     make_log(False).replace("2: DamageCommand Applied \n", "")):
            path = self.write("headless", text)
            with self.assertRaises(ValueError):
                smoke.inspect_log(path, False)

    def test_next_command_before_previous_completes(self):
        text = make_log(True)
        old = "[lab timing] ms=30 seq=1 stage=Completed command=DeclareDirectAttackCommand\n"
        text = text.replace(old, "").replace("2: DamageCommand Applied ", old + "2: DamageCommand Applied ")
        path = self.write("classic", text)
        with self.assertRaisesRegex(ValueError, "next command"):
            smoke.inspect_log(path, True)

    def test_final_queue_must_be_idle(self):
        path = self.write("headless", make_log(False).replace(IDLE, IDLE + "\nqueue busy=True"))
        with self.assertRaisesRegex(ValueError, "idle"):
            smoke.inspect_log(path, False)


if __name__ == "__main__":
    unittest.main()
