#!/usr/bin/env python3
"""Track *playable* Joey.Core cards without conflating them with native RE coverage.

The real catalog is read locally using the existing Joey.Coverage bridge. The
bridge does not export card descriptions or binary assets. No EXE is executed.

Examples:
  python scripts/card_playability.py --dat ../passion/data.dat
  python scripts/card_playability.py --extracted scratch/coverage-current/extracted.json
  python scripts/card_playability.py --extracted tests/fixtures/playability/extracted.json --output scratch/progress-test
"""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import csv
import io
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "docs/cards/playability-manifest.json"
CATALOG = ROOT / "docs/evidence/card-callback-catalog.json"
DEFAULT_OUTPUT = ROOT / "scratch/card-playability"
KINDS = ("Monster", "Spell", "Trap", "Other")
STATUSES = ("Pending", "Partial", "Done", "Verified")
SCOPES = {"all-packs": 7, "joey": 4, "kaiba": 2, "yugi": 1}
TYPE_LABELS = {"Monster": "Monstros", "Spell": "Magias", "Trap": "Armadilhas", "Other": "Outros"}
KNOWN_ANCHORS = {1056: "Trap", 1002: "Trap", 342: "Spell", 332: "Spell", 1427: "Spell"}


def kind_for(class_code: int) -> str:
    if 1 <= class_code <= 20:
        return "Monster"
    if class_code == 21:
        return "Trap"
    if class_code == 22:
        return "Spell"
    # Class 0, 23 and 24 are not silently called spells/traps.
    return "Other"


def read_manifest(path: Path) -> dict[int, dict]:
    data = json.loads(path.read_text(encoding="utf-8"))
    if data.get("schemaVersion") != 1 or set(data.get("statuses", [])) != set(STATUSES):
        raise ValueError("Unsupported playability manifest version/status vocabulary")
    result: dict[int, dict] = {}
    group_ids: set[str] = set()
    for group in data["groups"]:
        group_id = group["id"]
        if not isinstance(group_id, str) or not group_id or group_id in group_ids:
            raise ValueError("Duplicate/invalid group ID")
        group_ids.add(group_id)
        if group["status"] not in STATUSES or group["status"] == "Pending":
            raise ValueError(f"Group {group_id} has invalid tracked status")
        if group["kind"] not in KINDS:
            raise ValueError(f"Invalid kind in {group_id}")
        if not group.get("subtype") or not group.get("mechanics") or not group.get("evidence"):
            raise ValueError(f"Group {group_id} lacks subtype, mechanic or evidence")
        for path_hint in group["evidence"]:
            # Source references belong to the repository, not an EXE or DAT.
            if not isinstance(path_hint, str) or not (ROOT / path_hint).is_file():
                raise ValueError(f"Missing evidence file: {path_hint}")
        for value in group["resourceIds"]:
            if type(value) is not int or value < 0 or value >= 65535:
                raise ValueError(f"Invalid resource ID in {group_id}")
            if value in result:
                raise ValueError(f"Resource ID {value} assigned to more than one group")
            result[value] = {key: group[key] for key in ("id", "kind", "subtype", "mechanics", "status", "evidence")}
            result[value]["limitations"] = group.get("limitations", [])
    return result


def read_callbacks(path: Path) -> dict[int, str]:
    data = json.loads(path.read_text(encoding="utf-8"))
    result = {}
    for rec in data["records"]:
        rid = rec["resource_id"]
        if rid in result:
            raise ValueError(f"Duplicate callback resource ID {rid}")
        result[rid] = rec["profile"]
    return result


def extract_with_dotnet(dat: Path) -> dict:
    if not dat.is_file():
        raise FileNotFoundError(f"Local data.dat not found: {dat}")
    project = str(ROOT / "src/Joey.Coverage")
    subprocess.run(["dotnet", "build", project, "--configuration", "Release"],
                   cwd=ROOT, text=True, capture_output=True, check=True)
    cmd = ["dotnet", "run", "--project", project,
           "--configuration", "Release", "--no-build", "--", str(dat.resolve())]
    result = subprocess.run(cmd, cwd=ROOT, text=True, capture_output=True, check=True)
    try:
        return json.loads(result.stdout)
    except json.JSONDecodeError as exc:
        raise ValueError("Joey.Coverage produced no valid JSON; check .NET build output") from exc


