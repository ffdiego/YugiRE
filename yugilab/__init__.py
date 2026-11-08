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


BACKEND_DIR = Path(__file__).resolve().parent.parent / "native_rules"


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
    parser.add_argument("--trace-ai", action="store_true",
                        help="Also observe AI selectors, evaluations and hand snapshots")
    parser.add_argument("--allow-multi-instance", action="store_true",
                        help="Enable the existing per-PID mutex bypass")
    return parser


def _run_native_launcher(arguments: list[str]) -> int:
    # Keep the tested Native Rules/Frida implementation as the backend.
    # No duplicated hooks, PE validation, or process-management logic.
    if str(BACKEND_DIR) not in sys.path:
        sys.path.insert(0, str(BACKEND_DIR))
    import native_rules
    return native_rules.main(arguments)


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
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
    if args.allow_multi_instance:
        forwarded.append("--allow-multi-instance")
    print(f"[yugilab] Capturing {args.duels} duels. "
          "YugiLab will terminate only the Joey PID it spawns.",
          flush=True)
    return _run_native_launcher(forwarded)
