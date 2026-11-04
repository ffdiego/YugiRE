"""Install Sparks and Raimei C++ callbacks together, with guarded rollback.

Requirements: pe_preflight.py in this directory and Python with the Frida package.
The original Joey EXE SHA256 is checked against the *loaded* process module.
Only the primary callback pointers of resources 342 and 1007 are modified.
Without flags, automatically locates Joey and uses the validated draw-five
Sparks and damage-plus-draw Raimei effects (400 LP).
Both pointer slots are restored at the end; the DLL stays loaded until Joey
exits to avoid dangling code addresses. Never attach two patchers at once.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import struct

from pe_preflight import parse_pe, probe

HERE = Path(__file__).resolve().parent
DEFAULT_DLL = HERE / "build-vs2026" / "Release" / "yugire_rules.dll"
MODES = {"damage": 1, "draw": 2, "damage-draw": 3}
MODE_NAMES = {value: name for name, value in MODES.items()}


def check_dll(dll: Path) -> None:
    if not dll.is_file():
        raise ValueError(f"DLL not found: {dll}\nBuild native_rules or pass --dll")
    data = dll.read_bytes()
    try:
        details = parse_pe(data)
    except (ValueError, struct.error) as exc:
        raise ValueError(f"Invalid DLL PE: {exc}") from exc
    if details["machine"] != 0x014C:
        raise ValueError(f"Expected 32-bit x86 DLL (PE machine 014C): {dll}")
    pe_offset = struct.unpack_from("<I", data, 0x3C)[0]
    characteristics = struct.unpack_from("<H", data, pe_offset + 22)[0]
    if not characteristics & 0x2000:
        raise ValueError(f"Expected DLL flag in PE COFF header: {dll}")


def damage_type(raw: str) -> int:
    """Argparse validator for the experimental LP damage range 1..9999."""
    try:
        value = int(raw, 10)
    except ValueError as exc:
        raise argparse.ArgumentTypeError("Damage must be an integer (1..9999 LP)") from exc
    if value < 1 or value > 9999:
        raise argparse.ArgumentTypeError("Damage must be between 1 and 9999 LP")
    return value


def show_counters(state: dict) -> None:
    for card in state["entries"]:
        mode = MODE_NAMES.get(card["configuredMode"], card["configuredMode"])
        print(f"[dual-rules] {card['name']}: calls={card['callbackCalls']} "
              f"damageCommands={card['commandsEmitted']} "
              f"drawBatches={card['drawBatches']} "
              f"mode={mode} damage={card['configuredDamage']} "
              f"currentPointer={card['currentPointer']}", flush=True)
    console = state.get("console") or {}
    if console:
        last = console.get("last")
        pending = console.get("pending")
        gate = console.get("gate")
        print(f"[console] drawBatches={console['batchesQueued']} "
              f"damageCommands={console.get('damageCommandsQueued', 0)} "
              f"pending={pending} last={last}", flush=True)
        if pending is not None:
            print(f"[console] duelQueueGate={gate}", flush=True)


def show_duel_state(snapshot: dict) -> None:
    """Human-readable, read-only view of native duel memory."""
    print("[state] Snapshot is not atomic; card indices are internal.", flush=True)
    for p in snapshot["sides"]:
        who = "CPU" if p["side"] == 0 else "Player"
        cards = ["-" if index == 0 else str(index)
                 for index in p["monsterIndices"]]
        print(f"[state] Side {p['side']} ({who}): "
              f"LP={p['lifePoints']} hand={p['handCount']} deck={p['deckCount']} "
              f"grave={p['graveyardCount']} banished={p['banishedCount']} "
              f"terminalFlags=0x{p['terminalFlags']:03X} "
              f"monsters=[{', '.join(cards)}]", flush=True)


def show_duel_diff(before: dict, after: dict) -> None:
    changes = 0
    for prev, current in zip(before["sides"], after["sides"]):
        for field in ("lifePoints", "handCount", "deckCount",
                      "graveyardCount", "banishedCount", "terminalFlags",
                      "monsterIndices"):
            if prev[field] != current[field]:
                print(f"[diff] side {current['side']} {field}: "
                      f"{prev[field]} -> {current[field]}", flush=True)
                changes += 1
    if changes == 0:
        print("[diff] No changes in the captured fields.", flush=True)


def show_help(default_side: int) -> None:
    print("[dual-rules] Commands: Enter/status = counters; "
          "draw 5 [0|1]; damage <1..9999> <0|1>; state; diff; "
          "cancel; help; stop", flush=True)
    print(f"[dual-rules] 'draw 5' defaults to side {default_side}. "
          "Side 0 = CPU, side 1 = player in the tested local duel. "
          "Damage requires an explicit target side.", flush=True)


def handle_command(command: str, api: object, default_side: int,
                   state_cache: dict | None = None) -> bool:
    """Return False only when the user requested shutdown."""
    parts = command.lower().strip().split()
    if not parts or parts == ["status"]:
        show_counters(api.status())
        return True
    if parts == ["help"]:
        show_help(default_side)
        return True
    if parts == ["stop"] or parts == ["quit"] or parts == ["exit"]:
        return False
    if parts == ["cancel"]:
        print(f"[console] {api.canceldraw()}", flush=True)
        return True
    if parts in (["state"], ["diff"]):
        snapshot = api.duelstate()
        if parts == ["diff"] and state_cache is not None and "previous" in state_cache:
            show_duel_diff(state_cache["previous"], snapshot)
        elif parts == ["diff"]:
            print("[diff] No baseline. Capturing one now; run diff again later.",
                  flush=True)
        else:
            show_duel_state(snapshot)
        if state_cache is not None:
            state_cache["previous"] = snapshot
        return True
    if parts[0] == "damage":
        if (len(parts) != 3 or not parts[1].isdecimal() or
                not (1 <= int(parts[1]) <= 9999) or parts[2] not in ("0", "1")):
            print("[console] Usage: damage <1..9999> <0|1>", flush=True)
            return True
        amount, side = int(parts[1]), int(parts[2])
        queued = api.queuedamage(side, amount)
        print(f"[console] Damage request {queued['id']} pending: "
              f"{amount} LP to side {side}. "
              "It will be queued on an idle duel update.", flush=True)
        return True
    if parts[0] == "draw":
        if len(parts) not in (2, 3) or parts[1] != "5" or (
            len(parts) == 3 and parts[2] not in ("0", "1")
        ):
            print("[console] Usage: draw 5 [0|1]", flush=True)
            return True
        side = default_side if len(parts) == 2 else int(parts[2])
        queued = api.queuedraw(side, 5)
        print(f"[console] Request {queued['id']} pending for side {side}. "
              "It will be enqueued on a later idle duel update; "
              "press Enter to check status.", flush=True)
        return True
    print(f"[console] Unknown command: {command!r}; type help", flush=True)
    return True


def resolve_joey_pid(device: object, explicit_pid: int | None = None) -> int:
    """Return the unique running joey_pc.exe PID, without guessing if ambiguous."""
    if explicit_pid is not None:
        if explicit_pid <= 0:
            raise ValueError("--pid must be a positive process ID")
        return explicit_pid

    processes = device.enumerate_processes()
    matches = sorted(
        {process.pid for process in processes
         if process.name.casefold() == "joey_pc.exe"}
    )
    if not matches:
        raise ValueError(
            "joey_pc.exe is not running. Start Joey and try again."
        )
    if len(matches) > 1:
        candidates = ", ".join(str(pid) for pid in matches)
        raise ValueError(
            f"Multiple joey_pc.exe processes found (PIDs: {candidates}). "
            "Choose one with --pid <number>."
        )
    return matches[0]


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pid", type=int, default=None,
                        help="Optional PID override when multiple Joey processes exist")
    parser.add_argument("--dll", type=Path, default=DEFAULT_DLL,
                        help="32-bit Windows yugire_rules.dll built by Visual Studio")
    parser.add_argument("--sparks", type=damage_type, default=200,
                        help="Sparks LP damage, 1..9999 (default: 200)")
    parser.add_argument("--raimei", type=damage_type, default=400,
                        help="Raimei LP damage, 1..9999 (default: 400)")
    parser.add_argument("--sparks-mode", choices=tuple(MODES), default="draw",
                        help="Sparks effect: damage, draw or damage-draw (default: draw)")
    parser.add_argument("--raimei-mode", choices=tuple(MODES), default="damage-draw",
                        help="Raimei effect: damage, draw or damage-draw (default: damage-draw)")
    parser.add_argument("--console-side", type=int, choices=(0, 1), default=1,
                        help="Default side for 'draw 5' (1 = player, 0 = CPU in tested local duel)")
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    dll_path = args.dll.resolve()

    try:
        check_dll(dll_path)
    except (OSError, ValueError) as exc:
        parser.error(str(exc))
    try:
        import frida
    except ImportError:
        parser.error("Frida not installed; use native_rules/.venv-frida")

    session = None
    script = None
    exit_code = 0
    try:
        joey_pid = resolve_joey_pid(frida.get_local_device(), args.pid)
        print(f"[dual-rules] Attaching to joey_pc.exe PID {joey_pid}", flush=True)
        session = frida.attach(joey_pid)
        source = (HERE / "yugire_dual_loader.js").read_text(encoding="utf-8")
        script = session.create_script(source)
        script.on("message", lambda message, data:
                  print(f"[frida] {message}", flush=True)
                  if message.get("type") == "error" else None)
        script.load()
        api = script.exports_sync

        module_path = Path(api.status()["modulePath"])
        checks = probe(module_path)
        if not checks["passed"]:
            print("[dual-rules] Loaded Joey EXE did not pass fingerprint checks.")
            print(json.dumps(checks["checks"], indent=2))
            return 2
        print("[dual-rules] Exact loaded Joey EXE SHA256 confirmed.", flush=True)
        print(f"[dual-rules] Loading {dll_path}", flush=True)
        state = api.install(str(dll_path), args.sparks, args.raimei,
                            MODES[args.sparks_mode], MODES[args.raimei_mode])
        print("[dual-rules] Both primary callback pointers installed.", flush=True)
        show_counters(state)
        print(f"[dual-rules] Effects: Sparks={args.sparks_mode}, "
              f"Raimei={args.raimei_mode}. Play a NORMAL duel.", flush=True)
        show_help(args.console_side)
        state_cache = {}
        while True:
            command = input("[dual-rules] > ").strip()
            try:
                if not handle_command(command, api, args.console_side, state_cache):
                    break
            except Exception as command_error:
                print(f"[console] Command failed: {command_error}", flush=True)
    except KeyboardInterrupt:
        print("\n[dual-rules] Ctrl+C: restoring original callbacks.", flush=True)
    except EOFError:
        print("\n[dual-rules] Input ended: restoring original callbacks.", flush=True)
    except Exception as exc:
        exit_code = 2
        print(f"[dual-rules] ERROR: {exc}", flush=True)
    finally:
        if script is not None:
            try:
                # Idempotent: restores a complete install or partial install.
                end = script.exports_sync.restore()
                print("[dual-rules] Restore completed; table pointers:", flush=True)
                show_counters(end)
            except Exception as exc:
                exit_code = 2
                print(f"[dual-rules] RESTORE FAILED: {exc}", flush=True)
                print("[dual-rules] Close Joey before trying any other patch.", flush=True)
            try:
                script.unload()
            except Exception:
                pass
        if session is not None:
            try:
                session.detach()
            except Exception:
                pass
    return exit_code


if __name__ == "__main__":
    raise SystemExit(main())
