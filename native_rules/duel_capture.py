"""One-file, loss-detecting stream of Joey demo duels for offline research.

Only the agent's producer hook counts masked terminal opcode 0x0004.
A live peek does NOT remove data; Python writes+fsyncs before asking the
agent to acknowledge those exact sequence numbers. Events are continuous
across the whole capture (the Frida hooks are not stopped for rotation).
"""
from __future__ import annotations

from datetime import datetime, timezone
import json
import os
from pathlib import Path
import time
from typing import Any, Callable


def enrich(event: dict[str, Any]) -> dict[str, Any]:
    """Add the same basic masked opcode fields as the interactive trace export."""
    if event.get("kind") != "command":
        return event
    args = event["args"]
    word = args[0] & 0xFFFF
    opcode = word & 0x7FFF
    result = {
        **event,
        "opcodeRaw": f"0x{word:04X}",
        "opcodeMasked": f"0x{opcode:04X}",
        "sideBit15": (word >> 15) & 1,
        "argsHex": [f"0x{v:08X}" for v in args],
    }
    if (opcode == 0xCE and str(event.get("caller", "")).lower() == "0x5abeab"
            and len(args) >= 2):
        result["ceContextLow17Hex"] = f"0x{(args[0] >> 15) & 0x1FFFF:05X}"
        result["ceResourceIdLow16"] = args[1] & 0xFFFF
        native = event.get("ceNativeState")
        if isinstance(native, dict) and isinstance(native.get("context"), int):
            context = native["context"]
            result["ceHandIndexCandidate"] = (context >> 6) & 0xFF
            result["ceFieldSlotCandidate"] = (context >> 1) & 0x1F
            result["ceFlag4000"] = bool(context & 0x4000)
            result["ceFlag8000"] = bool(context & 0x8000)
            if isinstance(native.get("source"), int):
                result["ceSourceIndexCandidate"] = native["source"] & 0xFFF
            if isinstance(native.get("stage"), int):
                result["ceStageGroup"] = (native["stage"] & 0xFE0) >> 5
    if (opcode == 0xCF and str(event.get("caller", "")).lower() == "0x57977f"
            and len(args) >= 2):
        result["cfResourceIdCandidate"] = args[1] & 0xFFFF
    return result


