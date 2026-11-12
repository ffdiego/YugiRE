"""YugiLab: supervised Joey duel captures using the Native Rules backend.

The launcher always SPAWNS the exact Joey executable after SHA256 preflight.
It owns only the PID returned by Frida. When the duel target is reached (or
an error/cancel occurs), native_rules.run_spawn flushes the JSONL and kills
that owned game process. No attaching to or killing user-supplied PIDs.
"""
from __future__ import annotations

import argparse
from pathlib import Path
import sys


BACKEND_DIR = Path(__file__).resolve().parent.parent / "native_hooks"


def _positive_int(raw: str) -> int:
    try:
        value = int(raw)
    except ValueError as exc:
        raise argparse.ArgumentTypeError("duel count must be an integer") from exc
    if value < 1:
        raise argparse.ArgumentTypeError("duel count must be at least 1")
    return value


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="yugilab",
        description="Run a finite, supervised Joey demo laboratory. "
                    "Creates and owns a new Joey PID, writes a continuous "
                    "JSONL, and closes the game after N duels.")
    parser.add_argument("--exe", type=Path, required=True,
                        help="Path to the ORIGINAL joey_pc.exe (SHA256 preflight enforced)")
    parser.add_argument("--duels", "-n", type=_positive_int, required=True,
                        help="Number of completed demo duels (terminal opcode 0x0004)")
    parser.add_argument("--speed-value", type=int, default=2400,
                        help="Pre-boot -speedy override; default: 2400")
    parser.add_argument("--deck-0", type=Path,
                        help="JSON deck with internal card indices for side 0")
    parser.add_argument("--deck-1", type=Path,
                        help="JSON deck with internal card indices for side 1")
    parser.add_argument("--seed", type=lambda s: int(s, 0), default=None,
                        help="Optional uint32 master seed for reproducible AI demo duels")
    parser.add_argument("--seed-start-index", type=int, default=0,
                        help=argparse.SUPPRESS)
    parser.add_argument("--trace-ai", action="store_true",
                        help="Also observe AI selectors, evaluations and hand snapshots")
    parser.add_argument("--allow-multi-instance", action="store_true",
                        help="Enable the existing per-PID mutex bypass")
    parser.add_argument("--workers", type=_positive_int, default=1,
                        help="Run up to 16 independent Joey workers in parallel (default: 1)")
    parser.add_argument("--stagger-seconds", type=float, default=1.1,
                        help="Wait between worker launches; default: 1.1s")
    parser.add_argument("--output-dir", type=Path, default=None,
                        help="Optional capture directory, or base directory for parallel runs")
    parser.add_argument("--cancel-file", type=Path, default=None,
                        help=argparse.SUPPRESS)
    return parser


def _run_native_launcher(arguments: list[str]) -> int:
    # Keep the tested Native Rules/Frida implementation as the backend.
    # No duplicated hooks, PE validation, or process-management logic.
    if str(BACKEND_DIR) not in sys.path:
        sys.path.insert(0, str(BACKEND_DIR))
    import native_rules
    return native_rules.main(arguments)


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    if args.workers > 16:
        parser.error("--workers must be <= 16 (test 2 or 4 before 10)")
    if args.workers > args.duels:
        parser.error("--workers cannot exceed --duels")
    if not 0 <= args.stagger_seconds <= 30:
        parser.error("--stagger-seconds must be between 0 and 30")
    if args.seed is not None:
        if not 0 <= args.seed <= 0xFFFFFFFF:
            parser.error("--seed must be an unsigned 32-bit integer")
        if (args.seed_start_index < 0 or args.seed_start_index > 0xFFFFFFFF or
                args.seed_start_index + args.duels > 0x100000000):
            parser.error("--seed-start-index + --duels exceeds uint32 range")
    elif args.seed_start_index != 0:
        parser.error("--seed-start-index requires --seed")

    if args.workers > 1:
        # Reject bad binaries or decks BEFORE launching several game processes.
        if str(BACKEND_DIR) not in sys.path:
            sys.path.insert(0, str(BACKEND_DIR))
        from pe_preflight import probe
        from deck_config import read_deck_file
        try:
            args.exe = args.exe.expanduser().resolve(strict=True)
            if args.exe.name.casefold() != "joey_pc.exe" or not args.exe.is_file():
                parser.error("--exe must point to an existing joey_pc.exe")
            if not probe(args.exe)["passed"]:
                parser.error("Joey PE32/SHA256 preflight failed")
            for deck in (args.deck_0, args.deck_1):
                if deck is not None:
                    read_deck_file(deck)
            if args.deck_0 is not None:
                args.deck_0 = args.deck_0.expanduser().resolve(strict=True)
            if args.deck_1 is not None:
                args.deck_1 = args.deck_1.expanduser().resolve(strict=True)
        except (OSError, ValueError) as exc:
            parser.error(str(exc))
        from .parallel import run_parallel
        root = (args.output_dir or
                (Path(__file__).resolve().parent.parent / "scratch" / "labs"))
        return run_parallel(args, root.expanduser().resolve())

    if args.cancel_file is not None and args.cancel_file.exists():
        print("[yugilab] Worker cancelled before spawn.", flush=True)
        return 130
    forwarded = [
        "--spawn", "--observe-only",
        "--exe", str(args.exe),
        "--duels", str(args.duels),
        "--speed-value", str(args.speed_value),
    ]
    for side, deck in ((0, args.deck_0), (1, args.deck_1)):
        if deck is not None:
            forwarded += [f"--deck-{side}", str(deck)]
    if args.trace_ai:
        forwarded.append("--trace-ai")
    if args.seed is not None:
        forwarded += ["--seed", str(args.seed),
                      "--seed-start-index", str(args.seed_start_index)]
    if args.allow_multi_instance:
        forwarded.append("--allow-multi-instance")
    if args.output_dir is not None:
        forwarded += ["--output-dir", str(args.output_dir)]
    if args.cancel_file is not None:
        forwarded += ["--cancel-file", str(args.cancel_file)]
    print(f"[yugilab] Capturing {args.duels} duels. "
          "YugiLab will terminate only the Joey PID it spawns.",
          flush=True)
    return _run_native_launcher(forwarded)
