"""Verify Sparks/Raimei rows against the already-versioned YugiRE card catalogue.

Run from native_rules:
    python tests/test_catalog.py
"""
from __future__ import annotations

import json
from pathlib import Path

HERE = Path(__file__).resolve()
ROOT = HERE.parents[2]
CATALOG = ROOT / "docs" / "evidence" / "card-callback-catalog.json"


def main() -> int:
    catalog = json.loads(CATALOG.read_text(encoding="utf-8"))
    rows = catalog["records"]
    expected = {66: (342, 755, "Sparks"), 172: (1007, 1049, "Raimei")}
    for row, (resource_id, index, name) in expected.items():
        record = rows[row]
        assert record["row"] == row
        assert record["resource_id"] == resource_id
        assert any(item["index"] == index and item["name"] == name
                   for item in record["identities"])
        assert record["profile"] == "P045"
    print("PASS: row 66 Sparks and row 172 Raimei map to expected IDs/profile")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
