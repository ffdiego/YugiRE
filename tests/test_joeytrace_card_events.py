"""Offline tests for generic observed card events and per-marker windows."""
from __future__ import annotations

import json
from pathlib import Path
import struct
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from joeytrace_core import (
    BLOCK_SIZE, PLAYER_STRIDE, MONSTER_START, MONSTER_STRIDE,
    FIELD_SLOT_COUNT, parse_state, diff_states, field_slot_byte_diffs, TraceSession,
)
from joeytrace_effects import generate_effect_windows


def block():
    raw = bytearray(BLOCK_SIZE)
    for side in (0, 1):
        struct.pack_into("<H", raw, side * PLAYER_STRIDE, 8000)
        raw[side * PLAYER_STRIDE + 4] = 5
    return raw


def set_card(raw, side, slot, index, occurrence=3, flags=0):
    pos = side * PLAYER_STRIDE + MONSTER_START + slot * MONSTER_STRIDE
    struct.pack_into("<I", raw, pos, index)
    struct.pack_into("<H", raw, pos+4, occurrence)
    struct.pack_into("<I", raw, pos+0x8C, flags)
    return pos


class CardEventsTests(unittest.TestCase):
    def test_eleven_slots_only_in_optin_mode(self):
        raw = block()
        set_card(raw, 0, 10, 454)
        regular = parse_state(bytes(raw))
        self.assertNotIn("field_slots", regular["players"][0])
        extended = parse_state(bytes(raw), include_card_events=True)
        self.assertEqual(len(extended["players"][0]["field_slots"]), FIELD_SLOT_COUNT)
        self.assertEqual(extended["players"][0]["field_slots"][10]["card_index"], 454)
        self.assertEqual(extended["players"][0]["field_slots"][10]["region_hint"], "other_field")
        self.assertIn("hand_cards", extended["players"][0])

    def test_placing_nonmonster_and_deck_count(self):
        old = block()
        new = bytearray(old)
        set_card(new, 0, 6, 291)
        new[5] = 32
        before = parse_state(bytes(old), include_card_events=True)
        after = parse_state(bytes(new), include_card_events=True)
        types = {change["type"] for change in diff_states(before, after)}
        self.assertIn("field_slot_card_index_changed", types)
        self.assertIn("deck_count_changed", types)

    def test_association_count_and_metadata(self):
        old = block()
        after = bytearray(old)
        pos = set_card(after, 1, 0, 550)
        struct.pack_into("<H", after, pos + 0x0A, 1)
        struct.pack_into("<H", after, pos + 0x0C, 0x0301)
        struct.pack_into("<H", after, pos + 0x4C, 0x0001)
        parsed = parse_state(bytes(after), include_card_events=True)
        association = parsed["players"][1]["field_slots"][0]["associations"][0]
        self.assertEqual(association["kind_nibble"], 1)
        self.assertEqual(association["reference_word_hex"], "0301")
        types = {x["type"] for x in diff_states(
            parse_state(bytes(old), include_card_events=True), parsed)}
        self.assertIn("field_slot_associations_changed", types)

    def test_invalid_association_count_does_not_read_past_record(self):
        raw = block()
        pos = set_card(raw, 0, 0, 550)
        struct.pack_into("<H", raw, pos+0x0A, 65535)
        slot = parse_state(bytes(raw), include_card_events=True)["players"][0]["field_slots"][0]
        self.assertFalse(slot["association_count_valid"])
        self.assertEqual(slot["associations"], [])

    def test_card_local_raw_diff_preserves_unknown_offsets(self):
        before = block()
        after = bytearray(before)
        pos = set_card(after, 0, 10, 290)
        after[pos + 0x48] = 0x7F
        entries = field_slot_byte_diffs(bytes(before), bytes(after))
        self.assertEqual(len(entries), 1)
        self.assertEqual(entries[0]["slot"], 10)
        self.assertTrue(any(x["slot_offset_hex"] == "0x48"
                            for x in entries[0]["byte_samples"]))

    def test_legacy_session_without_card_events_unchanged(self):
        with tempfile.TemporaryDirectory() as tmp:
            session = TraceSession(Path(tmp) / "duel", {})
            original = bytes(block())
            session.sample(original, 0)
            altered = bytearray(original)
            set_card(altered, 0, 6, 291)
            self.assertFalse(session.sample(bytes(altered), 100))
            session.finish("test_complete", 200)

    def test_effect_windows_attach_annotations_and_temporal_diffs(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp) / "duel"
            session = TraceSession(folder, {}, capture_card_events=True)
            original = bytes(block())
            session.sample(original, 0)
            changed = bytearray(original)
            set_card(changed, 1, 5, 291)
            session.sample(bytes(changed), 3000)
            session.marker(3100)
            session.annotate_marker(1, "Ativei uma carta de efeito")
            changed[PLAYER_STRIDE + 0x10 + 5 * MONSTER_STRIDE] = 0
            session.sample(bytes(changed), 4000)
            session.finish("test_complete", 5000)
            path = generate_effect_windows(folder, before_ms=200, after_ms=1500)
            obj = json.loads(path.read_text(encoding="utf-8"))
            self.assertEqual(obj["marker_count"], 1)
            win = obj["windows"][0]
            self.assertEqual(win["operator_notes"], ["Ativei uma carta de efeito"])
            self.assertEqual(win["total_batches"], 2)
            self.assertIn(291, win["card_indexes_observed"])
            self.assertIn("field_slot_card_index_changed", win["change_type_counts"])
            self.assertIn("no card causality proven", obj["interpretation_limit"])


if __name__ == "__main__":
    unittest.main()
