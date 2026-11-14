#!/usr/bin/env python3
"""Render a read-only coverage dashboard from a generated, validated registry.json."""
import argparse
from collections import Counter
import html
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))


def summarize(registry):
    cards = registry["cards"]
    total = len(cards)
    if not total:
        raise ValueError("Registry has no cards")
    annotated = [card for card in cards
                 if card["coverage"]["identification"]["state"] != "Unknown"]
    partial = [card for card in cards
               if card["coverage"]["implementation"]["state"] == "Partial"]
    validated = [card for card in cards
                 if card["coverage"]["validation"]["state"] == "ValidatedInScope"]
    # Four independent universes; this is NOT a percentage of playable cards.
    return {
        "total": total,
        "annotated": len(annotated),
        "partial": len(partial),
        "validated": len(validated),
        "mechanics": len(registry["mechanics"]),
        "links": sum(len(card["mechanics"]) for card in cards),
        "annotated_cards": sorted(annotated,
                                  key=lambda card: card["identity"]["internalIndex"]),
        "implementation_states": Counter(
            card["coverage"]["implementation"]["state"] for card in cards),
    }


def percent(count, total):
    return f"{100 * count / total:.2f}%"


def markdown(registry):
    s = summarize(registry)
    esc = lambda value: str(value).replace("|", "&#124;").replace("\n", " ")
    lines = [
        "# Progresso da reimplementação — Card Coverage Registry", "",
        "![Barras de cobertura por carta](progress.svg)", "",
        f"**Baseline auditado:** `{registry['baselineCommit']}`.",
        "Dados calculados a partir do `registry.json` gerado com o overlay revisado.",
        "**Não é uma porcentagem de jogabilidade nem de equivalência com Joey.**", "",
        "## Cartas", "",
        "| Métrica | Quantidade | Fração do catálogo |",
        "| --- | ---: | ---: |",
        f"| Catalogadas | {s['total']} | 100% |",
        f"| Identificadas/anotadas | {s['annotated']} | {percent(s['annotated'], s['total'])} |",
        f"| Implementação parcial documentada | {s['partial']} | {percent(s['partial'], s['total'])} |",
        f"| Validadas integralmente no escopo declarado | {s['validated']} | {percent(s['validated'], s['total'])} |",
        "", "## Mecânicas e vínculos (contagem independente)", "",
        f"- {s['mechanics']} mecânicas documentadas ou candidatas.",
        f"- {s['links']} vínculos entre cartas e mecânicas.",
        "- Uma primitiva compartilhada não equivale a uma carta completa.",
        "", "## Cartas identificadas", "",
        "| Índice | Recurso | Carta | Implementação | Validação | Mecânicas vinculadas |",
        "| ---: | ---: | --- | --- | --- | ---: |",
    ]
    for card in s["annotated_cards"]:
        identity = card["identity"]
        lines.append(
            f"| {identity['internalIndex']} | {identity['resourceId']} | "
            f"{esc(identity['name'])} | {card['coverage']['implementation']['state']} | "
            f"{card['coverage']['validation']['state']} | {len(card['mechanics'])} |")
    lines += [
        "", "## Interpretação", "",
        "- `Partial` significa que algum comportamento foi implementado no escopo documentado; não é carta inteiramente reproduzida.",
        "- `ValidatedInScope` tem critérios específicos de evidência. Testes sintéticos e capturas parciais não promovem esse estado.",
        "- Os inventários de efeitos permanecem abertos até que suas lacunas sejam explicitamente resolvidas.",
        "- Use [o relatório detalhado](integration-report.md) para os limites e a [fila estrutural](integration-report.md#prioridades-de-investigacao) para investigação.",
        "", "## Reprodução", "",
        "Gere o `registry.json` e `attributes.json` com [o gerador existente](generator.md). Depois execute:",
        "", "```powershell",
        "python scripts/render_coverage_progress.py --registry scratch/coverage-current/registry.json --output docs/coverage",
        "```", "",
        "A geração não altera o registry, não executa o Joey e não promove cobertura. ",
        "O SVG e este Markdown são derivados reproduzíveis; não são fontes primárias.", "",
    ]
    return "\n".join(lines)


def svg(registry):
    s = summarize(registry)
    items = [
        ("Anotadas", s["annotated"]),
        ("Implementação parcial", s["partial"]),
        ("Validadas integralmente", s["validated"]),
    ]
    lines = [
        '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 680 270" role="img" aria-labelledby="title desc">',
        '<title id="title">Cobertura do Card Coverage Registry</title>',
        f'<desc id="desc">{s["total"]} cartas no catálogo, {s["annotated"]} anotadas, {s["partial"]} com implementação parcial e {s["validated"]} validadas integralmente. Valores de escopos distintos.</desc>',
        '<rect width="680" height="270" fill="#f8fafc"/>',
        '<text x="24" y="32" fill="#0f172a" font-family="Arial,sans-serif" font-size="21" font-weight="bold">Cobertura por carta</text>',
        f'<text x="24" y="54" fill="#475569" font-family="Arial,sans-serif" font-size="13">Catálogo: {s["total"]} registros — não mede jogabilidade</text>',
    ]
    for i, (label, value) in enumerate(items):
        y = 86 + i * 58
        lines.extend([
            f'<text x="24" y="{y}" fill="#0f172a" font-family="Arial,sans-serif" font-size="14">{html.escape(label)}</text>',
            f'<text x="646" y="{y}" fill="#0f172a" font-family="Arial,sans-serif" font-size="14" text-anchor="end">{value} / {s["total"]} ({percent(value, s["total"])})</text>',
            f'<rect x="24" y="{y + 10}" width="620" height="13" fill="#e2e8f0" rx="6"/>',
            f'<rect x="24" y="{y + 10}" width="{(value / s["total"]) * 620:.3f}" height="13" fill="#2563eb" rx="2"/>',
        ])
    lines.append('</svg>')
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--registry", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    from scripts.coverage.validation import validate
    registry = json.loads(args.registry.read_text(encoding="utf-8"))
    validate(registry)
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / "progress.md").write_text(markdown(registry), encoding="utf-8", newline="\n")
    (args.output / "progress.svg").write_text(svg(registry), encoding="utf-8", newline="\n")


if __name__ == "__main__":
    main()
