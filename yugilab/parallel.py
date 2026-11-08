"""Run multiple supervised YugiLab processes and verify their capture footers.

Workers are Python subprocesses, each spawning exactly one Joey via Frida.
Cancellation uses a file observed by collect_duels(), so children can flush
their JSONL and kill only the PID they spawned. Never kill an arbitrary PID.
"""
from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import time


PID_PATTERN = re.compile(r"Spawned suspended Joey PID (\d+)")
VERIFIED_EXIT_PATTERN = re.compile(
    r"\[yugilab\] Owned Joey PID (\d+) closed "
    r"\(verified by Windows process handle\)\."
)
PROGRESS_PATTERN = re.compile(r"\[lab\] (\d+)/(\d+) duels")


@dataclass
class Worker:
    number: int
    requested: int
    process: subprocess.Popen
    log: Path


def distribute_duels(total: int, workers: int) -> list[int]:
    if total < 1 or workers < 1 or workers > total:
        raise ValueError("Require 1 <= workers <= total completed duels")
    base, extra = divmod(total, workers)
    return [base + (1 if index < extra else 0)
            for index in range(workers)]


def _last_json_line(path: Path) -> dict | None:
    """Read only a small tail, not the full ~100-MB event stream."""
    with path.open("rb") as file:
        file.seek(0, os.SEEK_END)
        size = file.tell()
        file.seek(max(0, size - 65536))
        lines = file.read().splitlines()
    for raw in reversed(lines):
        if raw.strip():
            try:
                result = json.loads(raw)
                return result if isinstance(result, dict) else None
            except (ValueError, UnicodeDecodeError):
                return None
    return None


def _worker_capture(worker: Worker, captures: Path) -> dict:
    log_content = worker.log.read_text(encoding="utf-8", errors="replace")
    matches = PID_PATTERN.findall(log_content)
    pid = int(matches[-1]) if matches else None
    verified_exits = {int(value) for value in VERIFIED_EXIT_PATTERN.findall(log_content)}
    exit_verified = pid is not None and pid in verified_exits
    files = (list(captures.glob(f"duels-*-pid{pid}.jsonl"))
             if pid is not None else [])
    record = {
        "worker": worker.number,
        "requestedDuels": worker.requested,
        "processId": pid,
        "exitCode": worker.process.returncode,
        "processTerminationVerified": exit_verified,
        "log": str(worker.log),
        "capture": str(files[0]) if len(files) == 1 else None,
        "complete": False,
        "capturedDuels": 0,
        "eventsWritten": 0,
    }
    if len(files) != 1:
        record["error"] = (f"Expected exactly one capture for PID {pid}, "
                           f"found {len(files)}")
        return record
    first = None
    with files[0].open(encoding="utf-8") as file:
        line = file.readline()
        if line:
            try:
                first = json.loads(line)
            except ValueError:
                pass
    last = _last_json_line(files[0])
    if not isinstance(first, dict) or first.get("kind") != "metadata":
        record["error"] = "Missing JSONL metadata"
    elif first.get("processId") != pid:
        record["error"] = "Capture metadata PID does not match spawned PID"
    elif first.get("requestedDuels") != worker.requested:
        record["error"] = "Worker target does not match capture metadata"
    elif not isinstance(last, dict) or last.get("kind") != "capture_summary":
        record["error"] = "Missing capture_summary footer (partial capture)"
    else:
        record["capturedDuels"] = last.get("capturedDuels", 0)
        record["eventsWritten"] = last.get("eventsWritten", 0)
        record["complete"] = (
            worker.process.returncode == 0 and
            exit_verified and
            last.get("complete") is True and
            last.get("overwritten") == 0 and
            last.get("capturedDuels") == worker.requested and
            isinstance(last.get("eventsWritten"), int))
        if not record["complete"]:
            record["error"] = ("Joey process exit not independently verified"
                               if not exit_verified else
                               "Worker failed or capture footer is incomplete")
    return record


def _last_progress(log: Path) -> int:
    try:
        content = log.read_text(encoding="utf-8", errors="replace")
    except OSError:
        return 0
    values = PROGRESS_PATTERN.findall(content)
    return int(values[-1][0]) if values else 0


def _stop_workers(cancel_file: Path) -> None:
    cancel_file.write_text("stop\n", encoding="utf-8")


