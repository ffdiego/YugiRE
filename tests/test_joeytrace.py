"""Independent, offline JoeyTrace tests. No original executable or replay required."""
from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import struct
import sys
import tempfile
import unittest

HERE = Path(__file__).resolve().parents[1] / "scripts"
sys.path.insert(0, str(HERE))
import joeytrace_core as core
import joeytrace


def make_block(*, lp0=8000, lp1=8000, hand0=5, hand1=5) -> bytearray:
    raw = bytearray(core.BLOCK_SIZE)
    for side, lp, hand in ((0, lp0, hand0), (1, lp1, hand1)):
        struct.pack_into("<H", raw, side * core.PLAYER_STRIDE, lp)
        raw[side * core.PLAYER_STRIDE + 4] = hand
    return raw


def set_monster(raw: bytearray, side: int, zone: int, index: int,
                occurrence: int = 5, position: int = 0x202, flags: int = 0x30000):
    offset = side * core.PLAYER_STRIDE + core.MONSTER_START + zone * core.MONSTER_STRIDE
    struct.pack_into("<I", raw, offset, index)
    struct.pack_into("<H", raw, offset + 4, occurrence)
    struct.pack_into("<H", raw, offset + 6, position)
    struct.pack_into("<I", raw, offset + 0x8C, flags)


class StateTests(unittest.TestCase):
    def test_layout_matches_known_snapshot(self):
        self.assertEqual(core.BLOCK_SIZE, 6792)
        self.assertEqual(core.PLAYER_FLAGS_OFFSET, 0x6CC)
        raw = make_block(lp0=400, lp1=7900, hand0=6, hand1=2)
        struct.pack_into("<I", raw, core.PLAYER_FLAGS_OFFSET, 0x00090000)
        set_monster(raw, 0, 0, 550, 29, 0x0202, 0x00030000)
        state = core.parse_state(bytes(raw))
        self.assertEqual(state["players"][0]["life_points"], 400)
        self.assertEqual(state["players"][0]["hand_count"], 6)
        self.assertEqual(state["players"][0]["player_flags_hex"], "00090000")
        monster = state["players"][0]["monsters"][0]
        self.assertEqual(monster["card_index"], 550)
        self.assertEqual(monster["occurrence_word"], 29)
        self.assertEqual(monster["position_word_hex"], "0202")
        self.assertEqual(monster["flags_hex"], "00030000")
        self.assertTrue(monster["position_locked_this_turn"])
        self.assertFalse(state["players"][0]["monsters"][1]["occupied"])

    def test_invalid_size(self):
        with self.assertRaisesRegex(ValueError, "Expected"):
            core.parse_state(b"short")

    def test_duel_heuristic_and_zero_life(self):
        self.assertTrue(core.plausible_duel(core.parse_state(bytes(make_block()))))
        self.assertFalse(core.plausible_duel(core.parse_state(bytes(make_block(lp0=0)))))

    def test_diffs_are_observations_not_assumed_actions(self):
        before = make_block()
        after = bytearray(before)
        after[core.PLAYER_STRIDE + 4] = 4
        set_monster(after, 1, 0, 550, 29, 0x0202, 0x00030000)
        changes = core.diff_states(core.parse_state(bytes(before)), core.parse_state(bytes(after)))
        self.assertEqual({c["type"] for c in changes}, {
            "hand_count_changed", "monster_identity_changed", "monster_position_changed",
            "monster_flags_changed",
        })
        self.assertEqual(changes[0]["side"], 1)
        self.assertNotIn("summon", json.dumps(changes))

    def test_life_delta_and_bit_xor(self):
        old = make_block(lp0=50)
        new = make_block(lp0=0)
        struct.pack_into("<I", new, core.PLAYER_FLAGS_OFFSET, 0x21000)
        changes = core.diff_states(core.parse_state(bytes(old)), core.parse_state(bytes(new)))
        self.assertEqual(changes[0]["delta"], -50)
        self.assertEqual(changes[1]["xor_hex"], "00021000")

    def test_diff_bytes_for_unknown_fields(self):
        old = make_block()
        new = bytearray(old)
        new[0x900] = 15
        new[0x901] = 16
        diff = core.raw_byte_diff(bytes(old), bytes(new), limit=1)
        self.assertEqual(diff["count"], 2)
        self.assertEqual(diff["sample"][0]["block_offset_hex"], "0x0900")
        self.assertTrue(diff["sample_truncated"])


