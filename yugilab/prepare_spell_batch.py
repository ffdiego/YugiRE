"""Prepare fingerprinted multi-spell native discovery decks for Joey.

Conservative filter: Joey-available DAT class 21 spell CANDIDATES only.
No claim that every selected card will be played, or that class membership
constitutes behavioral/legality parity. Native EXE and DAT remain untouched.
"""
from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import random

from .discovery import _json, _reviewed_normals, catalog
from native_hooks.deck_config import validate_deck


def prepare(
    cards: list[dict], reviewed_normals: list[dict], *,
    focus_indices: list[int] | None = None, exclude_indices: list[int] | None = None,
    spells_per_deck: int = 10, copies_per_spell: int = 2,
    rounds: int = 3, seed: int = 12345,
) -> dict:
    if type(rounds) is not int or not 1 <= rounds <= 8:
        raise ValueError("rounds must be 1..8")
    if type(seed) is not int or seed < 0:
        raise ValueError("seed must be a nonnegative integer")
    if (type(spells_per_deck) is not int or not 2 <= spells_per_deck <= 20
        or type(copies_per_spell) is not int or copies_per_spell not in (1, 2, 3)):
        raise ValueError("Use 2..20 spell identities and 1..3 copies per identity")
    normal_count = 40 - spells_per_deck * copies_per_spell
    if normal_count < 12:
        raise ValueError("At least 12 reviewed normal monsters per 40-card deck required")
    if len(reviewed_normals) < normal_count:
        raise ValueError(f"Need {normal_count} reviewed native normals; "
                         f"only {len(reviewed_normals)} available")

    focus = list(focus_indices or [])
    exclude = list(exclude_indices or [])
    if (len(set(focus)) != len(focus) or len(set(exclude)) != len(exclude)
        or set(focus) & set(exclude)
        or any(type(x) is not int or not 1 <= x <= 4095 for x in focus + exclude)):
        raise ValueError("Focus/exclude IDs must be distinct internal indices 1..4095")
    if len(focus) > spells_per_deck:
        raise ValueError("More focus spells than spells_per_deck")
    normal_by_id = {x["internalIndex"]: x for x in reviewed_normals}
    if len(normal_by_id) != len(reviewed_normals) or any(
        not x["joeyAvailable"] or x["group"] != "normalControl"
        for x in reviewed_normals
    ):
        raise ValueError("Reviewed-normal input must contain unique Joey normalControl")
    cards_by_index = {x["internalIndex"]: x for x in cards}
    if len(cards_by_index) != len(cards):
        raise ValueError("Duplicate catalog internal indices")
    spell_pool = [
        c for c in cards
        if c["joeyAvailable"] and c["group"] == "nonMonsterClass21"
        and c["internalIndex"] not in exclude
    ]
    by_spell_id = {c["internalIndex"]:c for c in spell_pool}
    missing_focus = [x for x in focus if x not in by_spell_id]
    if missing_focus:
        raise ValueError("Focus is not a Joey-available DAT class 21 "
                         f"spell candidate: {missing_focus}")
    extras = sorted(
        (c for c in spell_pool if c["internalIndex"] not in focus),
        key=lambda c: c["internalIndex"])
    need_extra = spells_per_deck - len(focus)
    if len(extras) < need_extra:
        raise ValueError(f"Need {need_extra} non-focus spell identities; "
                         f"only {len(extras)} available")
    rng = random.Random(seed)
    rng.shuffle(extras)
    normals = sorted(reviewed_normals, key=lambda c:c["internalIndex"])
    rng.shuffle(normals)
    if len(set(c["resourceId"] for c in spell_pool)) != len(spell_pool):
        # Shared RIDs cannot be attributed to an individual source card in
        # the CF/CE observer; fail rather than generate misleading identity.
        raise ValueError("Multiple spell candidates share resource IDs; "
                         "review before attributing native effects")

    deck_files = {}
    planned = []
    selected = set()
    for round_index in range(rounds):
        for side in (0, 1):
            base = (2 * round_index + side)
            if need_extra:
                offset = base * need_extra % len(extras)
                candidate_extras = [
                    extras[(offset + i) % len(extras)]
                    for i in range(need_extra)
                ]
            else:
                candidate_extras = []
            spell_ids = focus + [x["internalIndex"] for x in candidate_extras]
            if len(spell_ids) != spells_per_deck or len(set(spell_ids)) != len(spell_ids):
                raise ValueError("Duplicate spell identity within deck")
            normal_offset = base * normal_count % len(normals)
            normal_ids = [
                normals[(normal_offset + i) % len(normals)]["internalIndex"]
                for i in range(normal_count)
            ]
            main = normal_ids + [
                idx for idx in spell_ids for _ in range(copies_per_spell)
            ]
            if (len(main) != 40 or
                any(x > 3 for x in Counter(main).values())):
                raise ValueError("Invalid size or multiplicity of generated deck")
            random.Random(seed + 1009 * round_index + side).shuffle(main)
            deck = validate_deck(dict(main=main, second=[], third=[]))
            suffix = "A" if side == 0 else "B"
            filename = f"spell-round-{round_index+1:02d}-{suffix}.json"
            deck_files[filename] = deck
            selected.update(spell_ids)
            planned.append(dict(
                round=round_index+1, side=side, file=filename,
                spellIdentities=[dict(
                    internalIndex=i, resourceId=by_spell_id[i]["resourceId"],
                    name=by_spell_id[i]["name"], nativeClass=21,
                    copies=copies_per_spell,
                    callbackLookup=by_spell_id[i]["callbackLookup"])
                    for i in spell_ids],
                normalIndices=normal_ids,
                exactDeckSize=40,
                originalSourceUnmodified=True))
    return dict(
        schema="yugire-native-multi-spell-decks-v1",
        nativeParity=False,
        selectionClass="Joey-available DAT class 21 spell candidates",
        interpretationLimit="Inventory/coverage does not prove activation, "
            "effect resolution, valid targets or native rule parity. "
            "Duel RNG and AI may not play every selected spell. "
            "Do not classify absence of CF as a negative effect result.",
        seed=seed, rounds=rounds, spellsPerDeck=spells_per_deck,
        copiesPerSpell=copies_per_spell, normalsPerDeck=normal_count,
        spellCandidatesInJoey=len(spell_pool),
        focusIndices=focus, excludedIndices=exclude,
        distinctSpellIndicesAcrossDecks=len(selected),
        deckPlan=planned, decks=deck_files)


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--registry", required=True, type=Path)
    parser.add_argument("--attributes", required=True, type=Path)
    parser.add_argument("--reviewed-normals", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--focus-index", type=int, action="append", default=[])
    parser.add_argument("--exclude-index", type=int, action="append", default=[])
    parser.add_argument("--spells-per-deck", type=int, default=10)
    parser.add_argument("--copies-per-spell", type=int, default=2)
    parser.add_argument("--rounds", type=int, default=3)
    parser.add_argument("--seed", type=int, default=12345)
    args = parser.parse_args(argv)
    try:
        if args.output_dir.exists():
            raise ValueError(f"Refusing overwrite: {args.output_dir}")
        reg, attrs, normals = (
            _json(args.registry), _json(args.attributes),
            _json(args.reviewed_normals))
        cards = catalog(reg, attrs)
        plan = prepare(
            cards, _reviewed_normals(cards, normals),
            focus_indices=args.focus_index, exclude_indices=args.exclude_index,
            spells_per_deck=args.spells_per_deck,
            copies_per_spell=args.copies_per_spell,
            rounds=args.rounds, seed=args.seed)
        plan["inputHashes"] = dict(
            registry=_sha256(args.registry), attributes=_sha256(args.attributes),
            reviewedNormals=_sha256(args.reviewed_normals))
        decks = plan.pop("decks")
        args.output_dir.mkdir(parents=True)
        plan["outputHashes"] = {}
        for filename, deck in decks.items():
            path = args.output_dir / filename
            path.write_text(
                json.dumps(deck,ensure_ascii=False,indent=2)+"\n",
                encoding="utf-8",newline="\n")
            if validate_deck(json.loads(path.read_text(encoding="utf-8"))) != deck:
                raise ValueError("Deck roundtrip integrity failed: "+filename)
            plan["outputHashes"][filename] = _sha256(path)
        (args.output_dir/"spell-batch-plan.json").write_text(
            json.dumps(plan,ensure_ascii=False,indent=2)+"\n",
            encoding="utf-8",newline="\n")
        print(f"Prepared {len(decks)} verified 40-card decks across "
              f"{plan['rounds']} rounds: {plan['spellsPerDeck']} spell "
              f"identities x {plan['copiesPerSpell']} copies per side; "
              f"{plan['normalsPerDeck']} normal monsters.")
        print(f"Unique spell candidates across decks: "
              f"{plan['distinctSpellIndicesAcrossDecks']}; "
              f"available in Joey class 21: {plan['spellCandidatesInJoey']}.")
        for entry in plan["deckPlan"][:2]:
            print("Round 1, side",entry["side"],":",
                  ", ".join(x["name"] for x in entry["spellIdentities"]))
        print("No gameplay executed. Review spell-batch-plan.json before pilot.")
        return 0
    except (ValueError, OSError, KeyError, TypeError, json.JSONDecodeError) as exc:
        parser.exit(2,f"Multi-spell preparation failed: {exc}\n")


if __name__ == "__main__":
    raise SystemExit(main())
