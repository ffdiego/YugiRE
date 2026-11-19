"""Reproducible 96-card native demo exposure campaign (offline; no EXE execution).

Twelve cohorts, eight distinct focus cards each (3 effect Monsters + 3 Spells
+ 2 Traps). Each target occurs exactly THREE times in BOTH players' 40-card
decks, plus eight historical demo control indices twice each. Deck injection,
callback lookup and native behavior are independent evidence levels.
"""
from __future__ import annotations

import argparse
from collections import Counter
import json
from pathlib import Path

from native_hooks.deck_config import validate_deck

ROOT = Path(__file__).resolve().parents[1]
SEED = 20261119
COPIES = 3
CONTROLS = (151, 241, 525, 834, 885, 886, 887, 1052)
# Reviewed against the committed callback catalog and Joey pack mask.
# Not a claim that the CPU knows how to play every card.
FOCUS = {
    "monster": (
        47, 573, 1123, 1108, 1117, 1114, 1115, 1116, 1163, 601,
        1122, 1110, 1119, 1120, 1307, 15, 539, 579, 585, 735,
        751, 1242, 1246, 1254, 1256, 1330, 1336, 1338, 1340, 1341,
        1344, 1405, 1433, 1435, 1436, 1446,
    ),
    "spell": (
        1007, 1315, 1008, 1023, 1024, 1027, 1079, 1080, 1157, 1158,
        1160, 1161, 1162, 1181, 1183, 1203, 1213, 1248, 1311, 1312,
        1313, 1314, 1318, 1319, 1320, 1419, 1420, 1421, 1422, 1426,
        1449, 1450, 1451, 1902, 330, 332,
    ),
    "trap": (
        1214, 1425, 1090, 1091, 1092, 1094, 1095, 1097, 1098, 1099,
        1142, 1147, 1150, 1154, 1177, 1180, 1204, 1301, 1306, 1317,
        1321, 1323, 1424, 1447,
    ),
}
QUOTAS = {"monster": 3, "spell": 3, "trap": 2}


def read(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def shuffle(items: list[int], seed: int) -> list[int]:
    """xorshift32 Fisher–Yates, stable across Python and JS generators."""
    values = list(items)
    x = seed & 0xFFFFFFFF or 0x6D2B79F5
    for i in range(len(values) - 1, 0, -1):
        x ^= (x << 13) & 0xFFFFFFFF
        x ^= x >> 17
        x ^= (x << 5) & 0xFFFFFFFF
        x &= 0xFFFFFFFF
        j = x % (i + 1)
        values[i], values[j] = values[j], values[i]
    return values


def prepare(root: Path = ROOT, *, seed: int = SEED) -> tuple[dict, dict[str, dict]]:
    if type(seed) is not int or not 0 <= seed <= 0xFFFFFFFF:
        raise ValueError("Seed must be a uint32")
    catalog = read(root / "docs/evidence/card-callback-catalog.json")
    pack = {x[0]: x[1] for x in read(root / "docs/cards/pack-index.json")["cards"]}
    manifest = read(root / "docs/cards/playability-manifest.json")
    done = set().union(*(g["resourceIds"] for g in manifest["groups"]
                         if g["status"] == "Done"))
    by_resource = {}
    known_indices = set()
    for record in catalog["records"]:
        by_resource.setdefault(record["resource_id"], []).append(record)
        for item in record["identities"]:
            known_indices.add(item["index"])

    historical = set()
    for f in ("demo-A.json", "demo-B.json", "demo-C.json"):
        historical.update(read(root / "native_hooks/decks" / f)["main"])
    if len(CONTROLS) != 8 or len(set(CONTROLS)) != 8 or any(
        x not in historical or x in known_indices for x in CONTROLS
    ):
        raise ValueError("Control fillers are not the eight reviewed historical no-callback indices")

    if any(len(FOCUS[k]) != QUOTAS[k] * 12 for k in QUOTAS):
        raise ValueError("Requires exactly 36 monsters + 36 spells + 24 traps")
    ids = [rid for values in FOCUS.values() for rid in values]
    if len(ids) != 96 or len(set(ids)) != 96:
        raise ValueError("Resource IDs must be globally unique")

    def candidate(rid: int, kind: str) -> dict:
        records = by_resource.get(rid, [])
        identities = [item for record in records for item in record["identities"]
                      if ((1 <= item["class_code"] <= 20) if kind == "monster"
                          else item["class_code"] == (22 if kind == "spell" else 21))]
        if len(identities) != 1 or not (pack.get(rid, 0) & 4):
            raise ValueError(f"Unverified Joey pack/type/identity for resource {rid}")
        item = identities[0]
        if item["index"] in CONTROLS:
            raise ValueError(f"Target overlaps control {item['index']}")
        return dict(
            internalIndex=item["index"], resourceId=rid, name=item["name"],
            cardClass=item["class_code"], family=kind,
            callbackProfile=records[0]["profile"], doneSandbox=rid in done,
            copiesPerSide=3,
        )

    cohorts = []
    decks = {}
    for i in range(12):
        targets = [candidate(rid, kind) for kind in QUOTAS
                   for rid in FOCUS[kind][i*QUOTAS[kind]:(i+1)*QUOTAS[kind]]]
        indices = [x["internalIndex"] for x in targets]
        if len(set(indices)) != 8:
            raise ValueError(f"Cohort {i+1} has aliasing internal indices")
        common = [idx for idx in indices for _ in range(COPIES)]
        common.extend(idx for idx in CONTROLS for _ in range(2))
        by_side = {}
        for side in (0, 1):
            filename = f"cohort-{i+1:02d}-{'A' if side == 0 else 'B'}.json"
            deck = validate_deck({
                "main": shuffle(common, seed + (i+1)*1009 + side*7919),
                "second": [], "third": [],
            })
            count = Counter(deck["main"])
            if len(deck["main"]) != 40 or max(count.values()) > 3 or any(
                count[idx] != COPIES for idx in indices
            ):
                raise ValueError(f"Invalid multiplicities in {filename}")
            decks[filename] = deck
            by_side["A" if side == 0 else "B"] = filename
        cohorts.append(dict(number=i+1, name=f"cohort-{i+1:02d}",
                            decks=by_side, targets=targets,
                            focusCardsPerDeck=8, targetCopiesPerSide=COPIES))

    plan = dict(
        schema="yugire-native-96-card-exposure-v1",
        seed=seed, distinctTargetCards=96, cohortsCount=12,
        cohortComposition={"monster": 3, "spell": 3, "trap": 2},
        deckSize=40, copiesPerTargetOnEachSide=3,
        historicalControlIndices=list(CONTROLS),
        copiesPerControl=2, pilotDuelsPerCohort=3,
        totalPilotDuels=36, originalExeModified=False,
        evidenceStatus="PlannedDeckExposureOnly",
        caution="Joystick/AI use, activation and effects are NOT verified by deck inclusion. "
                "Controls come from previous demo decks, not an independently certified "
                "normal-monster catalog. Three copies may violate normal deck restrictions.",
        cohorts=cohorts,
    )
    return plan, decks


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--seed", type=int, default=SEED)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args(argv)
    if args.output_dir.exists():
        parser.error("Refusing to overwrite existing output directory")
    plan, decks = prepare(seed=args.seed)
    args.output_dir.mkdir(parents=True)
    for filename, data in decks.items():
        (args.output_dir / filename).write_text(
            json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    (args.output_dir / "campaign-plan.json").write_text(
        json.dumps(plan, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"Wrote {len(decks)} decks, {plan['distinctTargetCards']} target cards, "
          f"{plan['cohortsCount']} cohorts to {args.output_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
