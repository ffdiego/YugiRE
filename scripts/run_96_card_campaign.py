"""Safely run the committed 96-card campaign as bounded YugiLab state pilots.

No execution unless --execute. Each invocation uses <=3 duels, exactly
one worker and --trace-state. Failure or incomplete footer aborts the batch.
No EXE patching and no arbitrary PID termination; YugiLab owns its processes.
"""
from __future__ import annotations

import argparse
from collections import Counter
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import shlex
import subprocess
import sys

ROOT_PATH = Path(__file__).resolve().parents[1]
if str(ROOT_PATH) not in sys.path:
    sys.path.insert(0, str(ROOT_PATH))
from scripts.prepare_96_card_campaign import ROOT, prepare

EXPECTED_JOEY_SHA256 = (
    "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de"
)
DECK_FOLDER = ROOT / "native_hooks/decks/mass-96"
PLAN_FILE = DECK_FOLDER / "campaign-plan.json"


def load_plan() -> dict:
    committed = json.loads(PLAN_FILE.read_text(encoding="utf-8"))
    expected, decks = prepare()
    if committed != expected:
        raise ValueError("Committed 96-card plan differs from the catalog-derived plan")
    for name, deck in decks.items():
        source = json.loads((DECK_FOLDER / name).read_text(encoding="utf-8"))
        if source != deck:
            raise ValueError(f"Deck has changed since catalog-based preparation: {name}")
    return committed


def run_specs(plan: dict, *, cohorts: list[int] | None = None,
              repeats: int = 1, duels: int = 3, seed_offset: int = 0) -> list[dict]:
    if type(repeats) is not int or not 1 <= repeats <= 20:
        raise ValueError("repeats must be 1..20")
    if type(duels) is not int or not 1 <= duels <= 3:
        raise ValueError("--trace-state requires 1..3 duels and one worker per run")
    if type(seed_offset) is not int or not 0 <= seed_offset < 2**32:
        raise ValueError("seed offset must be uint32")
    eligible = {c["number"]: c for c in plan["cohorts"]}
    selected = list(eligible) if not cohorts else sorted(set(cohorts))
    if not selected or any(i not in eligible for i in selected):
        raise ValueError("Cohorts must be selected from 1..12")
    specs = []
    for number in selected:
        cohort = eligible[number]
        for rep in range(1, repeats+1):
            # Deterministic, disjoint 32-bit seeds for distinct rounds.
            seed = (plan["seed"] + seed_offset + number*100003 + rep*509) & 0xFFFFFFFF
            specs.append(dict(name=f"cohort-{number:02d}-rep-{rep:02d}",
                              cohort=number, repetition=rep, duels=duels, seed=seed,
                              deckA=DECK_FOLDER / cohort["decks"]["A"],
                              deckB=DECK_FOLDER / cohort["decks"]["B"]))
    return specs


def build_command(spec: dict, *, exe: Path, python: Path,
                  output_dir: Path, speed: int = 600) -> list[str]:
    if speed < 1:
        raise ValueError("speed must be positive")
    return [
        str(python), "-m", "yugilab", "--exe", str(exe),
        "--duels", str(spec["duels"]), "--workers", "1",
        "--trace-state", "--deck-0", str(spec["deckA"]),
        "--deck-1", str(spec["deckB"]),
        "--seed", str(spec["seed"]), "--speed-value", str(speed),
        "--output-dir", str(output_dir),
    ]


