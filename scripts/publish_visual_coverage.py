#!/usr/bin/env python3
"""Publish visual native Draw-entrypoint coverage without game binaries."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "docs/architecture"
INVENTORY = DOCS / "native-renderer-inventory.md"
MANIFEST = DOCS / "visual-function-coverage.json"
STATUSES = ("Pending", "Partial", "Done", "Verified")
IMPLEMENTED = ("Partial", "Done", "Verified")
ADDRESS = re.compile(r"(?<![0-9A-Fa-f])[0-9A-Fa-f]{8}(?![0-9A-Fa-f])")


def inventory_draws(text: str) -> set[str]:
    found: set[str] = set()
    for line in text.splitlines():
        if not line.startswith("| ") or line.startswith("| ---") or line.startswith("| Elemento"):
            continue
        columns = [part.strip() for part in line.split("|")[1:-1]]
        if len(columns) != 6:
            continue
        # The Draw column can include variants (Fusion) and chained helpers.
        for address in ADDRESS.findall(columns[3].split("→", 1)[0]):
            found.add(address.upper())
    return found


def load() -> list[dict]:
    source = json.loads(MANIFEST.read_text(encoding="utf-8"))
    if source.get("schemaVersion") != 1 or not isinstance(source.get("functions"), list):
        raise ValueError("Unsupported visual coverage manifest")
    seen: set[str] = set()
    for row in source["functions"]:
        if not isinstance(row, dict):
            raise ValueError("Invalid visual coverage row")
        address, family, status = [row.get(k) for k in ("address", "family", "status")]
        if (not isinstance(address, str) or not re.fullmatch(r"[0-9A-F]{8}", address)
                or address in seen or not isinstance(family, str) or not family
                or status not in STATUSES):
            raise ValueError(f"Duplicate or invalid native Draw: {row!r}")
        seen.add(address)
        client = row.get("client")
        if status in IMPLEMENTED:
            if (not isinstance(client, str) or not client.startswith("src/Joey.App/")
                    or not client.endswith(".cs") or not (ROOT / client).is_file()):
                raise ValueError(f"Missing C# implementation reference: {address}")
        elif client is not None:
            raise ValueError(f"Pending function has implementation reference: {address}")
    expected = inventory_draws(INVENTORY.read_text(encoding="utf-8"))
    if seen != expected:
        raise ValueError(f"Draw inventory mismatch: missing={sorted(expected - seen)} "
                         f"extra={sorted(seen - expected)}")
    return source["functions"]


def generated() -> tuple[dict[Path, str], dict[str, int | float]]:
    rows = load()
    total = len(rows)
    covered = sum(x["status"] in IMPLEMENTED for x in rows)
    verified = sum(x["status"] == "Verified" for x in rows)
    percent = round(100 * covered / total, 2) if total else 0.0
    stats = {"covered": covered, "total": total, "verified": verified, "percent": percent}
    badge = {"schemaVersion": 1, "label": "VISUALS",
             "message": f"{covered}/{total} Draw ({percent:.2f}%)",
             "color": "brightgreen" if percent >= 70 else "blue" if percent >= 20 else "orange"}
    tick = chr(96)
    lines = [
        "# Cobertura de Draw nativas — YugiRE", "",
        "Denominador: **rotinas Draw nativas distintas identificadas** em",
        "[native-renderer-inventory.md](native-renderer-inventory.md). Uma rotina",
        "compartilhada por várias famílias conta uma vez. Funções Draw ainda",
        "desconhecidas não entram no denominador, que poderá aumentar.", "",
        f"**Implementadas (inclusive parciais): {covered}/{total} ({percent:.2f}%).**",
        f"**Validadas no Joey original: {verified}/{total}.**", "",
        "Partial indica um componente visual C# relacionado, mas divergências",
        "conhecidas. Done exige escopo visual concluído em C#; Verified exige",
        "comparação com o executável original. Esta badge mede implementação,",
        "**não fidelidade pixel-perfect comprovada**.", "",
        "**Não cobre toda a UI:** seleções, diálogos e outros objetos com Draw",
        "desconhecido ainda não entram neste denominador. Ver",
        "[famílias visuais em investigação](../research/visual-unmapped-families-2026-11-21.md).", "" ,
        "| VA Draw | Família | Estado | Implementação C# |",
        "| --- | --- | --- | --- |",
    ]
    for row in rows:
        client = tick + row["client"] + tick if "client" in row else "—"
        lines.append(f"| {tick}{row['address']}{tick} | {row['family']} | {row['status']} | {client} |")
    lines += ["", "O conjunto de endereços deve corresponder ao inventário.",
              "Diálogos e ícones com Draw desconhecido permanecem listados no",
              "inventário, mas não inflam a porcentagem.", ""]
    return {
        DOCS / "badges/visual.json": json.dumps(badge, indent=2, ensure_ascii=False) + "\n",
        DOCS / "VISUAL_COVERAGE.md": "\n".join(lines),
    }, stats


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args(argv)
    outputs, stats = generated()
    stale = []
    for path, contents in outputs.items():
        if args.check:
            if not path.is_file() or path.read_text(encoding="utf-8") != contents:
                stale.append(str(path.relative_to(ROOT)))
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(contents, encoding="utf-8")
    if stale:
        parser.error("Generated files out of date: " + ", ".join(stale))
    print(f"Visual Draw coverage: {stats['covered']}/{stats['total']} "
          f"({stats['percent']:.2f}%); Verified={stats['verified']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
