"""Native effect discovery: diverse bounded decks and evidence-scoped research queue.

Offline only. A catalog callback is a lead, CF/CE are card-associated commands,
and deck verification proves deck injection -- NONE proves an effect activation.
Only P045 cases with explicit before/after LP are tagged as bounded observations.
"""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import hashlib
import json
from pathlib import Path
import random

from .effect_report import _candidate, _opcode, gather, link_fixed_damage_deltas

# Preserved from NormalSummonRules.RequiresSpecialSummon; this list is an
# additional conservative native-demo candidate gate, not a rules model.
SPECIAL_ONLY_IDS = {
    0x37, 0x38, 0x3e, 0x42, 0x170, 0x175, 0x187, 0x2d6, 0x2d7,
    0x2d8, 0x2e5, 0x2fe, 0x34d, 0x4b2, 0x4e9, 0x5ea, 0x5eb,
    0x5ec, 0x5ed, 0x5ee, 0x5ef,
}
P045 = {342, 343, 344, 345, 1007}


def _json(path: Path) -> dict:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise ValueError(f"Expected JSON object: {path}")
    return data


def catalog(registry: dict, attributes: dict) -> list[dict]:
    if registry.get("schemaVersion") != 1 or attributes.get("registryVersion") != 1:
        raise ValueError("Expected coverage registry and attributes version 1")
    if registry.get("fingerprints") != attributes.get("fingerprints"):
        raise ValueError("Coverage registry/attributes fingerprints differ")
    attr_by_index = {}
    for a in attributes["cards"]:
        idx = int(a["cardRef"].removeprefix("card:"))
        if idx in attr_by_index:
            raise ValueError(f"Duplicate attributes internal index {idx}")
        attr_by_index[idx] = a
    cards, used = [], set()
    for item in registry["cards"]:
        identity = item["identity"]
        index, rid = identity["internalIndex"], identity["resourceId"]
        if index in used or index not in attr_by_index:
            raise ValueError(f"Duplicate or missing attributes for {index}")
        used.add(index)
        a = attr_by_index[index]
        cls = a["classCode"]
        if cls != ((a["properties"] >> 20) & 31):
            raise ValueError(f"Class mismatches raw properties: {index}")
        category = (a["properties"] >> 18) & 3
        if not a["availability"]["Joey"]:
            group = "notInJoeyPack"
        elif 1 <= cls <= 20 and category == 0 and 1 <= a["level"] <= 4 and a["attack"] > 0:
            group = "normalControl"
        elif (1 <= cls <= 20 and category == 1 and 1 <= a["level"] <= 4
              and rid not in SPECIAL_ONLY_IDS and rid not in (0x776, 0x777, 0x778)):
            group = "effectCategoryMonster"
        elif cls in (21, 22, 23):
            group = f"nonMonsterClass{cls}"
        else:
            group = "outOfPilotScope"
        cards.append(dict(internalIndex=index, resourceId=rid, name=identity["name"],
                          group=group, nativeClass=cls, categoryBits=category,
                          callbackLookup=item.get("callbackLookup", {}).get("state", "Unknown"),
                          joeyAvailable=bool(a["availability"]["Joey"])))
    if len(cards) != len(attr_by_index):
        raise ValueError("Registry/attributes card counts disagree")
    return sorted(cards, key=lambda c: c["internalIndex"])


def _reviewed_normals(cards: list[dict], manifest: dict) -> list[dict]:
    if manifest.get("schemaVersion") != 1 or manifest.get("mode") != "SandboxPartialOnly":
        raise ValueError("Normals need a reviewed SandboxPartialOnly manifest")
    by_index = {c["internalIndex"]: c for c in cards}
    approved, seen = [], set()
    for entry in manifest["cards"]:
        idx = entry["internalIndex"]
        if idx in seen:
            raise ValueError(f"Duplicate reviewed identity {idx}")
        seen.add(idx)
        c = by_index.get(idx)
        if (c is None or c["resourceId"] != entry["resourceId"] or
            c["group"] != "normalControl" or entry["policy"] != "BaseMonsterSandbox" or
            not isinstance(entry.get("note"), str) or len(entry["note"].strip()) < 12):
            raise ValueError(f"Unreviewed or non-normal control {idx}")
        approved.append(c)
    return approved


