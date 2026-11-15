"""Deterministic, read-only progress rendering; no native coverage promotion."""
import copy
import unittest
from scripts.render_coverage_progress import summarize, markdown, svg


def card(index, name, identified=False, partial=False, validated=False):
    return {
        "identity": {"internalIndex": index, "resourceId": index + 100,
                     "name": name},
        "coverage": {
            "identification": {"state": "Identified" if identified else "Unknown"},
            "implementation": {"state": "Partial" if partial else "Unknown"},
            "validation": {"state": "ValidatedInScope" if validated else "Unknown"},
        },
        "mechanics": ([{"mechanicRef": "m"}] if partial else []),
    }


class ProgressTests(unittest.TestCase):
    def test_distinct_denominators_and_determinism(self):
        registry = {
            "baselineCommit": "a" * 40,
            "cards": [card(1, "Sparks", True, True),
                      card(2, "Witch | unaudited", True),
                      card(3, "Unreviewed")],
            "mechanics": [{"id": "m"}],
        }
        original = copy.deepcopy(registry)
        result = summarize(registry)
        self.assertEqual((result["total"], result["annotated"], result["partial"],
                          result["validated"], result["mechanics"], result["links"]),
                         (3, 2, 1, 0, 1, 1))
        self.assertEqual(registry, original)
        text = markdown(registry)
        self.assertIn("| Identificadas/anotadas | 2 |", text)
        self.assertIn("66.67%", text)
        self.assertIn("33.33%", text)
        self.assertIn("Witch &#124; unaudited", text)
        self.assertIn("0", text)
        drawing = svg(registry)
        self.assertIn("2 / 3 (66.67%)", drawing)
        self.assertIn("1 / 3 (33.33%)", drawing)
        self.assertIn("0 / 3 (0.00%)", drawing)
        self.assertEqual(markdown(registry), text)
        self.assertEqual(svg(registry), drawing)
        self.assertEqual(registry, original)

    def test_empty_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "no cards"):
            summarize({"cards": [], "mechanics": []})


if __name__ == "__main__":
    unittest.main()
