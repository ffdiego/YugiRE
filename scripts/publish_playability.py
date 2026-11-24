#!/usr/bin/env python3
"""Publish reproducible Shields.io badge payloads from a numeric pack inventory.
No DAT or game binaries are needed by GitHub Actions; counts cover sandbox C#.
"""
from __future__ import annotations
import argparse
from collections import Counter
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CARDS = ROOT / "docs/cards"
SCOPES = {"all-packs": 7, "joey": 4}
STATUSES = ("Pending", "Partial", "Done", "Verified")
KINDS = ("Monster", "Spell", "Trap")
SUBTYPES = ("Normal", "Effect", "Fusion", "Ritual", "ToReview")
FINGERPRINT = "7e8841ceb015861dacf898d4a62a909e399da14bb7d69c40de3afa3a4610e1ed"


def load():
    pack = json.loads((CARDS / "pack-index.json").read_text(encoding="utf-8"))
    manifest = json.loads((CARDS / "playability-manifest.json").read_text(encoding="utf-8"))
    subtype_data = json.loads((CARDS / "monster-subtypes.json").read_text(encoding="utf-8"))
    if (pack["schemaVersion"], manifest["schemaVersion"], subtype_data["schemaVersion"]) != (1, 1, 1):
        raise ValueError("Unknown catalog schema")
    if pack["datSha256"] != FINGERPRINT or pack["fields"] != ["resourceId", "packMask", "kindCode"]:
        raise ValueError("Unexpected pack provenance or fields")
    cards = {}
    labels = {1: "Monster", 2: "Spell", 3: "Trap"}
    for rid, mask, code in pack["cards"]:
        if (type(rid) is not int or rid in cards or
                type(mask) is not int or mask not in range(1, 8) or code not in labels):
            raise ValueError("Duplicate/invalid pack record")
        cards[rid] = {"mask": mask, "kind": labels[code]}
    if len(cards) != 771 or sum(bool(c["mask"] & 4) for c in cards.values()) != 350:
        raise ValueError("Unexpected pack denominator")
    status = {}
    for group in manifest["groups"]:
        if group["status"] not in STATUSES: raise ValueError("Bad manifest status")
        for rid in group["resourceIds"]:
            if rid in status: raise ValueError("Resource repeated in manifest")
            if rid in cards and cards[rid]["kind"] != group["kind"]:
                raise ValueError("Resource kind differs from manifest")
            status[rid] = group["status"]
    subtypes = subtype_data["subtypes"]
    for key, subtype in subtypes.items():
        rid = int(key)
        if (subtype not in SUBTYPES[:-1] or rid not in cards or
                cards[rid]["kind"] != "Monster"):
            raise ValueError("Invalid monster subtype mapping")
    return cards, status, subtypes, subtype_data["sources"]


def counts(cards, status, subtypes, scope):
    selected = [(rid, info) for rid, info in cards.items() if info["mask"] & scope]
    total_status = Counter(status.get(rid, "Pending") for rid, _ in selected)
    bykind = {}
    for kind in KINDS:
        subset = [rid for rid, info in selected if info["kind"] == kind]
        states = Counter(status.get(rid, "Pending") for rid in subset)
        bykind[kind] = {"total": len(subset), **{s: states[s] for s in STATUSES}}
    bysubtype = Counter(subtypes.get(str(rid), "ToReview") for rid, info in selected if info["kind"] == "Monster")
    complete = total_status["Done"] + total_status["Verified"]
    return {"total": len(selected), "done": complete,
            "pct": round(100 * complete / len(selected), 2) if selected else 0,
            "statuses": {s: total_status[s] for s in STATUSES},
            "bykind": bykind,
            "bysubtype": {s: bysubtype[s] for s in SUBTYPES}}


def render_report(scopes, sources):
    a, j = scopes["all-packs"], scopes["joey"]
    lines = ["# Cobertura jogável — YugiRE", "",
             "Métrica de cartas jogáveis no sandbox C#, não paridade com o executável original.", "",
             f"- **Trilogia:** {a['done']}/{a['total']} = {a['pct']:.2f}%",
             f"- **Joey:** {j['done']}/{j['total']} = {j['pct']:.2f}%",
             f"- **Partial:** {a['statuses']['Partial']}; **Verified:** {a['statuses']['Verified']} (trilogia).",
             f"- **Partial no Joey:** {j['statuses']['Partial']}.",
             "A badge mostra Done+Verified e, separadamente, o número de Partial. Migração de regras já Done para a fila (C12) não altera a cobertura jogável.", "",
             "## Por categoria", "",
             "| Conjunto | Tipo | Total | Done | Partial | Pending | Verified | % Done+Verified |",
             "| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |"]
    for scope, x in (("Trilogia", a), ("Joey", j)):
        for kind in KINDS:
            v = x["bykind"][kind]
            n = v["Done"] + v["Verified"]
            p = 100 * n / v["total"] if v["total"] else 0
            lines.append(f"| {scope} | {kind} | {v['total']} | {v['Done']} | {v['Partial']} | {v['Pending']} | {v['Verified']} | {p:.2f}% |")
    lines += ["", "## Subtipos de monstros (classificação, não implementação)", "",
              "| Subtipo | Trilogia | Joey |", "| --- | ---: | ---: |"]
    for name in SUBTYPES:
        lines.append(f"| {name} | {a['bysubtype'][name]} | {j['bysubtype'][name]} |")
    lines += ["", "## Como interpretar", "",
              "- Os 771 recursos da união dos packs e os 350 do Joey vêm do DAT original, mas o índice versionado tem somente IDs, flags e categorias, nunca textos ou arquivos de jogo.",
              "- Monster Normal/Effect/Fusion/Ritual são referências externas por nome exato ou normalizado; ToReview permanece explicitamente desconhecido.",
              "- O status Done vem exclusivamente do manifest C# com testes de cenário; classificação não promove carta a Done.",
              "- A infraestrutura genérica de invocação não significa que cada monstro esteja completo.",
              "- Verified exige comparação do mesmo efeito com o jogo original, não só um teste C#.",
              "", "### Fontes externas para subtipos", ""]
    lines += ["- " + u for u in sources]
    return "\n".join(lines) + "\n"


def generated():
    cards, status, subtypes, sources = load()
    scopes = {k: counts(cards, status, subtypes, v) for k, v in SCOPES.items()}
    outputs = {CARDS / "PLAYABILITY.md": render_report(scopes, sources)}
    for key, title in (("all-packs", "CARDS"),):
        v = scopes[key]
        data = {"schemaVersion": 1, "label": title,
                "message": f"{v['done']}/{v['total']} ({v['pct']:.2f}%) | {v['statuses']['Partial']} partial",
                "color": "brightgreen" if v["pct"] >= 70 else "blue" if v["pct"] >= 20 else "orange"}
        outputs[CARDS / "badges" / (key + ".json")] = json.dumps(data, indent=2, ensure_ascii=False) + "\n"
    return outputs, scopes


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args(argv)
    outputs, scopes = generated()
    stale = []
    for path, contents in outputs.items():
        if args.check:
            if not path.exists() or path.read_text(encoding="utf-8") != contents:
                stale.append(str(path.relative_to(ROOT)))
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(contents, encoding="utf-8")
    if stale:
        parser.error("Generated files out of date: " + ", ".join(stale))
    print("Playability:", ", ".join(f"{k}={v['done']}/{v['total']}" for k, v in scopes.items()))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