class LiveDuelCapture:
    """Trace file is created before game resumes and remains open until cleanup."""

    def __init__(self, api: object, pid: int, target_duels: int,
                 deck_plans: dict[str, Any], speed_value: int | None,
                 output_dir: Path | None = None,
                 rng_seed_base: int | None = None,
                 rng_seed_start_index: int = 0) -> None:
        self.api = api
        self.target_duels = target_duels
        self.written = 0
        self.terminals_written = 0
        self.seed_applied_count = 0
        self.seed_applied_indices: list[int] = []
        self.rng_seed_base = rng_seed_base
        self.rng_seed_start_index = rng_seed_start_index
        self.next_seq = 1
        self.closed = False
        self.finished = False
        self.output_dir = (output_dir or
                           Path(__file__).resolve().parent.parent / "scratch" / "traces")
        self.output_dir.mkdir(parents=True, exist_ok=True)
        now = datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S-%f")
        self.path = self.output_dir / f"duels-{now}-pid{pid}.jsonl"
        self.stream = self.path.open("x", encoding="utf-8")
        self.session = self.api.tracestatus()["session"]
        header = {
            "kind": "metadata", "formatVersion": 2, "mode": "live_duels",
            "processId": pid, "requestedDuels": target_duels,
            "speedValue": speed_value, "deckPlans": deck_plans,
            "rngSeedOverride": ({"baseSeed": rng_seed_base,
                                 "globalDuelStartIndex": rng_seed_start_index,
                                 "algorithm": "mix32-v1"}
                                if rng_seed_base is not None else None),
            "session": self.session, "startedUtc": datetime.now(timezone.utc).isoformat(),
            "note": "Masked 0x0004 counts terminal commands, not known winners. "
                    "Original command arguments and all passive probes retained."
        }
        self.stream.write(json.dumps(header, ensure_ascii=False) + "\n")
        self._sync()

    def _sync(self) -> None:
        self.stream.flush()
        os.fsync(self.stream.fileno())

    def _write_batch(self, events: list[dict[str, Any]]) -> None:
        if not events:
            return
        for event in events:
            seq = event.get("seq")
            if type(seq) is not int or seq != self.next_seq:
                raise RuntimeError(
                    f"Non-contiguous trace: expected seq {self.next_seq}, found {seq}")
            normalized = enrich(event)
            if (normalized.get("kind") == "demo_init" and
                    normalized.get("function") == "rng_seed_candidate" and
                    normalized.get("phase") == "enter" and
                    normalized.get("seedOverrideApplied") is True):
                self.seed_applied_count += 1
                self.seed_applied_indices.append(normalized.get("globalDuelIndex"))
            self.stream.write(json.dumps(normalized, ensure_ascii=False) + "\n")
            self.next_seq += 1
            self.written += 1
            if normalized.get("kind") == "command" and (
                    normalized.get("opcodeMasked") == "0x0004"):
                self.terminals_written += 1
        self._sync()

    def drain(self) -> dict[str, Any]:
        """Peek -> durable write -> acknowledge, while hooks keep recording."""
        while True:
            page = self.api.tracelivepeek(256)
            if page["session"] != self.session:
                raise RuntimeError("Frida trace session changed during streaming")
            events = page["events"]
            if not events:
                break
            self._write_batch(events)
            ack = self.api.traceliveack(
                self.session, events[0]["seq"], events[-1]["seq"])
            if ack.get("acknowledged") != len(events):
                raise RuntimeError("Frida acknowledged an unexpected number of events")
        status = self.api.tracestatus()
        if status["overwritten"]:
            raise RuntimeError(
                f"LOST {status['overwritten']} trace events; aborting collection")
        return status

    def finish(self, stopped_status: dict[str, Any], requested_complete: bool) -> dict[str, Any]:
        """Must be called after api.tracestop(), which freezes the ring buffer."""
        if self.finished:
            raise RuntimeError("Capture already finalized")
        if stopped_status["active"]:
            raise RuntimeError("Stop the agent's hooks before finalizing a capture")
        if stopped_status["session"] != self.session:
            raise RuntimeError("Trace session mismatch at finalization")
        count = stopped_status["retained"]
        for offset in range(0, count, 256):
            batch = self.api.traceevents(offset, 256)
            if len(batch) != min(256, count - offset):
                raise RuntimeError("Trace truncated while finalizing")
            self._write_batch(batch)
        if self.written != stopped_status["seen"] - stopped_status["overwritten"]:
            raise RuntimeError("Written event count disagrees with agent counters")
        if self.terminals_written != stopped_status["completedDuels"]:
            raise RuntimeError("Duels in trace disagree with the agent's terminal counter")
        if self.rng_seed_base is not None and requested_complete:
            expected = list(range(
                self.rng_seed_start_index,
                self.rng_seed_start_index + self.target_duels))
            if self.seed_applied_indices != expected:
                raise RuntimeError(
                    "Controlled RNG seed sequence missing or out of order: "
                    f"expected {expected}, got {self.seed_applied_indices}")
        if stopped_status["overwritten"]:
            raise RuntimeError("Agent discarded events before final export")
        # Existing trace acknowledgement prevents duplicate exports in launcher finally.
        self.api.traceacknowledge(self.session, stopped_status["seen"])
        summary = {
            "kind": "capture_summary", "requestedDuels": self.target_duels,
            "capturedDuels": self.terminals_written, "eventsWritten": self.written,
            "rngSeedOverridesApplied": self.seed_applied_count,
            "overwritten": stopped_status["overwritten"],
            "complete": requested_complete and
                        self.terminals_written >= self.target_duels,
            "finishedUtc": datetime.now(timezone.utc).isoformat(),
            "note": "Completed duels counted by terminal opcode 0x0004/0x8004. "
                    "Winner and card legality not decoded."
        }
        self.stream.write(json.dumps(summary, ensure_ascii=False) + "\n")
        self._sync()
        self.finished = True
        return summary

    def close(self) -> None:
        if not self.closed:
            self.stream.close()
            self.closed = True


def collect_duels(capture: LiveDuelCapture, poll_seconds: float = 0.25,
                  idle_seconds: float = 120.0,
                  should_cancel: Callable[[], bool] | None = None) -> None:
    """Poll until at least target terminal commands, reporting event-based progress."""
    last_events = -1
    last_change = time.monotonic()
    last_print = 0.0
    last_bucket = -1
    while True:
        if should_cancel is not None and should_cancel():
            raise KeyboardInterrupt("YugiLab coordinator requested cancellation")
        status = capture.drain()
        now = time.monotonic()
        duels = status["completedDuels"]
        seen = status["seen"]
        if seen != last_events:
            last_events = seen
            last_change = now
        elif now - last_change > idle_seconds:
            raise RuntimeError(
                f"No new trace events for {idle_seconds:.0f}s; aborting idle worker")
        bucket = min(20, duels * 20 // capture.target_duels)
        if bucket > last_bucket or now - last_print >= 5:
            percent = min(100, 100.0 * duels / capture.target_duels)
            print(f"[lab] {duels}/{capture.target_duels} duels "
                  f"({percent:.1f}%) | {capture.written} events saved | "
                  f"{status['retained']} buffered | lost {status['overwritten']}",
                  flush=True)
            last_print = now
            last_bucket = bucket
        if duels >= capture.target_duels:
            return
        time.sleep(poll_seconds)
