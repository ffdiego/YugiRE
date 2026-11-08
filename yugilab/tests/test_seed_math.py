"""Reference tests for the confirmed Joey 32-bit CRT RNG and YugiLab mixer."""
from __future__ import annotations

import unittest

from yugilab.seed_math import derive_duel_seed, msvc_rand_step


class SeedMathTests(unittest.TestCase):
    def test_mixer_matches_javascript_test_vectors(self):
        vectors = [
            (0, 0, 0), (0, 1, 1753845952), (0, 2, 3507691905),
            (12345, 0, 2435775735),
            (12345, 1, 4228375765),
            (12345, 2, 1634509376),
            (0xFFFFFFFF, 0, 1734902346),
            (0xFFFFFFFF, 1, 0),
        ]
        for base, index, expected in vectors:
            with self.subTest(base=base, index=index):
                self.assertEqual(derive_duel_seed(base, index), expected)

    def test_distinct_indices_have_distinct_seeds(self):
        seeds = {derive_duel_seed(12345, index) for index in range(20000)}
        self.assertEqual(len(seeds), 20000)

    def test_exact_msvc_rand_formula(self):
        state = 1
        for _ in range(1000):
            state, value = msvc_rand_step(state)
            self.assertTrue(0 <= value <= 32767)
        # Known first step from state=1.
        self.assertEqual(msvc_rand_step(1), (0x0029E2C0, 41))
        self.assertEqual(msvc_rand_step(0), (0x00269EC3, 38))

    def test_rejects_invalid_seeds(self):
        for value in (-1, 0x100000000, 1.5, True):
            with self.subTest(value=value):
                with self.assertRaises(ValueError):
                    derive_duel_seed(value, 0)
                with self.assertRaises(ValueError):
                    msvc_rand_step(value)


if __name__ == "__main__":
    unittest.main()