def build(extracted: dict, manifest: dict[int, dict],
          callbacks: dict[int, str]) -> tuple[list[dict], dict]:
    raw = extracted.get("cards")
    if not isinstance(raw, list):
        raise ValueError("Expected cards list from Joey.Coverage")
    entries = []
    seen_indices: set[int] = set()
    raw_resource_ids: set[int] = set()
    for card in raw:
        idx, rid, code, pack = (card[key] for key in
                               ("internalIndex", "resourceId", "classCode", "pack"))
        if any(type(x) is not int for x in (idx, rid, code, pack)) or not (
            0 <= idx <= 4095 and 0 <= rid < 65535 and 0 <= code <= 31 and 0 <= pack <= 65535
        ):
            raise ValueError("Invalid extracted index/resource/class/pack")
        if idx in seen_indices:
            raise ValueError(f"Duplicate internal index {idx}")
        seen_indices.add(idx)
        raw_resource_ids.add(rid)
        if type(card.get("properties")) is not int or ((card["properties"] >> 20) & 31) != code:
            raise ValueError(f"Class code inconsistent with property word at {idx}")
        kind = kind_for(code)
        if rid in KNOWN_ANCHORS and kind != KNOWN_ANCHORS[rid]:
            raise ValueError(f"Known classification mismatch: resource {rid}: {kind}")
        spec = manifest.get(rid)
        if spec is not None and kind != spec["kind"]:
            raise ValueError(f"Manifest kind mismatch for resource {rid}: {kind} != {spec['kind']}")
        entries.append({
            "internalIndex": idx,
            "resourceId": rid,
            "name": card.get("name", ""),
            "classCode": code,
            "packMask": pack,
            "Yugi": bool(pack & 1),
            "Kaiba": bool(pack & 2),
            "Joey": bool(pack & 4),
            "kind": kind,
            "subtype": spec["subtype"] if spec else "ToReview",
            "status": spec["status"] if spec else "Pending",
            "mechanics": spec["mechanics"] if spec else [],
            "group": spec["id"] if spec else None,
            "callbackProfile": callbacks.get(rid),
            "evidence": spec["evidence"] if spec else [],
            # Generic summoning/battle primitives exist for monsters, but that
            # alone does NOT mean a normal/effect monster is Done.
            "basicMonsterMechanics": kind == "Monster",
        })
    missing = sorted(set(manifest) - raw_resource_ids)
    by_scope = {}
    for scope, bit in SCOPES.items():
        subset = [r for r in entries if r["packMask"] & bit]
        status = Counter(r["status"] for r in subset)
        by_kind = {}
        for kind in KINDS:
            rows = [r for r in subset if r["kind"] == kind]
            ct = Counter(r["status"] for r in rows)
            by_kind[kind] = dict(total=len(rows), **{s: ct[s] for s in STATUSES},
                                 completePct=round(100 * (ct["Done"] + ct["Verified"]) / len(rows), 2)
                                 if rows else 0.0,
                                 unclassifiedSubtypes=sum(r["subtype"] == "ToReview" for r in rows))
        total = len(subset)
        complete = status["Done"] + status["Verified"]
        by_scope[scope] = dict(total=total, complete=complete, completePct=round(100 * complete / total, 2) if total else 0.0,
                               statuses={s: status[s] for s in STATUSES},
                               byKind=by_kind,
                               unclassifiedSubtypes=sum(r["subtype"] == "ToReview" for r in subset),
                               genericMonsterBasics=sum(r["basicMonsterMechanics"] for r in subset),
                               distinctResourceIds=len({r["resourceId"] for r in subset}))
    summary = {
        "schemaVersion": 1,
        "kind": "SandboxPlayabilityReport",
        "denominator": "CardListResources.ForPacks entries (internal indices), not 1114 unfiltered or all callback rows",
        "totalUnfiltered": len(entries),
        "manifestEntries": len(manifest),
        "manifestIdsNotInDat": missing,
        "fingerprints": extracted.get("fingerprints", {}),
        "byScope": by_scope,
        "trackedMechanics": sorted(set(m for row in entries if row["packMask"] & 7 for m in row["mechanics"])),
        "note": "Done means playable in the stated C# sandbox scope, not native verified. Unannotated monsters only have generic baseline mechanics."
    }
    return sorted(entries, key=lambda r: r["internalIndex"]), summary