def _rotate(pool: list[dict], amount: int, draw_index: int) -> list[dict]:
    if len(pool) < amount:
        raise ValueError(f"Need {amount} candidates, found {len(pool)}")
    offset = (draw_index * amount) % len(pool)
    return [pool[(offset + j) % len(pool)] for j in range(amount)]


def prepare(registry: dict, attributes: dict, normals: dict, *,
            seed: int = 12345, rounds: int = 3, normal_count: int = 16,
            effect_count: int = 12, nonmonster_count: int = 12) -> dict:
    if rounds not in range(1, 21) or any(x < 0 for x in (normal_count, effect_count, nonmonster_count)):
        raise ValueError("Rounds must be 1..20 and quotas nonnegative")
    if normal_count + effect_count + nonmonster_count != 40 or normal_count < 12:
        raise ValueError("Decks require 40 cards, including at least 12 reviewed normals")
    cards = catalog(registry, attributes)
    approved = _reviewed_normals(cards, normals)
    effects = [c for c in cards if c["group"] == "effectCategoryMonster"]
    nonmonsters = [c for c in cards if c["group"].startswith("nonMonsterClass")]
    # Callbacks can improve research yield, but do not establish effect
    # semantics. Shuffle each stratum for reproducible, rotating coverage.
    rng = random.Random(seed)
    for pool in (approved, effects, nonmonsters):
        pool.sort(key=lambda c: c["internalIndex"])
        rng.shuffle(pool)
    decks, all_selected = {}, set()
    for n in range(rounds):
        for side in (0, 1):
            offset = 2 * n + side
            chosen = (_rotate(approved, normal_count, offset) +
                      _rotate(effects, effect_count, offset) +
                      _rotate(nonmonsters, nonmonster_count, offset))
            ids = [c["internalIndex"] for c in chosen]
            if len(ids) != 40 or len(set(ids)) != 40:
                raise ValueError("Duplicate indices or wrong deck size")
            key = f"round-{n + 1:02d}-{('A', 'B')[side]}"
            decks[f"{key}.json"] = dict(main=ids, second=[], third=[])
            all_selected.update(ids)
    return dict(schema="yugire-native-effect-discovery-plan-v1",
                nativeParity=False, seed=seed, quotas=dict(normal=normal_count,
                effectCategoryMonster=effect_count, nonMonster=nonmonster_count),
                candidatePoolCounts=dict(reviewedNormal=len(approved),
                    effectCategoryMonster=len(effects), nonMonster=len(nonmonsters)),
                selectedDistinctCards=len(all_selected), rounds=rounds,
                interpretationLimit="Exploration-only native decks. Effect category/callback "
                "does not prove effect activation, correct behavior or sandbox support.",
                cards=[c for c in cards if c["internalIndex"] in all_selected],
                decks=decks)


