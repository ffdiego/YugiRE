#!/usr/bin/env python3
"""A09 DAT smoke: same console script in Lab, Classic and Instant.
Run on a desktop with .NET 10 and private data.dat. Close each App only
after the script completes. Output stays under scratch; no native EXE used.
"""
import argparse
import difflib
from pathlib import Path
import re
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[2]
SCENARIO = ROOT / "scripts/fixtures/duel-console-combat-smoke.txt"
ROW = re.compile(r"^(\d+: |(?:[-+] )?(?:turn=|queue busy=|legacy choice:|side [01] ))")
EXEC = re.compile(r"^(\d+): (\w+) (\w+)(?: |$)")
TIMING = re.compile(r"^\[lab timing\] ms=(\d+) seq=(\d+) stage=(Started|Completed) command=(\w+)$")


def inspect_log(path, graphical):
    lines = Path(path).read_text(encoding="utf-8", errors="replace").splitlines()
    if lines.count("Script completed.") != 1:
        raise ValueError(f"{path}: expected exactly one completed script")
    if any(line.startswith("[lab wait]") for line in lines):
        raise ValueError(f"{path}: scheduler reported a stall; inspect the log")
    rows = [line.rstrip() for line in lines if ROW.match(line)]
    executed = [(int(m[1]), m[2], m[3]) for line in rows if (m := EXEC.match(line))]
    if [seq for seq, _, _ in executed] != list(range(1, len(executed) + 1)):
        raise ValueError(f"{path}: missing or duplicate executed commands")
    if any(outcome != "Applied" for _, _, outcome in executed):
        raise ValueError(f"{path}: non-applied command")
    names = [name for _, name, _ in executed]
    if names.count("DeclareDirectAttackCommand") != 1 or names.count("DeclareTargetedAttackCommand") != 1:
        raise ValueError(f"{path}: both attack declarations are required")
    direct = names.index("DeclareDirectAttackCommand")
    targeted = names.index("DeclareTargetedAttackCommand")
    if targeted <= direct or names[direct + 1:direct + 2] != ["DamageCommand"]:
        raise ValueError(f"{path}: missing direct attack damage")
    # Both combatants use the same positive-ATK card in attack position.
    if names[targeted + 1:targeted + 5] != [
        "SnapshotBattleRemovalCommand", "SnapshotBattleRemovalCommand",
        "FinishBattleRemovalCommand", "FinishBattleRemovalCommand"
    ]:
        raise ValueError(f"{path}: expected two removals from equal-ATK combat")
    queues = [row for row in rows if row.startswith("queue busy=")]
    if not queues or queues[-1] != "queue busy=False pending=0 active=- decision=- fault=False":
        raise ValueError(f"{path}: no idle healthy queue snapshot")
    if graphical:
        timing = [(int(m[1]), int(m[2]), m[3], m[4]) for line in lines if (m := TIMING.match(line))]
        expected = [(seq, stage, name) for seq, name, _ in executed for stage in ("Started", "Completed")]
        if [(seq, stage, name) for _, seq, stage, name in timing] != expected:
            raise ValueError(f"{path}: missing timing or next command started before completion")
        if any(a[0] > b[0] for a, b in zip(timing, timing[1:])):
            raise ValueError(f"{path}: non-monotonic clock")
    return rows


def compare(directory):
    directory = Path(directory)
    reference = inspect_log(directory / "headless.txt", False)
    for mode in ("classic", "instant"):
        actual = inspect_log(directory / f"{mode}.txt", True)
        if actual != reference:
            diff = "\n".join(difflib.unified_diff(reference, actual, fromfile="headless", tofile=mode, lineterm=""))
            raise ValueError(f"{mode}: trace/state mismatch\n{diff}")
    print("Headless, Classic and Instant: matching executed trace and state; serial timing logs.")
    print("Inspect sword cue and removals manually; this is not a native timing or pixel comparison.")


def run_logged(args, path):
    with path.open("x", encoding="utf-8") as output:
        with subprocess.Popen(args, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                              text=True, encoding="utf-8", errors="replace") as process:
            for line in process.stdout:
                output.write(line)
                output.flush()
                print(line, end="", flush=True)
            code = process.wait()
    if code:
        raise ValueError(f"{path}: process exited with status {code}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--dat", type=Path)
    group.add_argument("--logs", type=Path, help="Compare an existing output directory without running the App")
    args = parser.parse_args()
    try:
        if args.logs is not None:
            compare(args.logs)
            return 0
        dat = args.dat.resolve(strict=True)
        base = ROOT / "scratch/actions-combat"
        base.mkdir(parents=True, exist_ok=True)
        directory = Path(tempfile.mkdtemp(prefix="smoke-", dir=base))
        print(f"Private logs: {directory}", flush=True)
        run_logged(["dotnet", "run", "--project", "src/Joey.Lab", "--",
                    "duel-console", "--dat", str(dat), str(SCENARIO)], directory / "headless.txt")
        inspect_log(directory / "headless.txt", False)
        for mode in ("classic", "instant"):
            print(f"{mode}: close the window only after 'Script completed.'", flush=True)
            command = ["dotnet", "run", "--project", "src/Joey.App", "--",
                       str(dat), f"--duel-script={SCENARIO}"]
            if mode == "instant":
                command.append("--instant")
            run_logged(command, directory / f"{mode}.txt")
            inspect_log(directory / f"{mode}.txt", True)
        compare(directory)
        return 0
    except (OSError, ValueError) as error:
        print(error)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