class SessionTests(unittest.TestCase):
    def test_baseline_dedup_markers_and_report(self):
        with tempfile.TemporaryDirectory() as d:
            folder = Path(d) / "duel"
            session = core.TraceSession(folder, {"synthetic": True}, raw_blocks=True)
            raw = bytes(make_block())
            self.assertFalse(session.sample(raw, 0, "2026-01-01T00:00:00Z"))
            self.assertFalse(session.sample(raw, 100, "2026-01-01T00:00:00.100Z"))
            modified = bytearray(raw)
            struct.pack_into("<H", modified, 0, 6500)
            self.assertTrue(session.sample(bytes(modified), 200, "2026-01-01T00:00:00.200Z"))
            session.marker(210, "Ataque observado", "console")
            report = session.finish("test_complete", 300)
            self.assertTrue(report.exists())
            manifest = json.loads((folder / "manifest.json").read_text())
            self.assertEqual(manifest["change_batches"], 1)
            self.assertEqual(manifest["poll_count"], 3)
            self.assertEqual(manifest["event_counts"]["life_points_changed"], 1)
            changes = [json.loads(x) for x in (folder / "state-changes.jsonl").read_text().splitlines()]
            self.assertEqual(changes[0]["changes"][0]["delta"], -1500)
            self.assertEqual(len((folder / "snapshots.jsonl").read_text().splitlines()), 2)
            self.assertTrue((folder / "raw-snapshots.bin").read_bytes().startswith(b"JTRC1"))
            self.assertEqual((folder / "raw-snapshots.bin").stat().st_size, 8 + 2 * (16 + core.BLOCK_SIZE))
            self.assertEqual(len((folder / "markers.jsonl").read_text().splitlines()), 1)
            with self.assertRaises(FileExistsError):
                core.TraceSession(folder, {})

    def test_unknown_only_requires_opt_in(self):
        for tracking in (False, True):
            with self.subTest(tracking=tracking), tempfile.TemporaryDirectory() as d:
                session = core.TraceSession(Path(d) / "duel", {}, track_unknown=tracking)
                raw = make_block()
                session.sample(bytes(raw), 0)
                raw[0x900] = 1
                changed = session.sample(bytes(raw), 100)
                self.assertEqual(changed, tracking)
                self.assertEqual(session.change_batches, int(tracking))
                session.finish("test_complete", 200)

    def test_annotate_marker_after_capture(self):
        with tempfile.TemporaryDirectory() as d:
            path = Path(d) / "duel"
            s = core.TraceSession(path, {})
            s.sample(bytes(make_block()), 0)
            s.marker(15, "", "F9")
            s.finish("test_complete", 20)
            result = joeytrace.main(["annotate", "--session", str(path), "--marker", "1",
                                     "--text", "Monstro equipado virado para baixo"])
            self.assertEqual(result, 0)
            annotation = json.loads((path / "marker-annotations.jsonl").read_text().splitlines()[0])
            self.assertEqual(annotation["marker_index"], 1)
            self.assertIn("virado para baixo", annotation["note"])

    def test_import_synthetic_replay(self):
        with tempfile.TemporaryDirectory() as d:
            root = Path(d)
            folder = root / "duel"
            s = core.TraceSession(folder, {})
            s.sample(bytes(make_block()), 0)
            s.finish("test_complete", 10)
            replay_path = root / "sample.ydr"
            data = bytearray(struct.pack("<4s8s4I", b"YGR\0", b"ABCDEFGH", 1300, 1, 1, 12345))
            for group in ((730,), (), (), (1123,), (), ()):
                data += struct.pack("<H", len(group))
                if group:
                    data += struct.pack("<H", group[0])
            data += struct.pack("<I", 2)
            data += struct.pack("<4H", 0x8001, 0, 0, 0)
            data += struct.pack("<4H", 0x8004, 0, 0, 0)
            replay_path.write_bytes(data)
            self.assertEqual(joeytrace.main(["import-replay", "--session", str(folder),
                                             "--file", str(replay_path)]), 0)
            summary = json.loads((folder / "replay-summary.json").read_text())
            self.assertEqual(summary["header"]["save_identifier_hex"], "REDACTED")
            self.assertEqual(summary["record_count"], 2)
            self.assertEqual((folder / "replay.ydr").read_bytes(), data)
            self.assertEqual(len((folder / "replay-commands.csv").read_text().splitlines()), 3)

    def test_offline_demo(self):
        with tempfile.TemporaryDirectory() as d:
            path = Path(d) / "demo"
            result = joeytrace.main(["demo", "--output", str(path)])
            self.assertEqual(result, 0)
            summary = json.loads((path / "manifest.json").read_text())
            self.assertTrue(summary["synthetic_demo"])
            self.assertEqual(summary["change_batches"], 2)
            self.assertEqual(summary["marker_count"], 1)
            self.assertFalse(summary["native_command_hook_enabled"])


if __name__ == "__main__":
    unittest.main()