def _capture_summary(path: Path) -> tuple[dict, Counter, Counter, dict, dict]:
    """Stream JSONL, preserve resource identities and check verified deck injections."""
    counts, verified, examples = Counter(), Counter(), defaultdict(list)
    issues, seq_expected, events, terminals, footer, header = [], 1, 0, 0, None, None
    sha = hashlib.sha256()
    with path.open("rb") as source:
        for line_no, raw in enumerate(source, 1):
            sha.update(raw)
            if not raw.strip():
                continue
            e = json.loads(raw)
            kind = e.get("kind")
            if header is None:
                if kind != "metadata":
                    issues.append("Missing initial metadata")
                header = e
                continue
            if kind == "capture_summary":
                if footer is not None:
                    issues.append("Duplicate capture_summary")
                footer = e
                continue
            if footer is not None:
                issues.append("Events after footer")
            events += 1
            seq = e.get("seq")
            if type(seq) is not int or seq != seq_expected:
                issues.append(f"Noncontiguous seq at line {line_no}: {seq}, expected {seq_expected}")
            seq_expected = seq + 1 if type(seq) is int else seq_expected
            if kind == "deck_override":
                if e.get("phase") == "failed":
                    issues.append(f"Deck override failure at seq {seq}")
                if e.get("phase") == "verified":
                    side = str(e.get("side"))
                    planned = (header.get("deckPlans") or {}).get(side, {}).get("main")
                    actual = e.get("actualIndices")
                    if not isinstance(actual, list) or not isinstance(planned, list) or Counter(actual) != Counter(planned):
                        issues.append(f"Deck verification disagree for side {side} at seq {seq}")
                    else:
                        for idx in set(actual):
                            verified[idx] += 1
            opcode = _opcode(e)
            if opcode is not None:
                candidate = _candidate(e, opcode)
                if candidate:
                    ckind, rid = candidate
                    counts[(ckind, rid)] += 1
                    if len(examples[rid]) < 5:
                        examples[rid].append(dict(file=path.name, seq=seq, duelIndex=terminals,
                                                  kind=ckind))
                if opcode == 4:
                    terminals += 1
    if footer is None or footer.get("complete") is not True:
        issues.append("Capture footer missing or incomplete")
    if footer is not None:
        if footer.get("eventsWritten") != events or footer.get("capturedDuels") != terminals:
            issues.append("Footer event/terminal counts disagree")
        if footer.get("overwritten") != 0:
            issues.append("Capture events overwritten or unknown")
    if header is None or header.get("mode") != "live_duels":
        issues.append("Not a live_duels capture")
    # Four verification records might mean 2 repeated decks; the absence
    # of a card in a command never implies it was not drawn.
    linked = link_fixed_damage_deltas(path)
    summary = dict(file=path.name, sha256=sha.hexdigest(), events=events,
                   terminalCommands=terminals, integrityOk=not issues, issues=issues,
                   verifiedDeckCardIdentities=len(verified),
                   linkedP045Cases=len(linked),
                   exactP045LpDeltas=sum(c["exactLpDelta"] for c in linked))
    return summary, counts, verified, dict(examples), Counter(
        (c["sourceResourceId"], bool(c["exactLpDelta"])) for c in linked)


