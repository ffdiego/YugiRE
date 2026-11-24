"""ACTIONS badge uses only completed, unique roadmap milestones."""
import json
import unittest

from scripts.publish_actions_progress import BADGE, PLAN, generated, milestones


def plan(*rows):
    return "### Itens da trilha ACTIONS\n" + "\n".join(rows) + "\n## Trilha CARDS\n"


class ActionsProgressTests(unittest.TestCase):
    def test_counts_only_completed(self):
        source = plan("- [x] **A01 — Contract.** Status: `Concluído`.",
                      "- [ ] **A02 — Queue.** Status: `Parcial`.",
                      "- [ ] **A03 — Events.** Status: `Bloqueado por A02`.")
        self.assertEqual(milestones(source), (1, 3))
        self.assertEqual(json.loads(generated(source))["message"], "1/3 marcos (33.33%)")

    def test_rejects_duplicate_and_inconsistent_status(self):
        row = "- [x] **A01 — Contract.** Status: `Concluído`."
        for invalid in (plan(row, row),
                        plan("- [x] **A02 — Queue.** Status: `Parcial`."),
                        plan("- [ ] **A03 — Events.** Status: `Concluído`."),
                        plan("- [ ] **A04 — Malformed.**")):
            with self.subTest(invalid=invalid), self.assertRaises(ValueError):
                milestones(invalid)

    def test_checked_in_badge(self):
        expected = generated(PLAN.read_text(encoding="utf-8"))
        self.assertEqual(BADGE.read_text(encoding="utf-8"), expected)
        self.assertEqual(json.loads(expected)["label"], "ACTIONS")


if __name__ == "__main__":
    unittest.main()
