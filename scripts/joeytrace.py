#!/usr/bin/env python3
"""JoeyTrace v1 — read-only timeline of Joey the Passion duel state.

Windows, Python 3.10+; standard library only. No DLL injection, no patches.
Run from YugiRE repository root:
    py -3 scripts/joeytrace.py capture
    py -3 scripts/joeytrace.py capture --raw-blocks
    py -3 scripts/joeytrace.py import-replay --session scratch/traces/<session> --file replay.ydr
    py -3 scripts/joeytrace.py demo --output scratch/traces/joeytrace-demo
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import queue
import shutil
import struct
import sys
import threading
import time

from joeytrace_core import (
    BLOCK_SIZE, EXPECTED_EXE_SHA256, MEMORY_RVA, TraceSession,
    parse_state, plausible_duel, utc_now,
)

from joeytrace_replay import choose_replay, list_replays
from joeytrace_effects import generate_effect_windows

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_TRACES_ROOT = ROOT / "scratch" / "traces"


class WinProcessProbe:
    """Windows-only reader; no process writes, suspension or code injection."""

    PROCESS_VM_READ = 0x0010
    PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
    PROCESS_SYNCHRONIZE = 0x00100000
    TH32CS_SNAPPROCESS = 0x00000002
    TH32CS_SNAPMODULE = 0x00000008
    TH32CS_SNAPMODULE32 = 0x00000010
    WAIT_OBJECT_0 = 0x00000000
    WAIT_TIMEOUT = 0x00000102

    def __init__(self, pid: int | None = None):
        if sys.platform != "win32":
            raise RuntimeError("Captura em tempo real requer Windows. Use 'demo' para testar offline.")
        import ctypes
        from ctypes import wintypes as w
        self.ctypes = ctypes
        self.w = w
        self.kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        self.user32 = ctypes.WinDLL("user32", use_last_error=True)

        class PROCESSENTRY32W(ctypes.Structure):
            _fields_ = [
                ("dwSize", w.DWORD), ("cntUsage", w.DWORD),
                ("th32ProcessID", w.DWORD), ("th32DefaultHeapID", ctypes.c_void_p),
                ("th32ModuleID", w.DWORD), ("cntThreads", w.DWORD),
                ("th32ParentProcessID", w.DWORD), ("pcPriClassBase", w.LONG),
                ("dwFlags", w.DWORD), ("szExeFile", w.WCHAR * 260),
            ]

        class MODULEENTRY32W(ctypes.Structure):
            _fields_ = [
                ("dwSize", w.DWORD), ("th32ModuleID", w.DWORD),
                ("th32ProcessID", w.DWORD), ("GlblcntUsage", w.DWORD),
                ("ProccntUsage", w.DWORD), ("modBaseAddr", ctypes.c_void_p),
                ("modBaseSize", w.DWORD), ("hModule", ctypes.c_void_p),
                ("szModule", w.WCHAR * 256), ("szExePath", w.WCHAR * 260),
            ]

        self.PROCESSENTRY32W = PROCESSENTRY32W
        self.MODULEENTRY32W = MODULEENTRY32W
        k = self.kernel32
        k.CreateToolhelp32Snapshot.argtypes = [w.DWORD, w.DWORD]
        k.CreateToolhelp32Snapshot.restype = w.HANDLE
        k.Process32FirstW.argtypes = [w.HANDLE, ctypes.POINTER(PROCESSENTRY32W)]
        k.Process32FirstW.restype = w.BOOL
        k.Process32NextW.argtypes = [w.HANDLE, ctypes.POINTER(PROCESSENTRY32W)]
        k.Process32NextW.restype = w.BOOL
        k.Module32FirstW.argtypes = [w.HANDLE, ctypes.POINTER(MODULEENTRY32W)]
        k.Module32FirstW.restype = w.BOOL
        k.Module32NextW.argtypes = [w.HANDLE, ctypes.POINTER(MODULEENTRY32W)]
        k.Module32NextW.restype = w.BOOL
        k.OpenProcess.argtypes = [w.DWORD, w.BOOL, w.DWORD]
        k.OpenProcess.restype = w.HANDLE
        k.CloseHandle.argtypes = [w.HANDLE]
        k.CloseHandle.restype = w.BOOL
        k.ReadProcessMemory.argtypes = [w.HANDLE, ctypes.c_void_p, ctypes.c_void_p,
                                        ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
        k.ReadProcessMemory.restype = w.BOOL
        k.WaitForSingleObject.argtypes = [w.HANDLE, w.DWORD]
        k.WaitForSingleObject.restype = w.DWORD
        self.user32.GetAsyncKeyState.argtypes = [ctypes.c_int]
        self.user32.GetAsyncKeyState.restype = ctypes.c_short
        self.requested_pid = pid
        self.pid: int | None = None
        self.handle = None
        self.image_base = 0
        self.exe_path: Path | None = None
        self.exe_sha256 = ""

    @property
    def invalid_handle(self) -> int:
        return self.ctypes.c_void_p(-1).value

    def _process_ids(self) -> list[int]:
        k = self.kernel32
        handle = k.CreateToolhelp32Snapshot(self.TH32CS_SNAPPROCESS, 0)
        if handle == self.invalid_handle:
            raise self.ctypes.WinError(self.ctypes.get_last_error())
        try:
            p = self.PROCESSENTRY32W()
            p.dwSize = self.ctypes.sizeof(p)
            if not k.Process32FirstW(handle, self.ctypes.byref(p)):
                raise self.ctypes.WinError(self.ctypes.get_last_error())
            result = []
            while True:
                if p.szExeFile.lower() == "joey_pc.exe":
                    result.append(p.th32ProcessID)
                if not k.Process32NextW(handle, self.ctypes.byref(p)):
                    break
            return result
        finally:
            k.CloseHandle(handle)

    def find_pid(self) -> int | None:
        ids = self._process_ids()
        if self.requested_pid:
            return self.requested_pid if self.requested_pid in ids else None
        if len(ids) > 1:
            raise RuntimeError(f"Multiplos joey_pc.exe encontrados: {ids}. Selecione --pid.")
        return ids[0] if ids else None

    def _main_module(self, pid: int) -> tuple[int, Path]:
        k = self.kernel32
        snapshot = k.CreateToolhelp32Snapshot(self.TH32CS_SNAPMODULE | self.TH32CS_SNAPMODULE32, pid)
        if snapshot == self.invalid_handle:
            raise self.ctypes.WinError(self.ctypes.get_last_error())
        try:
            module = self.MODULEENTRY32W()
            module.dwSize = self.ctypes.sizeof(module)
            if not k.Module32FirstW(snapshot, self.ctypes.byref(module)):
                raise self.ctypes.WinError(self.ctypes.get_last_error())
            while True:
                if module.szModule.lower() == "joey_pc.exe":
                    return int(module.modBaseAddr), Path(module.szExePath)
                if not k.Module32NextW(snapshot, self.ctypes.byref(module)):
                    break
            raise RuntimeError("Modulo principal joey_pc.exe nao encontrado")
        finally:
            k.CloseHandle(snapshot)

    def attach(self, pid: int) -> None:
        if self.handle:
            raise RuntimeError("Ja anexado")
        image_base, exe_path = self._main_module(pid)
        # Strict identity check prevents interpreting arbitrary builds with known offsets.
        sha256 = _sha256_file(exe_path)
        if sha256 != EXPECTED_EXE_SHA256:
            raise RuntimeError(
                f"SHA-256 desconhecido: {sha256}. Esperado: {EXPECTED_EXE_SHA256}. "
                "Nao aplique enderecos deste coletor a outro executavel (incluindo EXE com patch)."
            )
        handle = self.kernel32.OpenProcess(self.PROCESS_VM_READ | self.PROCESS_QUERY_LIMITED_INFORMATION
                                           | self.PROCESS_SYNCHRONIZE,
                                           False, pid)
        if not handle:
            raise self.ctypes.WinError(self.ctypes.get_last_error())
        self.pid = pid
        self.handle = handle
        self.image_base = image_base
        self.exe_path = exe_path
        self.exe_sha256 = sha256

    def alive(self) -> bool:
        if not self.handle:
            return False
        result = self.kernel32.WaitForSingleObject(self.handle, 0)
        if result == self.WAIT_TIMEOUT:
            return True
        if result == self.WAIT_OBJECT_0:
            return False
        raise self.ctypes.WinError(self.ctypes.get_last_error())

    def read(self) -> bytes:
        if not self.handle:
            raise RuntimeError("Processo nao anexado")
        buffer = self.ctypes.create_string_buffer(BLOCK_SIZE)
        count = self.ctypes.c_size_t()
        ok = self.kernel32.ReadProcessMemory(self.handle,
             self.ctypes.c_void_p(self.image_base + MEMORY_RVA), buffer, BLOCK_SIZE,
             self.ctypes.byref(count))
        if not ok or count.value != BLOCK_SIZE:
            raise self.ctypes.WinError(self.ctypes.get_last_error())
        return buffer.raw

    def hotkey_pressed(self, hotkey: str) -> bool:
        # GetAsyncKeyState checks global state. F9 conflicts with the Windows
        # console command-history prompt, so use Ctrl+Shift+J by default.
        keys = (0x11, 0x10, 0x4A) if hotkey == "ctrl+shift+j" else (0x78,)
        return all(bool(self.user32.GetAsyncKeyState(vk) & 0x8000) for vk in keys)

    def f9_pressed(self) -> bool:
        # Backwards-compatible helper for experiments selecting --hotkey f9.
        return self.hotkey_pressed("f9")

    def close(self) -> None:
        if self.handle:
            self.kernel32.CloseHandle(self.handle)
            self.handle = None


def _sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def parse_console_command(raw: str) -> tuple[str, str] | None:
    """Free text annotates the last hotkey marker; explicit 'mark' creates one."""
    text = raw.strip()
    if not text:
        return None
    command, _, rest = text.partition(" ")
    key = command.lower()
    if key in ("stop", "quit", "exit") and not rest.strip():
        return ("stop", "")
    if key in ("mark", "marcar"):
        return ("mark", rest.strip())
    if key in ("note", "anotar"):
        return ("annotate", rest.strip())
    if key in ("help", "ajuda", "?") and not rest.strip():
        return ("help", "")
    return ("annotate", text)


def console_commands(q: queue.SimpleQueue) -> None:
    print("[joeytrace] Texto livre + Enter anota o ultimo marcador. "
          "'mark descricao' cria outro; 'stop' encerra. "
          "Atalho padrao no jogo: Ctrl+Shift+J.", flush=True)
    while True:
        try:
            cmd = input()
        except (EOFError, OSError):
            return
        parsed = parse_console_command(cmd)
        if parsed is None:
            continue
        kind, text = parsed
        if kind == "help":
            print("[joeytrace] Texto livre anota o ultimo marcador; "
                  "'mark texto' cria outro; 'stop' encerra.", flush=True)
            continue
        q.put(parsed)
        if kind == "stop":
            return


def make_session_folder(root: Path, pid: int | str) -> Path:
    stamp = time.strftime("%Y%m%d-%H%M%S", time.gmtime())
    stem = f"duel-{stamp}-pid{pid}"
    result = root / stem
    counter = 1
    while result.exists():
        result = root / f"{stem}-{counter}"
        counter += 1
    return result


def run_capture(args: argparse.Namespace) -> int:
    if sys.platform != "win32":
        print("Captura ao vivo requer Windows. Use 'demo' ou 'testes' em outro SO.", file=sys.stderr)
        return 2
    probe = WinProcessProbe(args.pid)
    root = args.output.resolve()
    print("[joeytrace] Procurando joey_pc.exe (Ctrl+C cancela)...", flush=True)
    try:
        while True:
            pid = probe.find_pid()
            if pid is not None:
                probe.attach(pid)
                break
            if args.no_wait:
                raise RuntimeError("Nenhum joey_pc.exe em execucao")
            time.sleep(1)
        print(f"[joeytrace] Anexado PID {probe.pid}; EXE validado: {probe.exe_sha256[:12]}...")
        session_dir = make_session_folder(root, probe.pid)
        session = TraceSession(session_dir, {
            "process_id": probe.pid, "exe_sha256": probe.exe_sha256,
            "image_base_hex": f"0x{probe.image_base:08X}",
            "exe_path_local": str(probe.exe_path),
            "poll_ms_requested": args.poll_ms,
            "unknown_only_enabled": args.track_unknown,
            "hand_cards_enabled": args.hand_cards or args.card_events,
            "card_events_enabled": args.card_events,
            "raw_blocks_enabled": args.raw_blocks,
            "marker_hotkey": None if args.no_hotkey else args.hotkey,
            "duel_start_detection": "heuristic_two_consecutive_plausible_snapshots",
        }, track_unknown=args.track_unknown, raw_blocks=args.raw_blocks,
           raw_sample_limit=args.raw_sample_limit, capture_hand_cards=args.hand_cards,
           capture_card_events=args.card_events)
        print(f"[joeytrace] Gravando em {session_dir}")
        print("[joeytrace] Aguardando estado plausivel de duelo (LP>0 nos dois lados)...")
        queue_ = queue.SimpleQueue()
        if sys.stdin.isatty():
            threading.Thread(target=console_commands, args=(queue_,), daemon=True).start()
        started = time.monotonic()
        next_tick = started
        armed = False
        consecutive_valid = 0
        last_key = False
        last_hotkey_ms = -1000000
        read_failures = 0
        outcome = "stopped_by_operator"
        reason = ""
        last_changes_printed = 0
        last_segment_printed = 0
        try:
            while True:
                now = time.monotonic()
                elapsed_ms = max(0, int((now - started) * 1000))
                if args.duration and now - started >= args.duration:
                    outcome, reason = "duration_reached", "Limite de duracao atingido"
                    break
                if not probe.alive():
                    outcome, reason = "process_exited", "O joey_pc.exe terminou"
                    break
                try:
                    raw = probe.read()
                    state = parse_state(raw)
                    read_failures = 0
                except (OSError, ValueError, RuntimeError) as exc:
                    read_failures += 1
                    if armed:
                        session.record_read_error(str(exc), elapsed_ms)
                    if read_failures >= 20:
                        outcome, reason = "read_failed", f"20 leituras consecutivas falharam: {exc}"
                        break
                    raw, state = None, None
                if raw is not None:
                    if not armed:
                        consecutive_valid = consecutive_valid + 1 if plausible_duel(state) else 0
                        if consecutive_valid >= 2:
                            armed = True
                            print("[joeytrace] Estado plausivel de duelo detectado; iniciando amostras.", flush=True)
                    if armed:
                        session.sample(raw, elapsed_ms)
                        if session.segment_id != last_segment_printed:
                            last_segment_printed = session.segment_id
                            print(f"[joeytrace] POSSIVEL REINICIO DE DUELO: "
                                  f"segmento {session.segment_id} em {elapsed_ms} ms. "
                                  "Heuristica; nenhuma amostra foi descartada.", flush=True)
                        if session.change_batches != last_changes_printed:
                            last_changes_printed = session.change_batches
                            if args.verbose:
                                print(f"[joeytrace] t={elapsed_ms}ms mudanca #{last_changes_printed}")
                pressed = probe.hotkey_pressed(args.hotkey) if not args.no_hotkey else False
                if armed and pressed and not last_key and elapsed_ms - last_hotkey_ms >= 800:
                    session.marker(elapsed_ms, source=args.hotkey.upper())
                    last_hotkey_ms = elapsed_ms
                    print(f"[joeytrace] Marcador #{session.marker_count} "
                          f"({args.hotkey}) em {elapsed_ms}ms", flush=True)
                last_key = pressed
                while True:
                    try:
                        cmd, content = queue_.get_nowait()
                    except queue.Empty:
                        break
                    if cmd == "mark":
                        if armed:
                            session.marker(elapsed_ms, content, "console")
                            print(f"[joeytrace] Marcador #{session.marker_count}: {content}", flush=True)
                        else:
                            print("[joeytrace] Ainda aguardando inicio de duelo; marcador ignorado")
                    elif cmd == "annotate":
                        if not content:
                            print("[joeytrace] Descricao vazia; nada foi gravado.", flush=True)
                        elif not armed:
                            print("[joeytrace] Duelo nao detectado; anotacao nao gravada.", flush=True)
                        elif session.marker_count:
                            session.annotate_marker(session.marker_count, content, source="console")
                            print(f"[joeytrace] Descricao anexada ao marcador "
                                  f"#{session.marker_count}: {content}", flush=True)
                        else:
                            session.marker(elapsed_ms, content, "console")
                            print(f"[joeytrace] Marcador #{session.marker_count} "
                                  f"criado com descricao: {content}", flush=True)
                    elif cmd == "stop":
                        outcome, reason = "stopped_by_operator", "Comando stop recebido"
                        break
                if reason:
                    break
                next_tick += args.poll_ms / 1000
                sleep_for = next_tick - time.monotonic()
                if sleep_for > 0:
                    time.sleep(sleep_for)
                else:
                    next_tick = time.monotonic()
        except KeyboardInterrupt:
            outcome, reason = "interrupted", "Ctrl+C"
        finally:
            report = session.finish(outcome, int((time.monotonic() - started) * 1000), reason)
            if session.marker_count:
                try:
                    path = generate_effect_windows(session_dir)
                    print(f"[joeytrace] Observacoes por marcador: {path}", flush=True)
                except (ValueError, OSError) as exc:
                    print(f"[joeytrace] Nao foi possivel gerar effect-windows: {exc}", flush=True)
            print(f"[joeytrace] Encerramento: {outcome}; motivo: {reason or 'nao informado'}")
            print(f"\n[joeytrace] {session.change_batches} lotes de mudanca; {session.marker_count} marcadores.")
            print(f"[joeytrace] Relatorio: {report}")
            print("[joeytrace] Para anexar replay depois: py scripts/joeytrace.py import-replay --session \"" + str(session_dir) + "\"")
        # 'stop' terminates the reader thread; Ctrl+C may leave input() blocked.
        if outcome == "stopped_by_operator" and args.prompt_replay and sys.stdin.isatty():
            try:
                print("[joeytrace] Importacao opcional do replay desta sessao:")
                run_import_replay(argparse.Namespace(session=session_dir, file=None))
            except (ValueError, OSError, FileExistsError) as exc:
                print(f"[joeytrace] Replay nao importado: {exc}")
        return 0 if outcome not in ("read_failed",) else 1
    except (OSError, RuntimeError, ValueError) as exc:
        print(f"[joeytrace] ERRO: {exc}", file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        print("\n[joeytrace] Cancelado antes de iniciar sessao")
        return 130
    finally:
        probe.close()


def run_import_replay(args: argparse.Namespace) -> int:
    from inspect_joey_replay import read_file
    import csv
    folder = args.session.resolve()
    manifest_path = folder / "manifest.json"
    if not manifest_path.is_file():
        raise ValueError(f"Sessao invalida, manifest.json ausente: {folder}")
    file = args.file
    if file is None:
        file = choose_replay()
        if file is None:
            print("[joeytrace] Importacao cancelada; nenhum arquivo foi alterado.")
            return 0
    if not file.is_file():
        raise ValueError(f"Replay nao encontrado: {file}")
    print(f"[joeytrace] Selecionado: {file}")
    records, summary = read_file(file)
    raw_file = folder / "replay.ydr"
    if raw_file.exists():
        raise FileExistsError(f"Nao sobrescrevo o replay da sessao: {raw_file}")
    shutil.copy2(file, raw_file)
    summary["header"]["save_identifier_hex"] = "REDACTED"
    (folder / "replay-summary.json").write_text(
        json.dumps(summary, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    with (folder / "replay-commands.csv").open("x", encoding="utf-8", newline="") as f:
        if records:
            wr = csv.DictWriter(f, fieldnames=list(records[0]))
            wr.writeheader()
            wr.writerows(records)
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    manifest["replay_imported_utc"] = utc_now()
    manifest["replay_sha256"] = _sha256_file(raw_file)
    manifest["replay_records"] = len(records)
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    with (folder / "report.md").open("a", encoding="utf-8") as f:
        f.write(f"\n## Replay anexado\n\n- Arquivo local: `replay.ydr`\n"
                f"- Registros: {len(records)}; hash SHA-256: `{manifest['replay_sha256']}`\n"
                "- `replay-commands.csv` e `replay-summary.json` disponiveis para analise.\n"
                "- **Nao** foi comprovado alinhamento temporal entre o replay e as amostras de RAM.\n")
    print(f"[joeytrace] Replay anexado: {raw_file} ({len(records)} registros)")
    return 0


def run_annotate(args: argparse.Namespace) -> int:
    folder = args.session.resolve()
    if not (folder / "manifest.json").is_file():
        raise ValueError("Sessao invalida: manifest.json ausente")
    markers_path = folder / "markers.jsonl"
    if not markers_path.is_file():
        raise ValueError("Sessao invalida: markers.jsonl ausente")
    markers = [json.loads(line) for line in markers_path.read_text(encoding="utf-8").splitlines() if line]
    if not any(m.get("marker_index") == args.marker for m in markers):
        raise ValueError(f"Marcador #{args.marker} nao existe nesta sessao")
    annotations_path = folder / "marker-annotations.jsonl"
    with annotations_path.open("a", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps({"marker_index": args.marker, "annotated_utc": utc_now(),
                            "note": args.text}, ensure_ascii=False) + "\n")
    generate_effect_windows(folder)
    print(f"[joeytrace] Anotacao salva para marcador #{args.marker}")
    return 0


def run_demo(args: argparse.Namespace) -> int:
    """Synthetic end-to-end smoke test, not a real Joey capture."""
    folder = args.output.resolve()
    s = TraceSession(folder, {"synthetic_demo": True, "exe_sha256": "DEMO_NOT_REAL",
                              "poll_ms_requested": 100}, raw_blocks=True, track_unknown=True)
    try:
        blob = bytearray(BLOCK_SIZE)
        for side in range(2):
            struct.pack_into("<H", blob, side * 0xD44, 8000)
            blob[side * 0xD44 + 4] = 5
        s.sample(bytes(blob), 0, "2026-01-01T00:00:00Z")
        blob[0xD44 + 4] = 4
        struct.pack_into("<I", blob, 0xD44 + 0x10, 550)
        struct.pack_into("<H", blob, 0xD44 + 0x10 + 6, 0x0202)
        s.sample(bytes(blob), 100, "2026-01-01T00:00:00.100Z")
        s.marker(105, "Exemplo de invocacao", "demo")
        struct.pack_into("<H", blob, 0, 6100)
        s.sample(bytes(blob), 200, "2026-01-01T00:00:00.200Z")
    finally:
        print(f"[joeytrace] DEMO SINTETICA: {s.finish('demo_complete', 200)}")
    return 0


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(description=__doc__)
    sub = p.add_subparsers(dest="command", required=True)
    capture = sub.add_parser("capture", help="Capturar duel ao vivo no Windows")
    capture.add_argument("--pid", type=int, help="Selecionar PID se houver varias instancias")
    capture.add_argument("--output", type=Path, default=DEFAULT_TRACES_ROOT,
                         help="Pasta raiz das sessoes (default: scratch/traces)")
    capture.add_argument("--poll-ms", type=int, default=100, help="Intervalo entre leituras em ms (padrao: 100)")
    capture.add_argument("--no-wait", action="store_true", help="Falhar se o jogo nao estiver aberto")
    capture.add_argument("--no-hotkey", action="store_true", help="Desabilitar marcador global")
    capture.add_argument("--hotkey", choices=["ctrl+shift+j", "f9"], default="ctrl+shift+j",
                         help="Atalho global (padrao ctrl+shift+j; F9 conflita com historico do console)")
    capture.add_argument("--track-unknown", action="store_true", help="Gravar mudancas apenas em bytes desconhecidos (pode gerar muito log)")
    capture.add_argument("--raw-blocks", action="store_true", help="Persistir blocos completos de RAM localmente (dados potencialmente sensiveis)")
    capture.add_argument("--hand-cards", action="store_true",
                         help="Registrar indices/words das cartas das duas maos; dados ocultos/sensiveis")
    capture.add_argument("--card-events", action="store_true",
                         help="Observacao geral: maos, deck count, 11 slots do campo, associacoes e bytes locais")
    capture.add_argument("--raw-sample-limit", type=int, default=32,
                         help="Numero maximo de offsets de bytes incluidos em cada evento")
    capture.add_argument("--duration", type=float, default=0, help="Parar automaticamente apos N segundos")
    capture.add_argument("--verbose", action="store_true", help="Mostrar mudancas no terminal")
    capture.add_argument("--no-replay-prompt", action="store_false", dest="prompt_replay",
                         help="Nao oferecer anexar replay ao encerrar com stop")
    replay = sub.add_parser("import-replay", help="Anexar YDR salvo a sessao existente")
    replay.add_argument("--session", type=Path, required=True)
    replay.add_argument("--file", type=Path,
                        help="Arquivo YDR; se omitido, sugerir o mais recente via Registro")
    effect = sub.add_parser("effect-windows",
                            help="Agrupar alteracoes por proximidade temporal aos marcadores (nao prova causalidade)")
    effect.add_argument("--session", type=Path, required=True)
    effect.add_argument("--before-ms", type=int, default=15000)
    effect.add_argument("--after-ms", type=int, default=20000)
    effect.add_argument("--max-events", type=int, default=250)
    discovery = sub.add_parser("list-replays", help="Mostrar CommonDir nas visoes do Registro e replays encontrados")
    discovery.add_argument("--limit", type=int, default=10, help="Maximo de resultados (padrao 10)")
    annotate = sub.add_parser("annotate", help="Anotar marcador F9 apos o duelo")
    annotate.add_argument("--session", type=Path, required=True)
    annotate.add_argument("--marker", type=int, required=True)
    annotate.add_argument("--text", required=True)
    demo = sub.add_parser("demo", help="Gerar sessao simulada para testar o pipeline")
    demo.add_argument("--output", type=Path, required=True)
    args = p.parse_args(argv)
    if args.command == "capture":
        if not 20 <= args.poll_ms <= 5000:
            p.error("--poll-ms deve estar entre 20 e 5000")
        if not 0 <= args.raw_sample_limit <= 256:
            p.error("--raw-sample-limit deve estar entre 0 e 256")
        if args.duration < 0:
            p.error("--duration deve ser >= 0")
        return run_capture(args)
    if args.command == "annotate":
        try:
            return run_annotate(args)
        except (ValueError, OSError) as exc:
            print(f"[joeytrace] ERRO: {exc}", file=sys.stderr)
            return 1
    if args.command == "effect-windows":
        try:
            output = generate_effect_windows(args.session, before_ms=args.before_ms,
                                             after_ms=args.after_ms, max_events=args.max_events)
            print(f"[joeytrace] Janela dos marcadores: {output}")
            return 0
        except (ValueError, OSError) as exc:
            print(f"[joeytrace] ERRO: {exc}", file=sys.stderr)
            return 1
    if args.command == "list-replays":
        if args.limit < 1:
            p.error("--limit deve ser >= 1")
        list_replays(args.limit)
        return 0
    if args.command == "import-replay":
        try:
            return run_import_replay(args)
        except (ValueError, OSError, FileExistsError) as exc:
            print(f"[joeytrace] ERRO: {exc}", file=sys.stderr)
            return 1
    return run_demo(args)


if __name__ == "__main__":
    sys.exit(main())