def analyze(registry: dict, attributes: dict, inputs: list[Path], *, limit: int = 100) -> dict:
    cards = catalog(registry, attributes)
    capture_files = gather(inputs)
    captures, commands, verified, examples, linked = [], Counter(), Counter(), defaultdict(list), Counter()
    for path in capture_files:
        summary, this_commands, this_verified, this_examples, this_linked = _capture_summary(path)
        captures.append(summary)
        commands.update(this_commands)
        verified.update(this_verified)
        linked.update(this_linked)
        for rid, items in this_examples.items():
            if len(examples[rid]) < 6:
                examples[rid].extend(items[:6-len(examples[rid])])
    by_rid = defaultdict(list)
    for c in cards:
        by_rid[c["resourceId"]].append(c["internalIndex"])
    rows = []
    for card in cards:
        rid, idx = card["resourceId"], card["internalIndex"]
        cf, ce = commands[("CF", rid)], commands[("CE", rid)]
        confirmed, mismatched = linked[(rid, True)], linked[(rid, False)]
        if not card["joeyAvailable"] or (
            card["group"] in ("normalControl", "outOfPilotScope") and
            not (cf or ce or verified[idx])):
            continue
        ambiguous = len(by_rid[rid]) > 1
        if confirmed and not ambiguous:
            evidence = "ObservedFixedLpDeltaInScope"
            next_action = "Compare exact P045 command context with C#; do not infer full activation"
        elif cf or ce:
            evidence = "ResourceAssociatedCommandsOnly"
            next_action = "Inspect native context and changes around recorded seq; CE can be placement"
        elif verified[idx]:
            evidence = "DeckVerifiedNoResourceCommand"
            next_action = "Check draw/AI use and target constraints; do not infer no effect"
        else:
            evidence = "CatalogCandidateNotObserved"
            next_action = "Include in a bounded discovery deck and inspect any new native events"
        interesting = card["group"].startswith("nonMonsterClass") or card["group"] == "effectCategoryMonster"
        score = (100 if interesting and cf else 50 if interesting and ce else
                 20 if interesting and verified[idx] else 0)
        score += min(20, cf * 3) + min(10, ce) + (12 if card["callbackLookup"] == "Present" else 0)
        score += (10 if confirmed else 0)
        rows.append(dict(**card, cfCommands=cf, ceCommands=ce,
                         verifiedDeckBatches=verified[idx],
                         lpDeltaExactCases=confirmed, lpDeltaMismatches=mismatched,
                         sharedResourceIndices=by_rid[rid] if ambiguous else [],
                         evidence=evidence, readyForFullCsharpComparison=False,
                         nextAction=next_action, priorityScore=score,
                         examples=examples[rid]))
    # Normal cards are controls rather than effect-discovery priorities.
    queue = [r for r in rows if r["group"] not in ("normalControl", "outOfPilotScope")]
    queue.sort(key=lambda r: (-r["priorityScore"], -r["cfCommands"],
                               -r["ceCommands"], r["internalIndex"]))
    return dict(schema="yugire-native-effect-discovery-report-v1",
                integrityOk=all(x["integrityOk"] for x in captures),
                interpretationLimit="Resource-associated CF/CE commands and callback table are "
                "not proof of effect activation. Unseen commands cannot prove a card unused. "
                "P045 fixed LP changes are scoped native observations, not full card parity.",
                captures=captures, terminalCommands=sum(x["terminalCommands"] for x in captures),
                observedResourceIds=len({r for (_kind, r) in commands}),
                catalogCandidates=len(queue), rankedQueue=queue[:limit],
                perCard=rows)



def _summary_markdown(result: dict, *, max_rows: int = 25) -> str:
    lines = [
        "# Fila de investigação nativa — relatório exploratório",
        "",
        f"- Integridade das capturas: **{result['integrityOk']}**",
        f"- Comandos terminais: **{result['terminalCommands']}**",
        f"- Resource IDs associados a CF/CE: **{result['observedResourceIds']}**",
        f"- Candidatos no catálogo Joey: **{result['catalogCandidates']}**",
        "",
        "CF/CE indica associação a recurso, não ativação de efeito. "
        "Mudanças P045 de LP são observações restritas; não validam a carta inteira. "
        "A pontuação é uma prioridade heurística para pesquisa.",
        "",
        "| Índice | Recurso | Carta | Grupo | CF | CE | Casos LP P045 | Evidência |",
        "|---:|---:|---|---|---:|---:|---:|---|",
    ]
    for row in result["rankedQueue"][:max_rows]:
        name = row["name"].replace("|", r"\|").replace("\n", " ").replace("\r", " ")
        lines.append(
            f"| {row['internalIndex']} | {row['resourceId']} | {name} | "
            f"{row['group']} | {row['cfCommands']} | {row['ceCommands']} | "
            f"{row['lpDeltaExactCases']} | {row['evidence']} |"
        )
    lines.extend([
        "",
        "## Próximas verificações",
        "",
        "1. Examinar captures[].issues; qualquer perda invalida a campanha.",
        "2. Para cartas CF/CE sem LP correlacionado, inspecionar examples[].seq "
        "e estado nativo independente; CE pode ser apenas invocação/posicionamento.",
        "3. Para cartas com DeckVerifiedNoResourceCommand, medir se foram "
        "compradas ou apresentadas à IA antes de concluir qualquer ausência.",
        "4. Nunca comparar frequência/resultado com C# como se fossem os "
        "mesmos duelos. Guardar capturas e catálogo originais fora do Git.",
        "",
    ])
    return "\n".join(lines)


