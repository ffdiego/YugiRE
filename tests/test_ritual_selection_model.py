"""Independent subset enumeration against a static transcription, not native code."""
import itertools
from pathlib import Path
import sys
import unittest
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'scripts'))
from ritual_selection_model import candidate_can_finish, progress, subset_search


def oracle(levels, required, minimum, mandatory=()):
    for mask in range(1 << len(levels)):
        chosen = list(mandatory) + [level for i, level in enumerate(levels) if mask & (1 << i)]
        if chosen and required <= sum(chosen) < required + min(minimum, *chosen):
            return True
    return False


class RitualSelectionTests(unittest.TestCase):
    def test_recursive_search_against_subsets(self):
        # Positive remaining requirement; nonnegative levels include irrelevant zero-level cards.
        for count in range(5):
            for levels in itertools.product(range(4), repeat=count):
                for required in range(1, 9):
                    for minimum in (1, 2, 4, 255):
                        self.assertEqual(subset_search(levels, required, minimum), oracle(levels, required, minimum), (levels, required, minimum))

    def test_mandatory_candidate_against_subsets(self):
        for count in range(4):
            for others in itertools.product(range(4), repeat=count):
                for candidate in range(5):
                    for required in range(1, 9):
                        for minimum in (1, 3, 255):
                            self.assertEqual(candidate_can_finish(candidate, others, required, minimum), oracle(others, required, minimum, (candidate,)), (candidate, others, required, minimum))

    def test_not_global_minimum_sum(self):
        self.assertTrue(candidate_can_finish(6, [6, 8], 8))  # 12 allowed although 8 exists.
        self.assertTrue(candidate_can_finish(4, [6], 8))
        self.assertFalse(candidate_can_finish(4, [8], 8))
        self.assertTrue(candidate_can_finish(8, [4], 8))
        self.assertFalse(candidate_can_finish(0, [8], 8))

    def test_progress_carries_smallest_previous_tribute(self):
        self.assertEqual(progress(8, 255, 4), (4, 4))
        self.assertTrue(candidate_can_finish(6, [], 4, 4))
        self.assertFalse(candidate_can_finish(8, [], 4, 4))
        self.assertEqual(progress(4, 4, 6), (0, 4))


if __name__ == '__main__':
    unittest.main()
