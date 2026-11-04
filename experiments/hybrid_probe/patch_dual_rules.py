"""Install Sparks and Raimei C++ callbacks together, with guarded rollback.

Requirements: the existing YugiRE pe_preflight.py and .venv-frida environment.
The original Joey EXE SHA256 is checked against the *loaded* process module.
Only the primary callback pointers of resources 342 and 1007 are modified.
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
DEFAULT_DLL = HERE / "native_rules" / "build-vs2026" / "Release" / "yugire_rules.dll"
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
        print(f"[console] batchesQueued={console['batchesQueued']} "
              f"pending={pending} last={last}", flush=True)
        if pending is not None:
            print(f"[console] duelQueueGate={gate}", flush=True)


def show_help(default_side: int) -> None:
    print("[dual-rules] Commands: Enter/status = counters; "
          "draw 5 [0|1] = request five cards; cancel = cancel pending; "
          "help = this text; stop = restore", flush=True)
    print(f"[dual-rules] 'draw 5' defaults to side {default_side}. "
          "Confirmed in the local CPU duel: side 0 = CPU, side 1 = player. "
          "Use an explicit side for other modes.", flush=True)


def handle_command(command: str, api: object, default_side: int) -> bool:
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


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pid", type=int, required=True, help="PID of joey_pc.exe")
    parser.add_argument("--dll", type=Path, default=DEFAULT_DLL,
                        help="32-bit Windows yugire_rules.dll built by Visual Studio")
    parser.add_argument("--sparks", type=damage_type, default=200,
                        help="Sparks LP damage, 1..9999 (default: 200)")
    parser.add_argument("--raimei", type=damage_type, default=300,
                        help="Raimei LP damage, 1..9999 (default: 300)")
    parser.add_argument("--sparks-mode", choices=tuple(MODES), default="damage",
                        help="Sparks effect: damage, draw or damage-draw (default: damage)")
    parser.add_argument("--raimei-mode", choices=tuple(MODES), default="damage",
                        help="Raimei effect: damage, draw or damage-draw (default: damage)")
    parser.add_argument("--console-side", type=int, choices=(0, 1), default=1,
                        help="Default side for 'draw 5' (1 = player, 0 = CPU in tested local duel)")
    args = parser.parse_args()
    dll_path = args.dll.resolve()

    try:
        check_dll(dll_path)
    except (OSError, ValueError) as exc:
        parser.error(str(exc))
    try:
        import frida
    except ImportError:
        parser.error("Frida not installed; use experiments/hybrid_probe/.venv-frida")

    session = None
    script = None
    exit_code = 0
    try:
        print(f"[dual-rules] Attaching to Joey PID {args.pid}", flush=True)
        session = frida.attach(args.pid)
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
        while True:
            command = input("[dual-rules] > ").strip()
            try:
                if not handle_command(command, api, args.console_side):
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
