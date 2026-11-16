#!/usr/bin/env python3
"""Prepare a bounded, effect-heavy pilot for native YugiLab and C# DemoDuel.

The same two 40-card main decks are exported in native internal-index form
and sandbox-manifest form. No native execution, hook changes or auto-validation.
Requires reviewed sandbox pool, complete coverage registry, and attributes
generated from the exact game binary; all source artifacts stay local.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

# (internal index, resource ID, display label): audited P045 *partial* commands.
# No other effects, activation guards, or native equality are inferred.
BURN = (
    (755, 342, "Sparks"),
    (769, 343, "Hinotama"),
    (738, 344, "Final Flame"),
    (557, 345, "Ookazi"),
    (1049, 1007, "Raimei"),
)


def _read(path: Path) -> tuple[dict, str]:
    raw = path.read_bytes()
    result = json.loads(raw)
    if not isinstance(result, dict):
        raise ValueError(f"Expected JSON object in {path}")
    return result, hashlib.sha256(raw).hexdigest()


def prepare(pool: dict, registry: dict, attributes: dict) -> dict[str, object]:
    if pool.get("schemaVersion") != 1 or pool.get("mode") != "SandboxPartialOnly":
        raise ValueError("Input pool must be an explicitly reviewed SandboxPartialOnly manifest")
    if registry.get("schemaVersion") != 1 or attributes.get("registryVersion") != 1:
        raise ValueError("Expected registry v1 and attributes v1")
    if registry.get("fingerprints") != attributes.get("fingerprints"):
        raise ValueError("Coverage registry/attributes fingerprints disagree")
    by_index = {}
    for c in registry["cards"]:
        identity = c["identity"]
        key = identity["internalIndex"]
        if key in by_index:
            raise ValueError(f"Duplicate registry internal index {key}")
        by_index[key] = identity
    by_attr = {}
    for a in attributes["cards"]:
        index = int(a["cardRef"].removeprefix("card:"))
        if index in by_attr:
            raise ValueError(f"Duplicate attributes internal index {index}")
        by_attr[index] = a

    normals = {}
    for c in pool["cards"]:
        index, rid = c["internalIndex"], c["resourceId"]
        if index in normals or by_index.get(index, {}).get("resourceId") != rid:
            raise ValueError(f"Duplicate/mismatched pool identity {index}/{rid}")
        a = by_attr.get(index)
        if a is None or not a["availability"]["Joey"]:
            raise ValueError(f"Pool card {index} is not proven available in Joey pack")
        if (c["policy"] != "BaseMonsterSandbox" or
                not isinstance(c.get("note"), str) or len(c["note"].strip()) < 12 or
                not 1 <= a["classCode"] <= 20 or a["level"] not in (1, 2, 3, 4) or
                a["attack"] <= 0 or (a["properties"] >> 18) & 3 != 0):
            raise ValueError(f"Unreviewed or non-normal pool card {index}")
        normals[index] = dict(c)

    effects = []
    for index, rid, name in BURN:
        a = by_attr.get(index)
        if by_index.get(index, {}).get("resourceId") != rid or a is None:
            raise ValueError(f"Effect identity mismatch: {index}/{rid}")
        # Only use cards in Joey pack, never silently place prior-game-only
        # resources in the Joey demo.
        if a["availability"]["Joey"]:
            # DAT class codes 21..23 are non-monster card categories handled
            # by DemoDuel's spell/trap zone; a numeric category alone does
            # not prove a full native spell/activation contract.
            native_class = a["classCode"]
            if native_class not in (21, 22, 23) or ((a["properties"] >> 20) & 31) != native_class:
                raise ValueError(
                    f"Unsupported P045 non-monster class {native_class} for "
                    f"{name} {index}/{rid}; expected one of 21, 22, 23 "
                    "matching the raw properties"
                )
            effects.append((index, rid, name))
    if not effects:
        raise ValueError("No audited P045 spell available in the Joey pack")

    spell_indices = [index for index, _rid, _name in effects for _ in range(2)]
    normal_count = 40 - len(spell_indices)
    if len(normals) < normal_count + 2:
        raise ValueError(f"Need at least {normal_count + 2} reviewed Joey normal monsters")

    # Deterministic overlapping decks with different monster populations:
    # first the alternating indices, then the remaining alternating indices.
    order = sorted(normals)
    partitions = [order[::2] + order[1::2], order[1::2] + order[::2]]
    mains = []
    for side in (0, 1):
        main = partitions[side][:normal_count] + spell_indices
        if len(main) != 40 or any(main.count(x) > 3 for x in main):
            raise ValueError("Invalid generated deck multiplicity or length")
        mains.append(main)

    reviewed = [normals[x] for x in sorted(normals)]
    reviewed += [
        dict(internalIndex=index, resourceId=rid,
             policy="DirectDamageSandbox",
             note="Pilot P045 bounded fixed LP delta; no complete native effect or legal activation claim.")
        for index, rid, _name in effects
    ]
    sandbox = dict(schemaVersion=1, mode="SandboxPartialOnly", cards=reviewed,
                   deck0=mains[0], deck1=mains[1])
    plan = dict(schema="yugire-effect-pilot-v1", nativeParity=False,
                scope="P045 fixed-damage candidate commands only; no proof of activation",
                effects=[dict(internalIndex=i, resourceId=rid, name=name,
                              nativeClassCode=by_attr[i]["classCode"], copiesPerDeck=2)
                         for i, rid, name in effects],
                unavailableJoeyEffects=[
                    dict(internalIndex=i, resourceId=rid, name=name)
                    for i, rid, name in BURN if i not in {e[0] for e in effects}
                ],
                normalIdentities=len(normals), normalsPerDeck=normal_count,
                deck0=mains[0], deck1=mains[1])
    native0 = dict(main=mains[0], second=[], third=[])
    native1 = dict(main=mains[1], second=[], third=[])
    return {
        "sandbox-mixed.json": sandbox,
        "native-effect-A.json": native0,
        "native-effect-B.json": native1,
        "effect-pilot-plan.json": plan,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pool", type=Path, required=True,
                        help="Operator-reviewed sandbox manifest with normal monsters")
    parser.add_argument("--registry", type=Path, required=True)
    parser.add_argument("--attributes", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args(argv)
    try:
        pool, pool_sha = _read(args.pool)
        registry, registry_sha = _read(args.registry)
        attributes, attr_sha = _read(args.attributes)
        outputs = prepare(pool, registry, attributes)
        outputs["effect-pilot-plan.json"]["inputsSha256"] = {
            "pool": pool_sha, "registry": registry_sha, "attributes": attr_sha
        }
        if args.output_dir.exists():
            raise ValueError(f"Output directory exists: {args.output_dir}")
        args.output_dir.mkdir(parents=True)
        for filename, value in outputs.items():
            (args.output_dir / filename).write_text(
                json.dumps(value, ensure_ascii=False, indent=2) + "\n",
                encoding="utf-8", newline="\n"
            )
        plan = outputs["effect-pilot-plan.json"]
        print(f"Created two 40-card decks: {plan['normalsPerDeck']} normal monsters "
              f"+ {len(plan['effects']) * 2} P045 spells each.")
        print(f"Joey-effect candidates: {[e['name'] for e in plan['effects']]}")
        print("No gameplay observed, no native effect validated. Start with 3 native duels.")
        print(f"Output: {args.output_dir}")
        return 0
    except (ValueError, KeyError, TypeError, OSError, json.JSONDecodeError) as exc:
        parser.exit(2, f"Effect pilot deck preparation failed: {exc}\n")


if __name__ == "__main__":
    raise SystemExit(main())
