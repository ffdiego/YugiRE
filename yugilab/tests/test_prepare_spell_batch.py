"""Synthetic multi-spell preparation checks; no DAT, EXE or native process."""
from __future__ import annotations

import copy
from collections import Counter
import unittest

from yugilab.prepare_spell_batch import prepare
from native_hooks.deck_config import validate_deck


def c(index, group, *, available=True, rid=None):
    return dict(internalIndex=index, resourceId=index+2000 if rid is None else rid,
                name=f"Card {index}",group=group,joeyAvailable=available,
                callbackLookup="Present",categoryBits=0,nativeClass=21)


def fixtures():
    normals=[c(i,"normalControl") for i in range(1,41)]
    spells=[c(i,"nonMonsterClass21") for i in range(300,340)]
    traps=[c(414,"nonMonsterClass22"),c(415,"nonMonsterClass23")]
    previous=[c(399,"nonMonsterClass21",available=False)]
    return normals+spells+traps+previous,normals


class SpellBatchTests(unittest.TestCase):
    def test_generate_rotating_verified_spell_only_decks(self):
        cards,normals=fixtures()
        saved=copy.deepcopy(cards)
        plan=prepare(cards,normals,focus_indices=[300,301],
                     spells_per_deck=10,copies_per_spell=2,
                     rounds=3,seed=12345)
        self.assertEqual(cards,saved)
        self.assertEqual(plan["schema"],"yugire-native-multi-spell-decks-v1")
        self.assertEqual(plan["normalsPerDeck"],20)
        self.assertEqual(len(plan["decks"]),6)
        observed=set()
        for item in plan["deckPlan"]:
            deck=plan["decks"][item["file"]]
            self.assertEqual(validate_deck(deck),deck)
            self.assertEqual(len(deck["main"]),40)
            counts=Counter(deck["main"])
            self.assertTrue(all(value<=3 for value in counts.values()))
            self.assertEqual(len(item["spellIdentities"]),10)
            spell_ids={x["internalIndex"] for x in item["spellIdentities"]}
            self.assertTrue({300,301}.issubset(spell_ids))
            observed.update(spell_ids)
            for spell in item["spellIdentities"]:
                self.assertEqual(spell["nativeClass"],21)
                self.assertEqual(counts[spell["internalIndex"]],2)
            self.assertEqual(len(item["normalIndices"]),20)
            self.assertTrue(all(counts[x]==1 for x in item["normalIndices"]))
            self.assertNotIn(414,deck["main"])
            self.assertNotIn(415,deck["main"])
            self.assertNotIn(399,deck["main"])
        self.assertEqual(len(observed),plan["distinctSpellIndicesAcrossDecks"])
        self.assertEqual(plan,prepare(cards,normals,focus_indices=[300,301],
                                      spells_per_deck=10,copies_per_spell=2,
                                      rounds=3,seed=12345))

    def test_bad_focus_or_trap_or_non_joey_are_rejected(self):
        cards,normals=fixtures()
        for focus,pattern in [([414],"spell candidate"),
                              ([399],"spell candidate"),
                              ([1000],"spell candidate"),
                              ([300,300],"distinct"),
                              ([300,414],"spell candidate")]:
            with self.subTest(focus=focus):
                with self.assertRaisesRegex(ValueError,pattern):
                    prepare(cards,normals,focus_indices=focus)
        with self.assertRaisesRegex(ValueError,"distinct"):
            prepare(cards,normals,focus_indices=[300],exclude_indices=[300])

    def test_invalid_quotas_nonnative_or_missing_pool(self):
        cards,normals=fixtures()
        with self.assertRaisesRegex(ValueError,"At least 12"):
            prepare(cards,normals,spells_per_deck=15,copies_per_spell=2)
        with self.assertRaisesRegex(ValueError,"Need 20"):
            prepare(cards,normals[:12])
        with self.assertRaisesRegex(ValueError,"rounds"):
            prepare(cards,normals,rounds=0)
        with self.assertRaisesRegex(ValueError,"nonnegative"):
            prepare(cards,normals,seed=-1)
        with self.assertRaisesRegex(ValueError,"spell identities"):
            prepare(cards,normals,spells_per_deck=1)
        with self.assertRaisesRegex(ValueError,"only"):
            prepare(cards,normals,spells_per_deck=10,
                    exclude_indices=list(range(300,338)))

    def test_duplicate_resource_ids_do_not_allow_ambiguous_native_evidence(self):
        cards,normals=fixtures()
        duplicate=copy.deepcopy(cards)
        next(c for c in duplicate if c["internalIndex"]==301)["resourceId"]=2300
        with self.assertRaisesRegex(ValueError,"share resource IDs"):
            prepare(duplicate,normals)


if __name__=="__main__":
    unittest.main()
