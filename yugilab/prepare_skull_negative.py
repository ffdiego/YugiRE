"""Prepare paired demo decks with Skull Invitation removed from both sides.

Pure offline data operation: input decks are never overwritten, source deck
order/length and every non-Skull card stay unchanged. The replacement must
be a Joey-available normal-control monster from the fingerprinted coverage
registry; prefer a card already present in the affected deck to minimize
new card identities. This is a controlled contrast, NOT an identical game
trajectory or proof that every 300-LP loss comes from Skull Invitation.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from .discovery import _json, catalog
from .temporal_correlations import correlate_graveyard_lp300

SKULL_INDEX = 414
ZONES = ("main", "second", "third")


def deck_data(path: Path) -> dict[str, list[int]]:
    # Reuse the exact native injector's deck schema, including size/count
    # constraints; run the generator from repository root, no Frida needed.
    from native_hooks.deck_config import read_deck_file
    return read_deck_file(path)


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def prepare_control(
    source_decks: list[dict[str, list[int]]],
    catalog_cards: list[dict],
    *, replacement_index: int | None = None
) -> tuple[list[dict[str, list[int]]], list[dict]]:
    """Return isolated A/B deck copies and a per-side audit manifest."""
    if len(source_decks) != 2:
        raise ValueError("Two source decks (side 0 and 1) are required")
    by_index = {c["internalIndex"]: c for c in catalog_cards}
    verified = {index for index, c in by_index.items()
                if c["group"] == "normalControl" and c["joeyAvailable"]}
    if replacement_index is not None and (
            type(replacement_index) is not int or replacement_index not in verified):
        raise ValueError("Explicit replacement must be Joey-available normalControl")
    transformed, audit = [], []
    replaced_total = 0
    for side, deck in enumerate(source_decks):
        if any(zone not in deck or not isinstance(deck[zone], list) for zone in ZONES):
            raise ValueError(f"Side {side}: missing deck zone")
        original = {zone: deck[zone].copy() for zone in ZONES}
        skull_locations = [
            dict(zone=zone, index=i)
            for zone in ZONES for i, card in enumerate(original[zone])
            if card == SKULL_INDEX
        ]
        replacement = None
        if skull_locations:
            present_normals = sorted({
                card for zone in ZONES for card in original[zone]
                if card in verified
            })
            replacement = (replacement_index if replacement_index is not None
                           else next(iter(present_normals), None))
            if replacement is None:
                raise ValueError(f"Side {side} has Skull Invitation but no "
                                 "existing reviewed normal-control replacement; "
                                 "provide --replacement-index after inspecting catalog")
            for location in skull_locations:
                original[location["zone"]][location["index"]] = replacement
            replaced_total += len(skull_locations)
        if any(SKULL_INDEX in original[zone] for zone in ZONES):
            raise ValueError(f"Side {side}: Skull remained in negative-control deck")
        # Only the specifically listed Skull positions may change.
        for zone in ZONES:
            if len(original[zone]) != len(deck[zone]):
                raise ValueError(f"Side {side} zone length changed")
            for i, value in enumerate(original[zone]):
                if value != deck[zone][i] and deck[zone][i] != SKULL_INDEX:
                    raise ValueError(f"Side {side} non-Skull card changed")
        transformed.append(original)
        selected = by_index.get(replacement) if replacement is not None else None
        audit.append(dict(
            side=side, originalSkullCount=len(skull_locations),
            replacedLocations=skull_locations,
            replacementInternalIndex=replacement,
            replacementResourceId=selected["resourceId"] if selected else None,
            replacementName=selected["name"] if selected else None,
            replacementAlreadyPresentInSource=(
                any(replacement in deck[zone] for zone in ZONES)
                if replacement is not None else None),
            unchangedNonSkullCards=True,
            zoneCounts={zone:len(original[zone]) for zone in ZONES}
        ))
    if replaced_total == 0:
        raise ValueError("The two supplied decks contain no Skull Invitation; "
                         "cannot construct a negative control for this pair")
    return transformed, audit


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--registry", type=Path, required=True)
    parser.add_argument("--attributes", type=Path, required=True)
    parser.add_argument("--deck-0", type=Path, required=True)
    parser.add_argument("--deck-1", type=Path, required=True)
    parser.add_argument("--replacement-index", type=int)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args(argv)
    try:
        if args.output_dir.exists():
            raise ValueError("Output directory already exists; refusing overwrite")
        sources = [args.deck_0, args.deck_1]
        decks = [deck_data(p) for p in sources]
        cards = catalog(_json(args.registry), _json(args.attributes))
        controls, audits = prepare_control(
            decks, cards, replacement_index=args.replacement_index)
        args.output_dir.mkdir(parents=True)
        manifest = dict(
            schema="yugire-skull-negative-decks-v1",
            description="Deck substitution experiment; not equivalent duel trajectories",
            sourceDecks=[dict(side=i, path=str(p), sha256=_sha256(p))
                         for i,p in enumerate(sources)],
            registrySha256=_sha256(args.registry),
            attributesSha256=_sha256(args.attributes),
            sourceSkullInternalIndex=SKULL_INDEX,
            audit=audits,
            outputs=[])
        for side, deck in enumerate(controls):
            name=f"negative-side-{side}.json"
            target=args.output_dir / name
            target.write_text(json.dumps(deck, indent=2, ensure_ascii=False)+"\n",
                              encoding="utf-8")
            # Validation again with exactly the injector schema.
            loaded = deck_data(target)
            if loaded != deck:
                raise ValueError(f"Deck roundtrip mismatch: {name}")
            manifest["outputs"].append(dict(side=side,name=name,sha256=_sha256(target)))
        (args.output_dir / "control-manifest.json").write_text(
            json.dumps(manifest, indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
        print("Negative decks created. No source files modified.")
        for item in audits:
            print(f"side {item['side']}: replaced {item['originalSkullCount']} Skull "
                  f"with normalControl {item['replacementInternalIndex']} "
                  f"({item['replacementName']}); original card order preserved")
        print("Review control-manifest.json before a new native duel.")
        return 0
    except (ValueError, OSError, KeyError, TypeError, json.JSONDecodeError) as exc:
        parser.exit(2, f"Negative-control deck generation failed: {exc}\n")


if __name__ == "__main__":
    raise SystemExit(main())
