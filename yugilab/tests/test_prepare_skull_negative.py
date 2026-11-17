"""No-process tests for a strictly audited no-Skull demonstration deck pair."""
from __future__ import annotations

import copy
import json
from pathlib import Path
import tempfile
import unittest

from yugilab.prepare_skull_negative import prepare_control, main


def cards():
    return [
        dict(internalIndex=414,resourceId=1306,name="Skull Invitation",
             group="nonMonsterClass22",joeyAvailable=True),
        dict(internalIndex=969,resourceId=1315,name="Rain of Mercy",
             group="nonMonsterClass21",joeyAvailable=True),
        dict(internalIndex=24,resourceId=24,name="Normal 24",
             group="normalControl",joeyAvailable=True),
        dict(internalIndex=377,resourceId=377,name="Normal 377",
             group="normalControl",joeyAvailable=True),
        dict(internalIndex=500,resourceId=500,name="Other",
             group="effectCategoryMonster",joeyAvailable=True),
    ]


def deck(*, skull_count=1, normal=24):
    # Two legitimate 40-card main decks; keep order stable.
    main = [500] * 40
    main[0] = normal
    for i in range(1, skull_count + 1):
        main[i] = 414
    return dict(main=main, second=[], third=[])


class NegativeDeckTests(unittest.TestCase):
    def test_only_skull_replaced_existing_normal_and_zone_length_stable(self):
        originals = [deck(skull_count=2,normal=377), deck(skull_count=1,normal=24)]
        saved = copy.deepcopy(originals)
        negative,audit = prepare_control(originals,cards())
        self.assertEqual(originals, saved)
        self.assertEqual([a["originalSkullCount"] for a in audit],[2,1])
        self.assertEqual([a["replacementInternalIndex"] for a in audit],[377,24])
        self.assertEqual(negative[0]["main"][:4],[377,377,377,500])
        self.assertEqual(negative[1]["main"][:3],[24,24,500])
        self.assertTrue(all(len(x["main"])==40 for x in negative))
        self.assertTrue(all(414 not in x[z] for x in negative
                            for z in ("main","second","third")))
        self.assertTrue(all(x["unchangedNonSkullCards"] for x in audit))

    def test_explicit_override_must_be_reviewed_normal_control(self):
        original = [deck(),deck(skull_count=0)]
        with self.assertRaisesRegex(ValueError,"normalControl"):
            prepare_control(original,cards(),replacement_index=500)
        with self.assertRaisesRegex(ValueError,"normalControl"):
            prepare_control(original,cards(),replacement_index=414)
        negative,audit = prepare_control(original,cards(),replacement_index=377)
        self.assertEqual(audit[0]["replacementInternalIndex"],377)
        self.assertEqual(audit[1]["originalSkullCount"],0)
        self.assertIsNone(audit[1]["replacementInternalIndex"])
        self.assertEqual(negative[1],original[1])

    def test_fails_closed_for_no_skull_or_no_in_deck_normal(self):
        with self.assertRaisesRegex(ValueError,"no Skull Invitation"):
            prepare_control([deck(skull_count=0)]*2,cards())
        source = deck()
        source["main"][0]=500
        with self.assertRaisesRegex(ValueError,"no existing reviewed normal"):
            prepare_control([source,deck(skull_count=0)],cards())

    def test_deck_config_schema_roundtrip(self):
        # Validating the original and generated deck with injector semantics
        # avoids surprises with counts or extra data when actually spawned.
        from native_hooks.deck_config import validate_deck
        generated,_ = prepare_control([deck(),deck(skull_count=0)],cards())
        for result in generated:
            self.assertEqual(validate_deck(result),result)


if __name__=="__main__":
    unittest.main()