def run_parallel(args, output_root: Path) -> int:
    """Spawn independent Python workers, and count only verified footers."""
    allocations = distribute_duels(args.duels, args.workers)
    start_monotonic = time.monotonic()
    output_root.mkdir(parents=True, exist_ok=True)
    run_dir = Path(tempfile.mkdtemp(prefix="parallel-", dir=output_root))
    captures = run_dir / "captures"
    captures.mkdir()
    cancel_file = run_dir / "STOP"
    metadata = {
        "mode": "yugilab_parallel", "requestedDuels": args.duels,
        "workersRequested": args.workers, "allocations": allocations,
        "startedUtc": datetime.now(timezone.utc).isoformat(),
        "speedValue": args.speed_value,
        "traceAi": args.trace_ai,
        "complete": False,
    }
    workers: list[Worker] = []
    interrupted = False
    failed = False
    error: str | None = None

    # An extra second between workers reduces risk from an old time-based
    # RNG seed. It DOES NOT establish that original game seeds are independent.
    print(f"[yugilab] Parallel run: {args.duels} duels / {args.workers} workers "
          f"-> {allocations}; output: {run_dir}", flush=True)
    try:
        for index, target in enumerate(allocations, 1):
            # Stop launching more Joeys if a prior child failed at startup.
            broken = next((w for w in workers
                           if w.process.poll() not in (None, 0)), None)
            if broken is not None:
                failed = True
                error = f"Worker {broken.number} failed during startup"
                _stop_workers(cancel_file)
                break
            if cancel_file.exists():
                interrupted = True
                break
            log = run_dir / f"worker-{index:02d}.log"
            argv = [
                sys.executable, "-m", "yugilab",
                "--exe", str(args.exe),
                "--duels", str(target),
                "--workers", "1",
                "--speed-value", str(args.speed_value),
                "--output-dir", str(captures),
                "--cancel-file", str(cancel_file),
                "--allow-multi-instance",
            ]
            if args.deck_0 is not None:
                argv += ["--deck-0", str(args.deck_0)]
            if args.deck_1 is not None:
                argv += ["--deck-1", str(args.deck_1)]
            if args.trace_ai:
                argv.append("--trace-ai")
            kwargs = {}
            if os.name == "nt":
                kwargs["creationflags"] = subprocess.CREATE_NEW_PROCESS_GROUP
            else:
                kwargs["start_new_session"] = True
            with log.open("w", encoding="utf-8") as output:
                process = subprocess.Popen(
                    argv, stdout=output, stderr=subprocess.STDOUT,
                    cwd=str(Path(__file__).resolve().parent.parent),
                    **kwargs)
            workers.append(Worker(index, target, process, log))
            print(f"[yugilab] Worker {index}/{len(allocations)}: "
                  f"Python PID {process.pid}; {target} duels", flush=True)
            if index < len(allocations) and args.stagger_seconds > 0:
                time.sleep(args.stagger_seconds)

        last_status = 0.0
        while any(worker.process.poll() is None for worker in workers):
            if not failed:
                for worker in workers:
                    rc = worker.process.poll()
                    if rc is not None and rc != 0:
                        failed = True
                        error = f"Worker {worker.number} exited with code {rc}"
                        _stop_workers(cancel_file)
                        print(f"[yugilab] {error}; requesting graceful stop.",
                              flush=True)
                        break
            now = time.monotonic()
            if now - last_status >= 5:
                done = sum(min(w.requested, _last_progress(w.log)) for w in workers)
                print(f"[yugilab] Combined progress: {done}/{args.duels} "
                      f"duels; {sum(w.process.poll() is None for w in workers)} "
                      "workers still running", flush=True)
                last_status = now
            time.sleep(0.5)

    except KeyboardInterrupt:
        interrupted = True
        error = "Interrupted by operator"
        _stop_workers(cancel_file)
        print("\n[yugilab] Cancellation requested; allowing workers to "
              "flush traces and close their Joey PIDs.", flush=True)
    except Exception as exc:
        failed = True
        error = f"{type(exc).__name__}: {exc}"
        _stop_workers(cancel_file)
        print(f"[yugilab] Coordinator error: {error}", flush=True)
    finally:
        # NEVER terminate a Python worker before it has had a chance to
        # detach Frida and kill its own Joey. This also applies to Ctrl+C.
        # A hung worker must remain visible as a failure, not silently orphan
        # its Joey by force-killing the worker.
        for worker in workers:
            while worker.process.poll() is None:
                try:
                    worker.process.wait(timeout=1.0)
                except subprocess.TimeoutExpired:
                    continue
                except KeyboardInterrupt:
                    interrupted = True
                    _stop_workers(cancel_file)
            print(f"[yugilab] Worker {worker.number}: exit "
                  f"{worker.process.returncode}", flush=True)

        records = [_worker_capture(worker, captures) for worker in workers]
        succeeded = (not interrupted and not failed and
                     len(records) == args.workers and
                     all(item["complete"] for item in records) and
                     sum(item["capturedDuels"] for item in records) == args.duels)
        metadata.update({
            "complete": succeeded,
            "status": "complete" if succeeded else
                      ("interrupted" if interrupted else "failed"),
            "finishedUtc": datetime.now(timezone.utc).isoformat(),
            "elapsedSeconds": round(time.monotonic() - start_monotonic, 3),
            "capturedDuels": sum(item["capturedDuels"] for item in records),
            "eventsWritten": sum(item["eventsWritten"] for item in records),
            "error": error,
            "workers": records,
        })
        manifest = run_dir / "manifest.json"
        manifest.write_text(json.dumps(metadata, indent=2, ensure_ascii=False) + "\n",
                            encoding="utf-8")
        print(f"[yugilab] {metadata['capturedDuels']}/{args.duels} "
              f"verified duels. Complete={succeeded}; manifest: {manifest}",
              flush=True)
    return 0 if metadata["complete"] else (130 if interrupted else 2)
