"""Offline regression tests for extended JoeyTrace observation data."""
from __future__ import annotations

import json
from pathlib import Path
import struct
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from joeytrace_core import (
    BLOCK_SIZE, PLAYER_STRIDE, HAND_ARRAY_OFFSET, DUEL_STATUS_OFFSET,
    EXODIA_CARD_INDEXES, parse_state, diff_states,
    is_possible_duel_reset, TraceSession,
)


def make_block(hand0=0, hand1=0, lp0=8000, lp1=8000):
    data = bytearray(BLOCK_SIZE)
    for side, hand, lp in ((0, hand0, lp0), (1, hand1, lp1)):
        pos = side * PLAYER_STRIDE
        struct.pack_into("<H", data, pos, lp)
        data[pos + 4] = hand
    return data


def write_hand(raw, side, indexes):
    pos = side * PLAYER_STRIDE
    raw[pos + 4] = len(indexes)
    for i, card in enumerate(indexes):
        struct.pack_into("<I", raw, pos + HAND_ARRAY_OFFSET + 4*i,
                         card | ((i+1) << 12))


class ExtendedObservationTests(unittest.TestCase):
    def test_hand_indices_not_logged_by_default(self):
        raw = make_block(hand1=5)
        write_hand(raw, 1, [774, 775, 776, 777, 778])
        normal = parse_state(bytes(raw))["players"][1]
        self.assertNotIn("hand_cards", normal)
        captured = parse_state(bytes(raw), include_hand_cards=True)["players"][1]
        self.assertEqual(captured["exodia_parts_present"], sorted(EXODIA_CARD_INDEXES))
        self.assertTrue(captured["exodia_all_parts_in_hand"])
        self.assertEqual(captured["hand_cards"][0]["card_index"], 774)

    def test_fifth_exodia_part_and_terminal_flag(self):
        before = make_block()
        write_hand(before, 1, [774, 775, 776, 777])
        after = bytearray(before)
        write_hand(after, 1, [774, 775, 776, 777, 778])
        struct.pack_into("<H", after, PLAYER_STRIDE + DUEL_STATUS_OFFSET, 0x200)
        changes = diff_states(
            parse_state(bytes(before), include_hand_cards=True),
            parse_state(bytes(after), include_hand_cards=True))
        hand = next(x for x in changes if x["type"] == "hand_cards_changed")
        flag = next(x for x in changes if x["type"] == "terminal_bits_changed")
        self.assertEqual([c["card_index"] for c in hand["added"]], [778])
        self.assertEqual(flag["became_set"], ["exodia"])

    def test_deckout_and_destiny_bits(self):
        raw = make_block()
        struct.pack_into("<H", raw, DUEL_STATUS_OFFSET, 0x500)
        flags = parse_state(bytes(raw))["players"][0]["terminal_bits"]
        self.assertTrue(flags["deck_out"])
        self.assertTrue(flags["destiny_board"])
        self.assertFalse(flags["exodia"])

    def test_possible_reset_is_tagged_without_data_loss(self):
        before = make_block(hand0=5, hand1=4)
        reset = make_block()
        self.assertTrue(is_possible_duel_reset(
            parse_state(bytes(before)), parse_state(bytes(reset))))
        with tempfile.TemporaryDirectory() as tmp:
            session = TraceSession(Path(tmp) / "session", {}, capture_hand_cards=True)
            session.sample(bytes(before), 0)
            session.sample(bytes(reset), 100)
            self.assertEqual(session.segment_id, 1)
            session.marker(110, "inicio visual")
            session.sample(bytes(make_block(hand0=5, hand1=5)), 200)
            session.finish("test_complete", 300)
            events = [json.loads(line) for line in
                      (session.folder / "state-changes.jsonl").read_text().splitlines()]
            self.assertEqual(events[0]["segment_id"], 1)
            self.assertTrue(any(x["type"] == "possible_duel_reset"
                                for x in events[0]["changes"]))
            self.assertEqual(events[1]["segment_id"], 1)
            manifest = json.loads((session.folder / "manifest.json").read_text())
            self.assertEqual(manifest["boundary_candidates"], 1)
            markers = [json.loads(x) for x in
                       (session.folder / "markers.jsonl").read_text().splitlines()]
            self.assertEqual(markers[0]["segment_id"], 1)

    def test_removing_monsters_does_not_falsely_trigger_reset(self):
        before = make_block(hand0=4, hand1=5)
        struct.pack_into("<I", before, 0x10, 591)
        after = make_block(hand0=4, hand1=5)
        self.assertFalse(is_possible_duel_reset(
            parse_state(bytes(before)), parse_state(bytes(after))))

    def test_change_of_card_with_constant_hand_count(self):
        before = make_block()
        write_hand(before, 0, [777, 774])
        after = bytearray(before)
        write_hand(after, 0, [777, 775])
        delta = diff_states(
            parse_state(bytes(before), include_hand_cards=True),
            parse_state(bytes(after), include_hand_cards=True))
        hand = next(x for x in delta if x["type"] == "hand_cards_changed")
        self.assertEqual([c["card_index"] for c in hand["removed"]], [774])
        self.assertEqual([c["card_index"] for c in hand["added"]], [775])


if __name__ == "__main__":
    unittest.main()