def inspect_capture(folder: Path, expected_duels: int) -> dict:
    sources = sorted(folder.rglob("*.jsonl"))
    if len(sources) != 1:
        return {"complete": False, "reason": f"Expected exactly 1 JSONL, found {len(sources)}"}
    source = sources[0]
    try:
        with source.open(encoding="utf-8") as stream:
            first = json.loads(stream.readline())
        with source.open("rb") as stream:
            stream.seek(0, 2)
            stream.seek(max(0, stream.tell() - 65536))
            rows = stream.read().splitlines()
        last = json.loads(next(row for row in reversed(rows) if row.strip()))
    except (OSError, ValueError, StopIteration, UnicodeDecodeError) as exc:
        return {"complete": False, "reason": f"Unreadable capture: {exc}"}
    state = last.get("stateTrace", {}) if isinstance(last, dict) else {}
    plans = first.get("deckPlans", {}) if isinstance(first, dict) else {}
    verified = {"0": 0, "1": 0}
    override_errors = []
    # Inspect only deck_override event lines. This avoids parsing hundreds
    # of thousands of unrelated native command/state events a second time.
    try:
        with source.open("rb") as stream:
            for raw in stream:
                if b"deck_override" not in raw:
                    continue
                item = json.loads(raw)
                if item.get("kind") != "deck_override":
                    continue
                phase = item.get("phase")
                side = str(item.get("side"))
                if phase in ("failed", "mismatch"):
                    override_errors.append(f"Native deck override {phase}, side {side}")
                if phase != "verified":
                    continue
                planned = (plans.get(side) or {}).get("main")
                actual = item.get("actualIndices")
                if (side not in verified or not isinstance(planned, list)
                    or not isinstance(actual, list)
                    or Counter(planned) != Counter(actual)):
                    override_errors.append(f"Deck identity mismatch, side {side}")
                else:
                    verified[side] += 1
    except (OSError, ValueError, UnicodeDecodeError) as exc:
        override_errors.append(f"Could not inspect deck overrides: {exc}")
    deck_ok = not override_errors and all(
        verified[str(side)] >= expected_duels for side in (0, 1))
    ok = (
        isinstance(first, dict) and first.get("kind") == "metadata" and
        isinstance(last, dict) and last.get("kind") == "capture_summary" and
        first.get("requestedDuels") == expected_duels and
        last.get("complete") is True and
        last.get("capturedDuels") == expected_duels and
        last.get("overwritten") == 0 and isinstance(state, dict) and
        state.get("samples", 0) > 0 and state.get("baselines", 0) >= expected_duels and
        state.get("readErrors", 0) == 0 and deck_ok
    )
    return {
        "complete": ok, "source": str(source),
        "capturedDuels": last.get("capturedDuels"),
        "eventsWritten": last.get("eventsWritten"),
        "stateTrace": state,
        "verifiedDeckOverrides": verified, "deckOverrideErrors": override_errors,
        "reason": None if ok else
            "Footer, state trace, native deck identity or baseline gate failed",
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--exe", type=Path, default=Path("joey_pc.exe"))
    parser.add_argument("--python", type=Path, default=Path(sys.executable))
    parser.add_argument("--output-root", type=Path,
                        default=ROOT / "scratch/mass96-pilot")
    parser.add_argument("--cohort", type=int, action="append", default=[])
    parser.add_argument("--repeats", type=int, default=1)
    parser.add_argument("--duels-per-run", type=int, default=3)
    parser.add_argument("--seed-offset", type=int, default=0)
    parser.add_argument("--speed-value", type=int, default=600)
    parser.add_argument("--execute", action="store_true",
                        help="Opt in to real native Joey duels (default: preview)")
    args = parser.parse_args(argv)
    try:
        plan = load_plan()
        jobs = run_specs(plan, cohorts=args.cohort, repeats=args.repeats,
                         duels=args.duels_per_run, seed_offset=args.seed_offset)
        if args.speed_value < 1:
            raise ValueError("speed-value must be positive")
        print(f"96-card campaign: {len(jobs)} jobs, "
              f"{sum(x['duels'] for x in jobs)} native duels requested, "
              f"{len({x['cohort'] for x in jobs})} cohorts")
        output_root = args.output_root.resolve()
        if not args.execute:
            for spec in jobs[:3]:
                command = build_command(spec, exe=args.exe, python=args.python,
                    output_dir=output_root / spec["name"], speed=args.speed_value)
                print("PREVIEW", subprocess.list2cmdline(command)
                      if sys.platform == "win32" else shlex.join(command))
            print("No native processes started. Supply --execute to run.")
            return 0

        exe = args.exe.resolve()
        python = args.python.resolve()
        if not exe.is_file() or exe.name.lower() != "joey_pc.exe":
            raise ValueError("--exe must point to an existing joey_pc.exe")
        if not python.is_file():
            raise ValueError("--python must point to the Frida virtualenv Python")
        if hashlib.sha256(exe.read_bytes()).hexdigest() != EXPECTED_JOEY_SHA256:
            raise ValueError("joey_pc.exe SHA256 differs from known approved binary")
        if output_root.exists():
            raise ValueError("Refusing to overwrite an existing output root")
        # Only write into scratch/ inside the repository; outside is allowed.
        try:
            relative = output_root.relative_to(ROOT.resolve())
        except ValueError:
            pass
        else:
            if not relative.parts or relative.parts[0] != "scratch":
                raise ValueError("Inside repository, captures must go under scratch/")
        output_root.mkdir(parents=True)
        (output_root / "logs").mkdir()
        report = {
            "schema": "mass96-pilot-runs-v1",
            "createdUtc": datetime.now(timezone.utc).isoformat(),
            "requested": len(jobs), "requestedDuels": sum(x["duels"] for x in jobs),
            "campaignStatus": "InProgress", "runs": [],
        }
        for spec in jobs:
            folder = output_root / spec["name"]
            log = output_root / "logs" / f"{spec['name']}.log"
            cmd = build_command(spec, exe=exe, python=python,
                                output_dir=folder, speed=args.speed_value)
            print(f"RUN {spec['name']} (seed {spec['seed']}, {spec['duels']} duels)",
                  flush=True)
            with log.open("w", encoding="utf-8") as stream:
                rc = subprocess.run(cmd, cwd=ROOT, stdin=subprocess.DEVNULL,
                                    stdout=stream, stderr=subprocess.STDOUT,
                                    check=False).returncode
            audit = inspect_capture(folder, spec["duels"]) if rc == 0 else {
                "complete": False, "reason": f"YugiLab exited with code {rc}",
            }
            report["runs"].append({
                "cohort": spec["cohort"], "repetition": spec["repetition"],
                "seed": spec["seed"], "duels": spec["duels"],
                "deckA": str(spec["deckA"]), "deckB": str(spec["deckB"]),
                "returnCode": rc, "log": str(log), **audit,
            })
            report["campaignStatus"] = (
                "Complete" if len(report["runs"]) == len(jobs) and audit["complete"]
                else "Failed" if not audit["complete"] else "InProgress"
            )
            (output_root / "campaign-runs.json").write_text(
                json.dumps(report, ensure_ascii=False, indent=2) + "\n",
                encoding="utf-8")
            if not audit["complete"]:
                print("STOP: capture failed integrity gate. Inspect", log, flush=True)
                return 2
        print("All captures passed footer/state gates. Card effects are still unverified.")
        return 0
    except (ValueError, OSError, KeyError) as exc:
        print(f"Campaign preflight failed: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
