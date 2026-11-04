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
from collections import Counter
from datetime import datetime, timezone
import json
from pathlib import Path
import struct
import threading

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
               f"destroyAttempts={console.get('destroyAttempts', 0)} "
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
        other = p.get("otherFieldIndices", [])
        positions = p.get("fieldPositionWords", [])
        if len(other) == 6 and len(positions) == 11:
            def describe(slot: int) -> str:
                index = other[slot - 5]
                return "-" if not index else f"{index}(pos=0x{positions[slot]:04X})"
            print(f"[field] Side {p['side']}: slots 5..9="
                  f"[{', '.join(describe(slot) for slot in range(5, 10))}] "
                  f"slot 10={describe(10)}", flush=True)


def show_duel_diff(before: dict, after: dict, *, quiet: bool = False) -> int:
    """Report meaningful field and hand transitions, with physical slot indices."""
    changes = 0
    for prev, current in zip(before["sides"], after["sides"]):
        side = current["side"]
        for field in ("lifePoints", "handCount", "deckCount",
                      "graveyardCount", "banishedCount", "terminalFlags"):
            if prev[field] != current[field]:
                print(f"[diff] side {side} {field}: "
                      f"{prev[field]} -> {current[field]}", flush=True)
                changes += 1
        for key, offset in (("monsterIndices", 0), ("otherFieldIndices", 5)):
            before_slots = prev.get(key, [])
            after_slots = current.get(key, [])
            for index, (old, new) in enumerate(zip(before_slots, after_slots)):
                if old != new:
                    slot = offset + index
                    print(f"[diff] side {side} field slot {slot}: "
                          f"{old or '-'} -> {new or '-'}", flush=True)
                    changes += 1
        previous_indices = prev.get("monsterIndices", []) + prev.get("otherFieldIndices", [])
        current_indices = current.get("monsterIndices", []) + current.get("otherFieldIndices", [])
        for slot, (old, new) in enumerate(zip(prev.get("fieldPositionWords", []),
                                              current.get("fieldPositionWords", []))):
            if (old != new and slot < len(previous_indices) and
                    (previous_indices[slot] or current_indices[slot])):
                print(f"[diff] side {side} field slot {slot} position: "
                      f"0x{old:04X} -> 0x{new:04X}", flush=True)
                changes += 1
        if prev.get("handIndices") != current.get("handIndices"):
            print(f"[diff] side {side} handIndices: "
                  f"{prev.get('handIndices', [])} -> {current.get('handIndices', [])}",
                  flush=True)
            changes += 1
    if changes == 0 and not quiet:
        print("[diff] No changes in the captured fields.", flush=True)
    return changes


class DuelWatcher:
    """A polling observer; it never calls native mutation routines."""

    def __init__(self, api: object):
        self.api = api
        self.interval_ms = 250
        self._halt = threading.Event()
        self._thread: threading.Thread | None = None
        self._previous: dict | None = None

    def running(self) -> bool:
        return self._thread is not None and self._thread.is_alive()

    def start(self, interval_ms: int = 250) -> None:
        if self.running():
            raise ValueError("Watch is already active; use watch off")
        if not 100 <= interval_ms <= 5000:
            raise ValueError("Watch interval must be 100..5000 ms")
        self._previous = self.api.duelstate()
        self.interval_ms = interval_ms
        self._halt.clear()
        self._thread = threading.Thread(target=self._loop, daemon=True)
        self._thread.start()

    def stop(self) -> None:
        self._halt.set()
        if self._thread is not None:
            self._thread.join(timeout=2)
            self._thread = None

    def _loop(self) -> None:
        while not self._halt.wait(self.interval_ms / 1000):
            try:
                current = self.api.duelstate()
                if self._previous is not None and current != self._previous:
                    print("[watch] Observed state change:", flush=True)
                    show_duel_diff(self._previous, current, quiet=True)
                self._previous = current
            except Exception as error:
                print(f"[watch] Stopped after read error: {error}", flush=True)
                self._halt.set()
                return

def show_trace_status(status: dict) -> None:
    print(f"[trace] active={status['active']} session={status['session']} "
          f"seen={status['seen']} retained={status['retained']} "
          f"overwritten={status['overwritten']} capacity={status['capacity']}",
          flush=True)


def save_command_trace(api: object, output: Path | None = None) -> Path:
    """Stop tracing and export bounded records to an exclusive JSONL file."""
    status = api.tracestop()
    if status["session"] == 0:
        raise ValueError("No trace session captured yet; use 'trace on'")
    if output is None:
        timestamp = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S-%f")
        pid = api.status()["processId"]
        output = HERE.parent / "scratch" / "traces" / f"commands-{timestamp}-pid{pid}.jsonl"
    output.parent.mkdir(parents=True, exist_ok=True)
    counts: Counter[str] = Counter()
    exported = 0
    # Exclusive creation: never silently overwrite earlier recordings.
    with output.open("x", encoding="utf-8") as destination:
        header = {"kind": "metadata", "producer": status["producer"],
                  "session": status["session"], "capacity": status["capacity"],
                  "seen": status["seen"], "retained": status["retained"],
                  "overwritten": status["overwritten"],
                  "note": "Producer inputs only. Not proof of command execution or state mutation."}
        destination.write(json.dumps(header, ensure_ascii=False) + "\n")
        for offset in range(0, status["retained"], 256):
            batch = api.traceevents(offset, 256)
            if not batch:
                raise RuntimeError(f"Trace truncated during export at offset {offset}")
            for event in batch:
                if event.get("kind") == "command":
                    args = event["args"]
                    command_word = args[0] & 0xFFFF
                    opcode = command_word & 0x7FFF
                    event = {**event, "opcodeRaw": f"0x{command_word:04X}",
                             "opcodeMasked": f"0x{opcode:04X}",
                             "sideBit15": (command_word >> 15) & 1,
                             "argsHex": [f"0x{v:08X}" for v in args]}
                    counts[event["opcodeMasked"]] += 1
                destination.write(json.dumps(event, ensure_ascii=False) + "\n")
                exported += 1
    print(f"[trace] Saved {exported} events: {output}", flush=True)
    if status["overwritten"]:
        print(f"[trace] WARNING: {status['overwritten']} older events were "
              "overwritten by the bounded buffer.", flush=True)
    if counts:
        summary = ", ".join(f"{code}={n}" for code, n in counts.most_common(12))
        print(f"[trace] Most frequent masked opcodes: {summary}", flush=True)
    return output