def report_markdown(summary: dict) -> str:
    u, j = summary["byScope"]["all-packs"], summary["byScope"]["joey"]
    lines = [
        "# Progresso jogável — YugiRE",
        "",
        "Fonte: data.dat local (somente leitura) + `docs/cards/playability-manifest.json`.",
        "Isto NÃO é a porcentagem do antigo registry de engenharia reversa.",
        "",
        f"- União Power of Chaos: **{u['complete']}/{u['total']} = {u['completePct']:.2f}%** concluídas no sandbox.",
        f"- Apenas pack Joey: **{j['complete']}/{j['total']} = {j['completePct']:.2f}%**.",
        f"- Monstros com infraestrutura genérica de invocação/batalha: **{u['genericMonsterBasics']}**, não contabilizados como cartas completas.",
        f"- Subtipos ainda por classificar manualmente: **{u['unclassifiedSubtypes']}**.",
        "",
        "## Progresso por categoria (união dos 3 packs)",
        "",
        "| Categoria | Total | Done | Verified | Partial | Pending | Completo |",
        "| --- | ---: | ---: | ---: | ---: | ---: | ---: |",
    ]
    for key in KINDS:
        row = u["byKind"][key]
        lines.append(f"| {TYPE_LABELS[key]} | {row['total']} | {row['Done']} | {row['Verified']} | {row['Partial']} | {row['Pending']} | {row['completePct']:.2f}% |")
    lines += [
        "",
        "### Como ler este resultado",
        "",
        "- **Done** = efeito acionável, testado em C# no escopo indicado; não exige reversão de todo o EXE.",
        "- **Verified** = jogo original confrontado com a mesma funcionalidade inteira; nenhuma promoção é automática.",
        "- **Partial** = integração limitada por carta (ex.: três equipamentos).",
        "- **Pending** = ainda sem registro de implementação; isso não comprova ausência de qualquer primitivo genérico.",
        "- **ToReview** no CSV = subtipo/efeito ainda não revisado. Perfil de callback é um agrupamento técnico e não é classificação semântica.",
        "- O total considera a união dos packs (bits 1/2/4). O filtro Joey isolado aparece separadamente.",
        "- Duplicatas de resource ID em índices internos diferentes não são normalizadas; a contagem é por registro de carta.",
        "",
    ]
    return "\n".join(lines)


def save(output: Path, rows: list[dict], summary: dict) -> None:
    output.mkdir(parents=True, exist_ok=True)
    (output / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    (output / "PROGRESS.md").write_text(report_markdown(summary), encoding="utf-8")
    fields = ["internalIndex", "resourceId", "name", "classCode", "packMask", "Yugi", "Kaiba", "Joey",
              "kind", "subtype", "status", "group", "mechanics", "callbackProfile", "basicMonsterMechanics"]
    with (output / "cards.csv").open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        for row in rows:
            writer.writerow({k: "|".join(row[k]) if k == "mechanics" else row[k] for k in fields})
    (output / "cards.json").write_text(json.dumps(rows, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--dat", type=Path, help="Local legitimate data.dat; .NET read-only bridge is run")
    source.add_argument("--extracted", type=Path, help="Existing Joey.Coverage raw extraction JSON (or fixture)")
    parser.add_argument("--manifest", type=Path, default=MANIFEST)
    parser.add_argument("--callbacks", type=Path, default=CATALOG)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args(argv)
    extracted = json.loads(args.extracted.read_text(encoding="utf-8")) if args.extracted else extract_with_dotnet(args.dat)
    cards, summary = build(extracted, read_manifest(args.manifest), read_callbacks(args.callbacks))
    save(args.output, cards, summary)
    union, joey = summary["byScope"]["all-packs"], summary["byScope"]["joey"]
    print(f"Sandbox Done/Verified: all packs {union['complete']}/{union['total']} ({union['completePct']:.2f}%); "
          f"Joey {joey['complete']}/{joey['total']} ({joey['completePct']:.2f}%).")
    print(f"Report written to {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
