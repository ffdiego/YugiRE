"""Offline corpus/runner integrity tests; NEVER launches Joey or Frida."""
from __future__ import annotations

from collections import Counter
from contextlib import redirect_stdout
import io
import json
from pathlib import Path
import tempfile
import unittest

from scripts.prepare_96_card_campaign import (
    CONTROLS, COPIES, FOCUS, QUOTAS, ROOT, prepare, shuffle,
)
from scripts.run_96_card_campaign import (
    PLAN_FILE, DECK_FOLDER, build_command, inspect_capture,
    load_plan, main as run_main, run_specs,
)


class Native96CardCampaignTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.plan, cls.decks = prepare()

    def test_96_unique_catalog_bound_resources(self):
        plan = self.plan
        self.assertEqual(plan["schema"], "yugire-native-96-card-exposure-v1")
        self.assertEqual(plan["distinctTargetCards"], 96)
        self.assertEqual(plan["cohortsCount"], 12)
        self.assertEqual(plan["totalPilotDuels"], 36)
        self.assertEqual(plan["evidenceStatus"], "PlannedDeckExposureOnly")
        self.assertEqual(len(plan["cohorts"]), 12)
        self.assertEqual(len(self.decks), 24)
        targets = [t for c in plan["cohorts"] for t in c["targets"]]
        self.assertEqual(len(targets), 96)
        self.assertEqual(len({t["resourceId"] for t in targets}), 96)
        self.assertEqual(len({t["internalIndex"] for t in targets}), 96)
        self.assertEqual(Counter(t["family"] for t in targets),
                         {"monster": 36, "spell": 36, "trap": 24})
        self.assertTrue(all(t["callbackProfile"].startswith("P") for t in targets))
        self.assertTrue(all(t["copiesPerSide"] == 3 for t in targets))

    def test_each_deck_is_40_cards_with_three_of_each_target_on_both_sides(self):
        for cohort in self.plan["cohorts"]:
            with self.subTest(cohort=cohort["number"]):
                self.assertEqual(Counter(t["family"] for t in cohort["targets"]),
                                 QUOTAS)
                targets = {t["internalIndex"] for t in cohort["targets"]}
                self.assertEqual(len(targets), 8)
                first, second = [
                    self.decks[cohort["decks"][side]]
                    for side in ("A", "B")
                ]
                for deck in (first, second):
                    self.assertEqual(set(deck), {"main", "second", "third"})
                    self.assertEqual(len(deck["main"]), 40)
                    self.assertEqual(deck["second"], [])
                    self.assertEqual(deck["third"], [])
                    counts = Counter(deck["main"])
                    self.assertTrue(all(1 <= x <= 4095 for x in counts))
                    self.assertLessEqual(max(counts.values()), 3)
                    self.assertEqual(set(counts), targets | set(CONTROLS))
                    for target in targets:
                        self.assertEqual(counts[target], COPIES)
                    for control in CONTROLS:
                        self.assertEqual(counts[control], 2)
                self.assertEqual(Counter(first["main"]), Counter(second["main"]))
                self.assertNotEqual(first["main"], second["main"])

    def test_committed_decks_are_reproduced_from_catalog_and_same_seed(self):
        self.assertEqual(load_plan(), self.plan)
        self.assertEqual(json.loads(PLAN_FILE.read_text(encoding="utf-8")),
                         self.plan)
        for name, deck in self.decks.items():
            with self.subTest(name=name):
                self.assertEqual(
                    json.loads((DECK_FOLDER / name).read_text(encoding="utf-8")),
                    deck)
        self.assertEqual(shuffle([1, 2, 3, 4, 5], 12345),
                         shuffle([1, 2, 3, 4, 5], 12345))
        self.assertNotEqual(shuffle([1, 2, 3, 4, 5], 12345),
                            shuffle([1, 2, 3, 4, 5], 12346))

    def test_runner_is_bounded_and_state_focused(self):
        specs = run_specs(self.plan)
        self.assertEqual(len(specs), 12)
        self.assertEqual(sum(x["duels"] for x in specs), 36)
        self.assertEqual(len(run_specs(self.plan, repeats=5)), 60)
        self.assertEqual(sum(x["duels"] for x in run_specs(
            self.plan, repeats=5)), 180)
        selected = run_specs(self.plan, cohorts=[3, 1, 3], repeats=2,
                             duels=2, seed_offset=1000)
        self.assertEqual([x["cohort"] for x in selected], [1, 1, 3, 3])
        self.assertEqual(len({x["seed"] for x in selected}), 4)
        cmd = build_command(selected[0], exe=Path("joey_pc.exe"),
                            python=Path("python"),
                            output_dir=Path("scratch/isolated"))
        self.assertIn("--trace-state", cmd)
        self.assertNotIn("--trace-ai", cmd)
        self.assertEqual(cmd[cmd.index("--duels")+1], "2")
        self.assertEqual(cmd[cmd.index("--workers")+1], "1")
        for opts in (dict(duels=4), dict(repeats=21),
                     dict(cohorts=[13]), dict(seed_offset=-1)):
            with self.subTest(options=opts), self.assertRaises(ValueError):
                run_specs(self.plan, **opts)

    def test_runner_preview_never_requires_exe_and_never_executes(self):
        with tempfile.TemporaryDirectory() as temp:
            target = Path(temp) / "preview-no-exe-created"
            with redirect_stdout(io.StringIO()) as out:
                self.assertEqual(run_main(["--cohort", "1",
                    "--output-root", str(target)]), 0)
            self.assertIn("No native processes started", out.getvalue())
            self.assertIn("--trace-state", out.getvalue())
            self.assertFalse(target.exists())

    def test_footer_auditor_requires_verified_decks_on_both_sides(self):
        with tempfile.TemporaryDirectory() as temp:
            folder = Path(temp)
            self.assertFalse(inspect_capture(folder, 3)["complete"])
            file = folder / "capture.jsonl"
            header = dict(kind="metadata", requestedDuels=3,
                          deckPlans={
                              "0": {"main": [101, 102, 102]},
                              "1": {"main": [201, 202, 202]},
                          })
            verified = [
                dict(kind="deck_override", phase="verified",
                     side=side, actualIndices=header["deckPlans"][str(side)]["main"])
                for _ in range(3) for side in (0, 1)
            ]
            complete = dict(kind="capture_summary", complete=True,
                            capturedDuels=3, overwritten=0,
                            stateTrace=dict(samples=20, baselines=3, readErrors=0))

            def write(events=verified, footer=complete):
                file.write_text("\\n".join(json.dumps(item) for item in
                    [header, *events, footer])+"\\n", encoding="utf-8")

            write()
            valid = inspect_capture(folder, 3)
            self.assertTrue(valid["complete"])
            self.assertEqual(valid["verifiedDeckOverrides"], {"0": 3, "1": 3})
            for altered in ({"overwritten": 2}, {"capturedDuels": 2},
                            {"complete": False}, {"stateTrace": {"samples": 0}},
                            {"stateTrace": {"samples": 20, "baselines": 2,
                                            "readErrors": 0}}):
                with self.subTest(altered=altered):
                    write(footer=dict(complete, **altered))
                    self.assertFalse(inspect_capture(folder, 3)["complete"])

            write(events=verified[:-1])
            self.assertFalse(inspect_capture(folder, 3)["complete"],
                             "Missing side 1 verification must abort")
            failed = dict(kind="deck_override", phase="failed", side=0)
            write(events=verified+[failed])
            self.assertFalse(inspect_capture(folder, 3)["complete"],
                             "Native override failure must abort")
            mismatch = dict(verified[0], actualIndices=[999, 102, 102])
            write(events=[mismatch, *verified[1:]])
            self.assertFalse(inspect_capture(folder, 3)["complete"],
                             "Wrong actual deck contents must abort")


if __name__ == "__main__":
    unittest.main()
