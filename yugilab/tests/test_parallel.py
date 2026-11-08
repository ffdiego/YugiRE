"""Offline tests for parallel YugiLab scheduling, verification and failures."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from yugilab.parallel import distribute_duels, run_parallel


class ParallelLabTests(unittest.TestCase):
    def _run(self, total: int, workers: int, broken: int | None = None):
        spawned = []
        with tempfile.TemporaryDirectory() as tmp:
            args = argparse.Namespace(
                exe=Path("C:/Joey/joey_pc.exe"), duels=total, workers=workers,
                speed_value=2400, deck_0=Path("A.json"), deck_1=Path("B.json"),
                trace_ai=True, stagger_seconds=0.0)
            class FakePopen:
                def __init__(self, argv, stdout, stderr, cwd, **kwargs):
                    self.argv = argv
                    self.pid = 10000 + len(spawned) + 1
                    self.returncode = (2 if broken == len(spawned) + 1 else 0)
                    target = int(argv[argv.index("--duels") + 1])
                    folder = Path(argv[argv.index("--output-dir") + 1])
                    folder.mkdir(parents=True, exist_ok=True)
                    capture = folder / f"duels-20000101-pid{self.pid}.jsonl"
                    header = {"kind": "metadata", "processId": self.pid,
                              "requestedDuels": target}
                    summary = {"kind": "capture_summary",
                               "capturedDuels": target,
                               "eventsWritten": target * 40,
                               "complete": True, "overwritten": 0}
                    capture.write_text(json.dumps(header) + "\n" +
                                       json.dumps(summary) + "\n",
                                       encoding="utf-8")
                    stdout.write(f"[native-rules] Spawned suspended Joey PID {self.pid}\n")
                    stdout.write(f"[lab] {target}/{target} duels (100.0%)\n")
                    stdout.flush()
                    spawned.append(self)
                def poll(self):
                    return self.returncode
                def wait(self, timeout=None):
                    return self.returncode

            with patch("yugilab.parallel.subprocess.Popen", FakePopen):
                result = run_parallel(args, Path(tmp))
            manifests = list(Path(tmp).glob("parallel-*/manifest.json"))
            self.assertEqual(len(manifests), 1)
            manifest = json.loads(manifests[0].read_text(encoding="utf-8"))
            worker_argv = [p.argv for p in spawned]
            return result, manifest, worker_argv

    def test_balanced_allocation(self):
        self.assertEqual(distribute_duels(100, 10), [10] * 10)
        self.assertEqual(distribute_duels(23, 4), [6, 6, 6, 5])
        with self.assertRaises(ValueError):
            distribute_duels(2, 3)

    def test_ten_workers_create_ten_verified_captures(self):
        result, manifest, launched = self._run(100, 10)
        self.assertEqual(result, 0)
        self.assertTrue(manifest["complete"])
        self.assertEqual(manifest["capturedDuels"], 100)
        self.assertEqual(len(manifest["workers"]), 10)
        self.assertEqual(len(set(row["processId"] for row in manifest["workers"])), 10)
        self.assertEqual(manifest["eventsWritten"], 4000)
        for args in launched:
            self.assertIn("--allow-multi-instance", args)
            self.assertIn("--trace-ai", args)
            self.assertIn("--cancel-file", args)
            self.assertIn("--output-dir", args)
            self.assertEqual(args[args.index("--duels") + 1], "10")
            self.assertEqual(args[:3], [args[0], "-m", "yugilab"])

    def test_failed_worker_keeps_all_data_but_never_claims_success(self):
        code, manifest, launched = self._run(23, 4, broken=2)
        self.assertEqual(code, 2)
        self.assertFalse(manifest["complete"])
        self.assertEqual(manifest["status"], "failed")
        self.assertEqual([int(cmd[cmd.index("--duels") + 1]) for cmd in launched],
                         [6, 6, 6, 5])
        self.assertEqual(manifest["workers"][1]["exitCode"], 2)


if __name__ == "__main__":
    unittest.main()
