#!/usr/bin/env python3
"""Exercise the built Lab exporter and the comparison CLI against the archived window."""
import argparse
import json
from pathlib import Path
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[2]
ARCHIVE = ROOT / "docs/evidence/replays/relinquished_commands.csv"


def compare_pilot(lab: Path, trace: Path, archived_window: bool) -> tuple[int, dict]:
    arguments = ["dotnet", str(lab), "action-trace-battle-pilot"]
    if archived_window:
        arguments.append("--archived-window")
    with trace.open("w", encoding="utf-8") as output:
        subprocess.run(arguments, check=True, stdout=output)
    process = subprocess.run([
        sys.executable, str(ROOT / "scripts/compare_joey_action_trace.py"), str(ARCHIVE), str(trace),
        "--native-start", "281", "--native-count", "21", "--cs-start", "0", "--cs-count", "8",
    ], capture_output=True, text=True)
    if not process.stdout:
        raise RuntimeError(process.stderr.strip() or "Comparison emitted no report.")
    return process.returncode, json.loads(process.stdout)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("lab", type=Path, help="Path to the built Joey.Lab.dll")
    lab = parser.parse_args().lab.resolve(strict=True)
    with tempfile.TemporaryDirectory() as directory:
        trace = Path(directory) / "battle.json"
        code, report = compare_pilot(lab, trace, archived_window=True)
        assert (code, report["status"]) == (3, "inconclusive"), report
        assert not report["differences"], report
        assert [row["nativeIndex"] for row in report["nativeProjected"]] == [281, 290, 296, 301], report
        assert report["csharpMappedUnits"] == 4 and report["csharpComplete"], report
        assert len(report["nativeUnmapped"]) == 17 and len(report["csharpUnmapped"]) == 4, report

        code, shifted = compare_pilot(lab, trace, archived_window=False)
        assert (code, shifted["status"]) == (1, "mismatch"), shifted
        assert [diff["position"] for diff in shifted["differences"]] == list(range(4)), shifted

    print("Archive comparison: four projected commands agree, unsupported rows remain visible, and a changed pilot reports differences.")


if __name__ == "__main__":
    main()
