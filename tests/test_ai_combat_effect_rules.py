"""Synthetic tests for a documented subset of the Joey AI effect helpers."""
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'scripts'))
from ai_combat_effect_rules import (
    normalized_resource_id, equivalent_resource_ids,
    equipment_deltas, halve_attack_repeatedly,
)


class CombatEffectRulesTests(unittest.TestCase):
    def test_normalization(self):
        self.assertEqual(normalized_resource_id(2034), 34)
        self.assertEqual(normalized_resource_id(1999), 1999)
        with self.assertRaises(ValueError):
            normalized_resource_id(-1)

    def test_equivalence_is_symmetric_with_selected_pairs(self):
        for a, b in ((0x3EB, 0x40A), (0x22, 0x4BA),
                     (0x3D, 0x4E1), (0x39, 0x76D), (0x4DD, 0x76E)):
            self.assertTrue(equivalent_resource_ids(a, b))
            self.assertTrue(equivalent_resource_ids(b, a))
        self.assertFalse(equivalent_resource_ids(0x22, 0x39))
        self.assertTrue(equivalent_resource_ids(0x22, 0x22 + 2000))

    def test_static_equipment_deltas(self):
        self.assertEqual(equipment_deltas(0x291), (300, 300))
        self.assertEqual(equipment_deltas(0x58C), (1000, -1000))
        self.assertEqual(equipment_deltas(0x422), (-500, -500))
        self.assertEqual(equipment_deltas(0x12F), (1000, 0))
        self.assertIsNone(equipment_deltas(0xFFFF))

    def test_equipment_conditions(self):
        self.assertEqual(equipment_deltas(0x12D, attribute_code=0x200), (400, -200))
        self.assertEqual(equipment_deltas(0x12D, attribute_code=0x100), (0, 0))
        self.assertEqual(equipment_deltas(0x49E, type_code=15), (700, 0))
        self.assertEqual(equipment_deltas(0x49E, type_code=14), (0, 0))
        self.assertEqual(equipment_deltas(0x604, monster_resource_id=0x53B), (300, 0))
        self.assertEqual(equipment_deltas(0x604), (0, 0))

    def test_repeated_halving_rounds_individually(self):
        self.assertEqual(halve_attack_repeatedly(2001, 0), 2001)
        self.assertEqual(halve_attack_repeatedly(2001, 1), 1001)
        self.assertEqual(halve_attack_repeatedly(2001, 2), 501)
        self.assertEqual(halve_attack_repeatedly(0, 3), 0)
        with self.assertRaises(ValueError):
            halve_attack_repeatedly(-1, 1)


if __name__ == '__main__':
    unittest.main()