def show_help(default_side: int) -> None:
    print("[dual-rules] Commands: Enter/status = counters; "
          "draw 5 [0|1]; damage <1..9999> <0|1>; "
          "destroy <0|1> <0..4>; state; diff; hand [0|1]; "
          "watch on [100..5000 ms]; watch off; "
          "trace on [16..20000]; trace mark <label>; trace status; "
          "trace off; trace dump; cancel; help; stop", flush=True)
    print(f"[dual-rules] 'draw 5' defaults to side {default_side}. "
          "Side 0 = CPU, side 1 = player in the tested local duel. "
          "Damage and destroy require an explicit target side. "
          "Monster slots are zero-based.", flush=True)


def handle_command(command: str, api: object, default_side: int,
                   state_cache: dict | None = None,
                   watcher: DuelWatcher | None = None) -> bool:
    """Return False only when the user requested shutdown."""
    raw = command.strip()
    parts = raw.lower().split()
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
    if parts[0] == "trace":
        if parts == ["trace", "status"] or parts == ["trace"]:
            show_trace_status(api.tracestatus())
        elif parts == ["trace", "off"]:
            show_trace_status(api.tracestop())
            print("[trace] Capture stopped; run 'trace dump' to save.", flush=True)
        elif parts == ["trace", "dump"]:
            save_command_trace(api)
        elif (parts == ["trace", "on"] or
              (len(parts) == 3 and parts[:2] == ["trace", "on"] and
               parts[2].isdecimal() and 16 <= int(parts[2]) <= 20000)):
            capacity = 8192 if len(parts) == 2 else int(parts[2])
            show_trace_status(api.tracestart(capacity))
        elif len(parts) >= 3 and parts[:2] == ["trace", "mark"]:
            # Preserve case and spaces; marker is bounded by the Frida agent.
            label = raw.split(None, 2)[2]
            show_trace_status(api.tracemark(label))
        else:
            print("[trace] Usage: trace on [16..20000] | trace mark <label> | "
                  "trace status | trace off | trace dump", flush=True)
        return True
    if parts[0] == "watch":
        if watcher is None:
            print("[watch] No watcher available.", flush=True)
        elif parts == ["watch", "off"]:
            watcher.stop()
            print("[watch] Stopped.", flush=True)
        elif parts == ["watch"] or parts == ["watch", "status"]:
            print(f"[watch] Active={watcher.running()} "
                  f"interval={watcher.interval_ms}ms", flush=True)
        elif (parts == ["watch", "on"] or
              (len(parts) == 3 and parts[:2] == ["watch", "on"] and
               parts[2].isdecimal() and 100 <= int(parts[2]) <= 5000)):
            interval = 250 if len(parts) == 2 else int(parts[2])
            watcher.start(interval)
            print(f"[watch] Reading duel state every {interval}ms "
                  "(non-atomic; transitions may be missed).", flush=True)
        else:
            print("[watch] Usage: watch on [100..5000] | watch off | watch status",
                  flush=True)
        return True
    if parts[0] == "hand":
        if len(parts) > 2 or (len(parts) == 2 and parts[1] not in ("0", "1")):
            print("[console] Usage: hand [0|1] (default: player side 1)", flush=True)
            return True
        side = 1 if len(parts) == 1 else int(parts[1])
        p = api.duelstate()["sides"][side]
        print(f"[hand] side {side}: count={p['handCount']} "
              f"indices={p.get('handIndices', [])} "
              f"truncated={p.get('handTruncated', False)}", flush=True)
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
    if parts[0] == "destroy":
        if len(parts) != 3 or parts[1] not in ("0", "1") or parts[2] not in (
                "0", "1", "2", "3", "4"):
            print("[console] Usage: destroy <0|1> <0..4> "
                  "(side, zero-based monster slot)", flush=True)
            return True
        side, slot = int(parts[1]), int(parts[2])
        try:
            queued = api.queuedestroy(side, slot)
        except Exception as error:
            if "Target monster slot is empty" in str(error):
                print(f"[console] Side {side} monster slot {slot} is empty; "
                      "nothing was queued.", flush=True)
                return True
            raise
        print(f"[console] Destruction request {queued['id']} pending: "
              f"side {side}, monster slot {slot}, "
              f"index {queued['expectedIndex']}. "
              "Submission does not guarantee destruction; check state/diff.",
              flush=True)
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
    watcher = None
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
        watcher = DuelWatcher(api)
        while True:
            command = input("[dual-rules] > ").strip()
            try:
                if not handle_command(command, api, args.console_side, state_cache,
                                      watcher):
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
        if watcher is not None:
            watcher.stop()
        if script is not None:
            # Preserve the bounded buffer even if the user exits via Ctrl+C.
            try:
                trace_state = script.exports_sync.tracestatus()
                if trace_state["session"] > 0 and trace_state["retained"] > 0:
                    save_command_trace(script.exports_sync)
                elif trace_state["active"]:
                    script.exports_sync.tracestop()
            except Exception as trace_error:
                print(f"[trace] Auto-save failed: {trace_error}", flush=True)
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
