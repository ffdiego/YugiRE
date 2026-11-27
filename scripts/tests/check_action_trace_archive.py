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


def check_script_checkpoints(lab: Path, folder: Path) -> None:
    script = folder / "checkpoint-script.txt"
    trace = folder / "script-trace.json"
    checkpoints = folder / "checkpoints.json"
    script.write_text("# scenario\nturn draw\nset 0 2\ndamage 200 1\n", encoding="utf-8")
    subprocess.run([
        "dotnet", str(lab), "duel-console", "--fixture", str(script),
        f"--trace-output={trace}", f"--checkpoint-output={checkpoints}",
    ], capture_output=True, check=True, text=True)
    document = json.loads(checkpoints.read_text(encoding="utf-8"))
    assert document["schema"] == "yugire-duel-checkpoints-v1" and document["complete"], document
    initial, drawn, placed, damaged = document["checkpoints"]
    assert [row["boundary"]["line"] for row in document["checkpoints"]] == [None, 2, 3, 4], document
    first_card = initial["sides"][0]["deck"][0]
    assert drawn["sides"][0]["hand"] == [first_card], drawn
    hidden = placed["sides"][0]["monsters"][2]
    assert hidden == {"card": first_card, "faceDown": True, "defense": True,
                      "attack": None, "defenseValue": None}, hidden
    assert damaged["sides"][1]["life"] == 7800, damaged
    executed = json.loads(trace.read_text(encoding="utf-8"))["rows"]
    assert damaged["boundary"]["lastExecutedSequence"] == executed[-1]["sequence"], damaged
    assert [row["boundary"]["lastExecutedSequence"] for row in document["checkpoints"]][0] is None

    failure = folder / "incomplete-checkpoints.json"
    script.write_text("damage 200 1\nnot-a-command\n", encoding="utf-8")
    result = subprocess.run([
        "dotnet", str(lab), "duel-console", "--fixture", str(script), f"--checkpoint-output={failure}",
    ], capture_output=True, text=True)
    incomplete = json.loads(failure.read_text(encoding="utf-8"))
    assert result.returncode == 1 and not incomplete["complete"], (result.stderr, incomplete)
    assert [row["boundary"]["line"] for row in incomplete["checkpoints"]] == [None, 1], incomplete
    print("Script checkpoints: initial state, occurrence movement, trace correlation and incomplete prefix passed.")


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
        check_script_checkpoints(lab, Path(directory))

    print("Archive comparison: four projected commands agree, unsupported rows remain visible, and a changed pilot reports differences.")


if __name__ == "__main__":
    main()
