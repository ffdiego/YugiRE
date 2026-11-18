"""Fast offline checks for scripts/duelos.py; no Frida or Joey required."""
from __future__ import annotations

from contextlib import redirect_stderr, redirect_stdout
import io
from pathlib import Path
import unittest

from scripts import duelos


class QuickDuelLauncherTests(unittest.TestCase):
    def test_short_command_defaults_to_three_state_duels(self):
        parser = duelos.build_parser()
        args = parser.parse_args([])
        cmd = duelos.build_command(args, Path("python"))
        self.assertEqual(cmd[cmd.index("--duels") + 1], "3")
        self.assertEqual(cmd[cmd.index("--workers") + 1], "1")
        self.assertEqual(cmd[cmd.index("--seed") + 1], "12345")
        self.assertIn("--trace-state", cmd)
        self.assertNotIn("--trace-battle-attributes", cmd)
        self.assertIn("demo-A.json", " ".join(cmd))
        self.assertIn("demo-B.json", " ".join(cmd))

    def test_custom_decks_state_and_battle_attributes(self):
        args = duelos.build_parser().parse_args(
            ["scratch/experiment-A.json", "scratch/experiment-B.json", "--battle-attributes"])
        cmd = duelos.build_command(args, Path("python"))
        self.assertIn("--trace-state", cmd)
        self.assertIn("--trace-battle-attributes", cmd)
        self.assertIn("experiment-A.json", cmd[cmd.index("--deck-0") + 1])
        self.assertIn("experiment-B.json", cmd[cmd.index("--deck-1") + 1])

    def test_large_run_requires_no_state_and_forwards_workers(self):
        with redirect_stdout(io.StringIO()) as out:
            self.assertEqual(duelos.main(["--duels", "10", "--workers", "2",
                                          "--no-state", "--dry-run"]), 0)
        cmd = out.getvalue()
        self.assertIn("--duels 10", cmd)
        self.assertIn("--workers 2", cmd)
        self.assertNotIn("--trace-state", cmd)

    def test_invalid_pilots_rejected_before_spawn(self):
        invalid = [["--duels", "4"], ["--workers", "2"],
                   ["--no-state", "--battle-attributes"],
                   ["--trace-ai"], ["--workers", "17", "--no-state"]]
        for opts in invalid:
            with self.subTest(opts=opts), redirect_stderr(io.StringIO()):
                with self.assertRaises(SystemExit):
                    duelos.main(opts + ["--dry-run"])


if __name__ == "__main__":
    unittest.main()