def _write_new(directory: Path, outputs: dict[str, object]) -> None:
    if directory.exists():
        raise ValueError(f"Output directory already exists: {directory}")
    directory.mkdir(parents=True)
    for name, data in outputs.items():
        target = directory / name
        target.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n",
                          encoding="utf-8", newline="\n")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)
    prep = sub.add_parser("prepare", help="Generate rotated 40-card native decks")
    ana = sub.add_parser("analyze", help="Create per-card metrics and research queue")
    for p in (prep, ana):
        p.add_argument("--registry", required=True, type=Path)
        p.add_argument("--attributes", required=True, type=Path)
        p.add_argument("--output-dir", required=True, type=Path)
    prep.add_argument("--reviewed-normals", required=True, type=Path)
    prep.add_argument("--seed", type=int, default=12345)
    prep.add_argument("--rounds", type=int, default=3)
    prep.add_argument("--normal", type=int, default=16)
    prep.add_argument("--effect-monsters", type=int, default=12)
    prep.add_argument("--nonmonsters", type=int, default=12)
    ana.add_argument("--input", nargs="+", required=True, type=Path)
    ana.add_argument("--limit", type=int, default=100)
    args = parser.parse_args(argv)
    try:
        if args.output_dir.exists():
            raise ValueError(f"Output directory already exists: {args.output_dir}")
        reg, attrs = _json(args.registry), _json(args.attributes)
        source_hashes = dict(registry=hashlib.sha256(args.registry.read_bytes()).hexdigest(),
                             attributes=hashlib.sha256(args.attributes.read_bytes()).hexdigest())
        if args.command == "prepare":
            pool_path = args.reviewed_normals
            plan = prepare(reg, attrs, _json(pool_path), seed=args.seed, rounds=args.rounds,
                           normal_count=args.normal, effect_count=args.effect_monsters,
                           nonmonster_count=args.nonmonsters)
            plan["inputHashes"] = dict(source_hashes,
                                       reviewedNormals=hashlib.sha256(pool_path.read_bytes()).hexdigest())
            outputs = dict(plan["decks"])
            outputs["discovery-plan.json"] = {k: v for k, v in plan.items() if k != "decks"}
            _write_new(args.output_dir, outputs)
            print(f"Native discovery decks: {len(plan['decks'])}; distinct selected identities: "
                  f"{plan['selectedDistinctCards']}; candidates: {plan['candidatePoolCounts']}")
            print("Native cards not reviewed for effect safety; pilot THREE duels with ONE worker first.")
        else:
            if args.limit < 1 or args.limit > 1000:
                raise ValueError("--limit must be 1..1000")
            result = analyze(reg, attrs, args.input, limit=args.limit)
            result["inputHashes"] = source_hashes
            _write_new(args.output_dir, {"discovery-report.json": result})
            (args.output_dir / "discovery-summary.md").write_text(
                _summary_markdown(result), encoding="utf-8", newline="\n")
            print(f"Native captures {len(result['captures'])}; terminals "
                  f"{result['terminalCommands']}; candidate resource IDs "
                  f"{result['observedResourceIds']}; integrity={result['integrityOk']}")
            print(f"Queue: {len(result['rankedQueue'])} of {result['catalogCandidates']} candidates.")
            if not result["integrityOk"]:
                return 2
        print(f"Output: {args.output_dir}")
        return 0
    except (OSError, ValueError, KeyError, TypeError, json.JSONDecodeError) as exc:
        parser.exit(2, f"Discovery failed: {exc}\n")


if __name__ == "__main__":
    raise SystemExit(main())
