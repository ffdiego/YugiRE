"""Convenient supervised Joey capture launcher (Windows, no separate hooks).

Examples (from repository root):
    python scripts/duelos.py
    python scripts/duelos.py scratch/A.json scratch/B.json --battle-attributes
    python scripts/duelos.py --duels 10 --workers 2 --no-state

Uses yugilab's original SHA256 preflight, process ownership and footer checks.
"""
from __future__ import annotations

import argparse
import os
from pathlib import Path
import shlex
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_DECK_0 = ROOT / "native_hooks" / "decks" / "demo-A.json"
DEFAULT_DECK_1 = ROOT / "native_hooks" / "decks" / "demo-B.json"
DEFAULT_EXE = ROOT.parent / "passion" / "joey_pc.exe"
DEFAULT_OUTPUT = ROOT / "scratch" / "captures"


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Capture three Joey demo duels with state deltas and named JSONL output."
    )
    parser.add_argument("deck0", nargs="?", type=Path, default=DEFAULT_DECK_0,
                        help="Side 0 JSON deck (default: demo-A.json)")
    parser.add_argument("deck1", nargs="?", type=Path, default=DEFAULT_DECK_1,
                        help="Side 1 JSON deck (default: demo-B.json)")
    parser.add_argument("--exe", type=Path, default=DEFAULT_EXE,
                        help="Verified original joey_pc.exe (default: ../passion/joey_pc.exe)")
    parser.add_argument("--python", type=Path, default=None,
                        help="Python with Frida; auto-detects native_hooks/.venv-frida")
    parser.add_argument("--duels", type=int, default=3)
    parser.add_argument("--workers", type=int, default=1)
    parser.add_argument("--speed-value", type=int, default=2400)
    parser.add_argument("--seed", type=lambda value: int(value, 0), default=12345)
    parser.add_argument("--output-dir", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--no-state", action="store_true",
                        help="Capture commands only; permits >3 duels and multiple workers")
    parser.add_argument("--battle-attributes", action="store_true",
                        help="Also sample calculated ATK/DEF at opcode 0x31 (state trace required)")
    parser.add_argument("--trace-ai", action="store_true",
                        help="Opt-in AI trace (requires --no-state)")
    parser.add_argument("--dry-run", action="store_true",
                        help="Show the backend command without spawning the game")
    return parser


def select_python(explicit: Path | None = None) -> Path:
    if explicit is not None:
        return explicit.expanduser().resolve()
    for python in (ROOT / "native_hooks" / ".venv-frida" / "Scripts" / "python.exe",
                   ROOT / "native_hooks" / ".venv-frida" / "bin" / "python"):
        if python.is_file():
            return python
    return Path(sys.executable)


def build_command(args: argparse.Namespace, python: Path) -> list[str]:
    cmd = [str(python), "-m", "yugilab",
           "--exe", str(args.exe.expanduser().resolve()),
           "--duels", str(args.duels),
           "--workers", str(args.workers),
           "--speed-value", str(args.speed_value),
           "--seed", str(args.seed),
           "--deck-0", str(args.deck0.expanduser().resolve()),
           "--deck-1", str(args.deck1.expanduser().resolve()),
           "--output-dir", str(args.output_dir.expanduser().resolve())]
    if not args.no_state:
        cmd.append("--trace-state")
    if args.battle_attributes:
        cmd.append("--trace-battle-attributes")
    if args.trace_ai:
        cmd.append("--trace-ai")
    return cmd


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    if args.duels < 1 or args.workers < 1 or args.workers > 16 or args.workers > args.duels:
        parser.error("Require --duels >= 1 and 1 <= --workers <= min(16, duels)")
    if args.speed_value < 1 or args.speed_value > 0xFFFFFFFF:
        parser.error("--speed-value must fit a positive uint32")
    if not 0 <= args.seed <= 0xFFFFFFFF:
        parser.error("--seed must fit uint32")
    if not args.no_state and (args.duels > 3 or args.workers != 1):
        parser.error("State trace currently requires 1 worker and <=3 duels; use --no-state for larger batches")
    if args.battle_attributes and args.no_state:
        parser.error("--battle-attributes requires state trace (omit --no-state)")
    if args.trace_ai and not args.no_state:
        parser.error("--trace-ai requires --no-state in this supervised pilot")

    python = select_python(args.python)
    cmd = build_command(args, python)
    if args.dry_run:
        print(" ".join(shlex.quote(x) for x in cmd))
        return 0
    if os.name != "nt":
        parser.error("Native Joey capture requires Windows; use --dry-run to inspect command on other hosts")
    for name, path in (("exe", args.exe), ("deck0", args.deck0), ("deck1", args.deck1),
                       ("python", python)):
        if not path.expanduser().is_file():
            parser.error(f"{name} missing: {path}")
    print("[duelos] Running supervised YugiLab with named JSONL capture.", flush=True)
    print("[duelos] Only the Joey PID created by this run may be terminated.", flush=True)
    return subprocess.run(cmd, cwd=ROOT, check=False).returncode


if __name__ == "__main__":
    raise SystemExit(main())
