"""YugiLab CLI contract tests; do not spawn Joey or import Frida."""
from __future__ import annotations

from contextlib import redirect_stderr, redirect_stdout
import io
import unittest
from unittest.mock import patch

import yugilab


class YugiLabCliTests(unittest.TestCase):
    def test_defaults_to_owned_spawn_observer_and_2400(self):
        with patch.object(yugilab, "_run_native_launcher", return_value=0) as run, \
             redirect_stdout(io.StringIO()) as printed:
            result = yugilab.main(["--exe", "Z:/Joey/joey_pc.exe", "--duels", "100"])
        self.assertEqual(result, 0)
        run.assert_called_once_with([
            "--spawn", "--observe-only", "--exe", "Z:/Joey/joey_pc.exe",
            "--duels", "100", "--speed-value", "2400",
        ])
        self.assertIn("terminate only the Joey PID it spawns", printed.getvalue())

    def test_forwards_decks_ai_and_multi_instance(self):
        with patch.object(yugilab, "_run_native_launcher", return_value=9) as run, \
             redirect_stdout(io.StringIO()):
            result = yugilab.main([
                "--exe", "joey_pc.exe", "-n", "3", "--speed-value", "1200",
                "--deck-0", "A.json", "--deck-1", "B.json",
                "--trace-ai", "--allow-multi-instance",
            ])
        self.assertEqual(result, 9)
        run.assert_called_once_with([
            "--spawn", "--observe-only", "--exe", "joey_pc.exe",
            "--duels", "3", "--speed-value", "1200",
            "--deck-0", "A.json", "--deck-1", "B.json",
            "--trace-ai", "--allow-multi-instance",
        ])

    def test_seeded_demo_forwards_base_seed_and_offset(self):
        with patch.object(yugilab, "_run_native_launcher", return_value=0) as run, \
             redirect_stdout(io.StringIO()):
            result = yugilab.main([
                "--exe", "joey_pc.exe", "--duels", "3",
                "--seed", "0x3039", "--seed-start-index", "7"])
        self.assertEqual(result, 0)
        args = run.call_args.args[0]
        self.assertEqual(args[args.index("--seed") + 1], "12345")
        self.assertEqual(args[args.index("--seed-start-index") + 1], "7")

    def test_seed_requires_valid_uint32(self):
        for value in ["-1", "4294967296", "not-a-number"]:
            with self.subTest(seed=value), \
                 patch.object(yugilab, "_run_native_launcher") as run, \
                 redirect_stderr(io.StringIO()), \
                 self.assertRaises(SystemExit):
                yugilab.main(["--exe", "joey_pc.exe",
                              "--duels", "3", "--seed", value])
            run.assert_not_called()

    def test_rejects_unowned_pid_and_nonpositive_duels(self):
        for arg in [
            ["--exe", "joey_pc.exe", "--duels", "0"],
            ["--exe", "joey_pc.exe", "--duels", "-4"],
            ["--exe", "joey_pc.exe", "--duels", "3", "--pid", "1000"],
            ["--exe", "joey_pc.exe"],
            ["--duels", "3"],
        ]:
            with self.subTest(arg=arg), \
                 patch.object(yugilab, "_run_native_launcher") as run, \
                 redirect_stderr(io.StringIO()), \
                 self.assertRaises(SystemExit):
                yugilab.main(arg)
            run.assert_not_called()


if __name__ == "__main__":
    unittest.main()
