"""Offline fixtures: selectors disambiguate duplicate monster indices."""
import copy
import unittest

from yugilab.battle_actor_projection import project_battle_actors


def fixture(attacker=1, direct=False):
    target = 1 - attacker
    selector = attacker | (target << 1) | (4 if direct else 0) | (3 << 8) | (2 << 11)
    sides = [dict(fieldSlots=[dict(cardIndex=0, positionWord=0, occurrenceWord=0)
                              for _ in range(11)]) for _ in range(2)]
    for side, slot, pose, occ in [(attacker, 3, 2, 17), (target, 2, 1, 91)]:
        sides[side]["fieldSlots"][slot].update(cardIndex=81, positionWord=pose, occurrenceWord=occ)
    sides[attacker]["fieldSlots"][0].update(cardIndex=81, positionWord=2, occurrenceWord=33)
    blocks = [None, None]
    for index, side, slot, pose in [(attacker, attacker, 3, 0), (1-attacker, target, 2, 0x20)]:
        blocks[index] = dict(blockIndex=index, descriptorWord=0xAB10 | slot << 1 | side | pose,
                             cardIndexWord=0 if direct and index != attacker else 81,
                             calculatedAttack=1200, calculatedDefense=800, damageWord=0)
    raw = dict(schema="joey-command-entry-battle-attributes-v1",
               samplePoint="command_producer_onEnter_0x31", nonAtomic=True,
               selectorWord=selector, blocks=blocks)
    return raw, sides


class BattleActorProjectionTests(unittest.TestCase):
    def test_both_attacker_sides_and_duplicate_indices(self):
        for attacker in (0, 1):
            raw, sides = fixture(attacker)
            a, b = project_battle_actors(raw, sides)["actors"]
            self.assertEqual((a["side"], a["slot"], a["blockIndex"], a["occurrenceWord"]),
                             (attacker, 3, attacker, 17))
            self.assertEqual((b["side"], b["slot"], b["blockIndex"], b["occurrenceWord"]),
                             (1-attacker, 2, 1-attacker, 91))
            self.assertEqual(a["status"], "field_correlated")
            self.assertEqual(b["status"], "field_correlated")
            self.assertTrue(b["faceDown"])
            self.assertTrue(b["defense"])
            self.assertEqual(a["rawBlock"], raw["blocks"][attacker])

    def test_direct_is_not_a_monster_even_with_occupied_selector_slot(self):
        for attacker in (0, 1):
            raw, sides = fixture(attacker, direct=True)
            result = project_battle_actors(raw, sides)
            self.assertTrue(result["direct"])
            self.assertEqual(result["actors"][1]["status"], "direct_target_absent")
            self.assertIsNone(result["actors"][1]["slot"])
            self.assertIsNone(result["actors"][1]["occurrenceWord"])

    def test_inconsistent_or_missing_field_is_not_correlated(self):
        raw, sides = fixture()
        a = project_battle_actors(raw, None)["actors"][0]
        self.assertEqual(a["status"], "no_field_sample")
        self.assertIsNone(a["occurrenceWord"])
        changed = copy.deepcopy(sides)
        changed[1]["fieldSlots"][3]["cardIndex"] = 99
        self.assertEqual(project_battle_actors(raw, changed)["actors"][0]["status"], "field_index_disagree")
        changed = copy.deepcopy(sides)
        changed[1]["fieldSlots"][3]["positionWord"] = 3
        self.assertEqual(project_battle_actors(raw, changed)["actors"][0]["status"], "field_pose_disagree")
        raw["blocks"][1]["descriptorWord"] ^= 1
        self.assertEqual(project_battle_actors(raw, sides)["actors"][0]["status"], "descriptor_selector_disagree")

    def test_rejects_bad_sampling_point_or_block_order(self):
        raw, sides = fixture()
        raw["blocks"].reverse()
        with self.assertRaisesRegex(ValueError, "blocks"):
            project_battle_actors(raw, sides)
        raw, sides = fixture()
        raw["nonAtomic"] = False
        with self.assertRaisesRegex(ValueError, "sample point"):
            project_battle_actors(raw, sides)
