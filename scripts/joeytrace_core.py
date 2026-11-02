"""JoeyTrace core: pure parsing, diffing and session writing (no game process access).

Mapped bytes are taken from scripts/Get-JoeyDuelSnapshot.ps1 in YugiRE.
This module deliberately uses neutral *observed change* labels: a diff is
not proof of a specific action or of its cause.
"""
from __future__ import annotations

import collections
import datetime as dt
import hashlib
import json
import struct
from pathlib import Path
from typing import Any

EXPECTED_EXE_SHA256 = "DB8FE7B7EA4B12F18A9BB1777B5F5B8EAB254011DB2A52571A0264725505B6DE"
IMAGE_BASE_DOCUMENTED = 0x00400000
MEMORY_VA_DOCUMENTED = 0x00A55D64
MEMORY_RVA = MEMORY_VA_DOCUMENTED - IMAGE_BASE_DOCUMENTED
PLAYER_STRIDE = 0xD44
BLOCK_SIZE = PLAYER_STRIDE * 2
MONSTER_COUNT = 5
MONSTER_START = 0x10
MONSTER_STRIDE = 0x90
PLAYER_FLAGS_OFFSET = 0x00A56430 - MEMORY_VA_DOCUMENTED


def utc_now() -> str:
    return dt.datetime.now(dt.timezone.utc).isoformat(timespec="milliseconds").replace("+00:00", "Z")


def parse_state(data: bytes) -> dict[str, Any]:
    """Decode established *visible fields only* from two player blocks."""
    if len(data) != BLOCK_SIZE:
        raise ValueError(f"Expected {BLOCK_SIZE} bytes, got {len(data)}")
    players: list[dict[str, Any]] = []
    for side in range(2):
        start = side * PLAYER_STRIDE
        lp = struct.unpack_from("<H", data, start)[0]
        hand_count = data[start + 4]
        flags = struct.unpack_from("<I", data, start + PLAYER_FLAGS_OFFSET)[0]
        monsters = []
        for zone in range(MONSTER_COUNT):
            pos = start + MONSTER_START + zone * MONSTER_STRIDE
            index = struct.unpack_from("<I", data, pos)[0] & 0xFFF
            occurrence = struct.unpack_from("<H", data, pos + 4)[0]
            position = struct.unpack_from("<H", data, pos + 6)[0]
            mflags = struct.unpack_from("<I", data, pos + 0x8C)[0]
            monsters.append({
                "zone": zone,
                "card_index": index,
                "occupied": index != 0,
                "occurrence_word": occurrence,
                "position_word_hex": f"{position:04X}",
                "flags_hex": f"{mflags:08X}",
                "position_locked_this_turn": index != 0 and bool(mflags & 0x20000),
            })
        players.append({
            "side": side,
            "life_points": lp,
            "hand_count": hand_count,
            "player_flags_hex": f"{flags:08X}",
            "monsters": monsters,
        })
    return {"players": players}


def plausible_duel(state: dict[str, Any]) -> bool:
    """Conservative heuristic to avoid treating the menu as an active duel.

    Not a proven native duel-start flag. Once a duel is armed, captures can
    legitimately contain LP=0 (the defeat state).
    """
    players = state["players"]
    return all(
        0 < p["life_points"] <= 20000
        and p["hand_count"] <= 80
        and all(m["card_index"] <= 1114 for m in p["monsters"])
        for p in players
    )


def diff_states(before: dict[str, Any], after: dict[str, Any]) -> list[dict[str, Any]]:
    changes: list[dict[str, Any]] = []
    for old, new in zip(before["players"], after["players"]):
        side = old["side"]
        for key in ("life_points", "hand_count", "player_flags_hex"):
            if old[key] != new[key]:
                entry: dict[str, Any] = {
                    "type": key + "_changed", "side": side,
                    "before": old[key], "after": new[key],
                }
                if key == "life_points":
                    entry["delta"] = new[key] - old[key]
                if key == "player_flags_hex":
                    entry["xor_hex"] = f"{int(old[key], 16) ^ int(new[key], 16):08X}"
                changes.append(entry)
        for a, b in zip(old["monsters"], new["monsters"]):
            zone = a["zone"]
            if (a["card_index"], a["occurrence_word"]) != (b["card_index"], b["occurrence_word"]):
                changes.append({
                    "type": "monster_identity_changed", "side": side, "zone": zone,
                    "before": {"card_index": a["card_index"], "occurrence_word": a["occurrence_word"]},
                    "after": {"card_index": b["card_index"], "occurrence_word": b["occurrence_word"]},
                })
            if a["position_word_hex"] != b["position_word_hex"]:
                changes.append({
                    "type": "monster_position_changed", "side": side, "zone": zone,
                    "before": a["position_word_hex"], "after": b["position_word_hex"],
                })
            if a["flags_hex"] != b["flags_hex"]:
                changes.append({
                    "type": "monster_flags_changed", "side": side, "zone": zone,
                    "before": a["flags_hex"], "after": b["flags_hex"],
                    "xor_hex": f"{int(a['flags_hex'], 16) ^ int(b['flags_hex'], 16):08X}",
                })
    return changes


def raw_byte_diff(before: bytes, after: bytes, limit: int = 32) -> dict[str, Any]:
    """Preserve raw byte offsets to enable later field mapping without claiming semantics."""
    if len(before) != BLOCK_SIZE or len(after) != BLOCK_SIZE:
        raise ValueError("Invalid block length")
    changed = [(i, a, b) for i, (a, b) in enumerate(zip(before, after)) if a != b]
    return {
        "count": len(changed),
        "sample": [
            {"block_offset_hex": f"0x{i:04X}", "before_hex": f"{a:02X}", "after_hex": f"{b:02X}"}
            for i, a, b in changed[:limit]
        ],
        "sample_truncated": len(changed) > limit,
    }


def _write_json_line(handle: Any, obj: dict[str, Any]) -> None:
    handle.write(json.dumps(obj, ensure_ascii=False, separators=(",", ":")) + "\n")
    handle.flush()


class TraceSession:
    """Append-only local evidence. No native calls; easy to test with synthetic blocks."""

    def __init__(self, folder: Path, metadata: dict[str, Any], *, track_unknown: bool = False,
                 raw_blocks: bool = False, raw_sample_limit: int = 32):
        folder = Path(folder)
        folder.mkdir(parents=True, exist_ok=False)  # no silent overwrite
        self.folder = folder
        self.track_unknown = track_unknown
        self.raw_sample_limit = raw_sample_limit
        self.raw_blocks = raw_blocks
        self.manifest: dict[str, Any] = {
            "schema": "joeytrace-v1", "status": "recording", "started_utc": utc_now(),
            "capture_mode": "read_only_external_polling", "data_origin": "ReadProcessMemory",
            "memory_rva_hex": f"0x{MEMORY_RVA:08X}",
            "memory_block_size": BLOCK_SIZE,
            "sampling_is_atomic": False,
            "native_command_hook_enabled": False,
            "interpretation_limit": "Changes are sampled state differences, not proven game actions; intermediate states can be missed.",
            **metadata,
        }
        self.counts: collections.Counter[str] = collections.Counter()
        self.change_batches = 0
        self.marker_count = 0
        self.poll_count = 0
        self.read_error_count = 0
        self.armed_at_utc: str | None = None
        self.last_snapshot_utc: str | None = None
        self.before: dict[str, Any] | None = None
        self.before_raw: bytes | None = None
        self.sequence = 0
        self.files = {
            "snapshots": (folder / "snapshots.jsonl").open("x", encoding="utf-8", newline="\n"),
            "changes": (folder / "state-changes.jsonl").open("x", encoding="utf-8", newline="\n"),
            "markers": (folder / "markers.jsonl").open("x", encoding="utf-8", newline="\n"),
        }
        self.raw_file = (folder / "raw-snapshots.bin").open("xb") if raw_blocks else None
        if self.raw_file:
            self.raw_file.write(b"JTRC1\x00\x00\x00")
            self.raw_file.flush()
        self._save_manifest()

    def _save_manifest(self) -> None:
        # Manifest may be updated. Stream files remain append-only.
        temp = self.folder / "manifest.json.tmp"
        temp.write_text(json.dumps(self.manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        temp.replace(self.folder / "manifest.json")

    def _save_snapshot(self, state: dict[str, Any], raw: bytes, elapsed_ms: int,
                       captured_utc: str, reason: str) -> None:
        obj = {"seq": self.sequence, "elapsed_ms": elapsed_ms, "captured_utc": captured_utc,
               "reason": reason, "raw_sha256": hashlib.sha256(raw).hexdigest(), **state}
        _write_json_line(self.files["snapshots"], obj)
        if self.raw_file:
            # little endian: uint32 sequence, uint64 elapsed milliseconds,
            # uint32 length, then exactly BLOCK_SIZE bytes.
            self.raw_file.write(struct.pack("<IQI", self.sequence, elapsed_ms, len(raw)))
            self.raw_file.write(raw)
            self.raw_file.flush()

    def sample(self, raw: bytes, elapsed_ms: int, captured_utc: str | None = None) -> bool:
        """Return True if a semantic or opted-in unknown-only change was recorded."""
        self.poll_count += 1
        captured_utc = captured_utc or utc_now()
        state = parse_state(raw)
        if self.before is None:
            self.before = state
            self.before_raw = raw
            self.armed_at_utc = captured_utc
            self.last_snapshot_utc = captured_utc
            self._save_snapshot(state, raw, elapsed_ms, captured_utc, "baseline")
            self._save_manifest()
            return False
        assert self.before_raw is not None
        delta = diff_states(self.before, state)
        # Preserve comparison against previous sample even if no semantic fields changed.
        raw_diff = raw_byte_diff(self.before_raw, raw, self.raw_sample_limit)
        should_save = bool(delta) or (self.track_unknown and raw_diff["count"] > 0)
        if should_save:
            self.sequence += 1
            self.change_batches += 1
            self.counts.update(entry["type"] for entry in delta)
            event = {"seq": self.sequence, "elapsed_ms": elapsed_ms, "captured_utc": captured_utc,
                     "type": "observed_state_difference", "changes": delta,
                     "raw_byte_diff": raw_diff, "evidence_method": "non_atomic_polling"}
            _write_json_line(self.files["changes"], event)
            self._save_snapshot(state, raw, elapsed_ms, captured_utc, "observed_change")
        self.before = state
        self.before_raw = raw
        self.last_snapshot_utc = captured_utc
        return should_save

    def marker(self, elapsed_ms: int, note: str = "", source: str = "F9") -> None:
        self.marker_count += 1
        _write_json_line(self.files["markers"], {
            "marker_index": self.marker_count, "elapsed_ms": elapsed_ms,
            "captured_utc": utc_now(), "source": source, "note": note,
            "semantic_label_supplied_by_operator": bool(note.strip()),
        })

    def record_read_error(self, message: str, elapsed_ms: int) -> None:
        self.read_error_count += 1
        # Keep the reason rather than silently accepting discontinuities.
        _write_json_line(self.files["markers"], {
            "elapsed_ms": elapsed_ms, "captured_utc": utc_now(),
            "source": "collector", "type": "read_error", "message": message,
        })

    def finish(self, status: str, elapsed_ms: int, message: str = "") -> Path:
        self.manifest.update({
            "status": status, "finished_utc": utc_now(), "duration_ms": elapsed_ms,
            "poll_count": self.poll_count, "change_batches": self.change_batches,
            "marker_count": self.marker_count, "read_error_count": self.read_error_count,
            "armed_at_utc": self.armed_at_utc, "last_snapshot_utc": self.last_snapshot_utc,
            "event_counts": dict(sorted(self.counts.items())),
        })
        if message:
            self.manifest["exit_reason"] = message
        self._save_manifest()
        for handle in self.files.values():
            handle.close()
        if self.raw_file:
            self.raw_file.close()
        report = self.folder / "report.md"
        rows = "\n".join(f"| `{k}` | {v} |" for k, v in sorted(self.counts.items()))
        if not rows:
            rows = "| (nenhuma mudanca semantica observada) | 0 |"
        report.write_text(
            f"# JoeyTrace — relato de captura\n\n"
            f"- Estado: **{status}**\n"
            f"- Inicio: `{self.manifest['started_utc']}`\n"
            f"- Fim: `{self.manifest['finished_utc']}`\n"
            f"- Duracao: {elapsed_ms / 1000:.1f} segundos\n"
            f"- Leituras bem-sucedidas: {self.poll_count}; falhas: {self.read_error_count}\n"
            f"- Lotes de alteracoes: **{self.change_batches}**\n"
            f"- Marcadores: **{self.marker_count}**\n"
            f"- Comandos nativos capturados: **nao** (nenhum hook instalado)\n\n"
            f"## Campos alterados\n\n| Campo | Ocorrencias |\n|---|---:|\n{rows}\n\n"
            f"## Evidencias\n\n"
            f"- `manifest.json`: versao, fingerprint e parametros do coletor.\n"
            f"- `state-changes.jsonl`: deltas observados, com offsets dos bytes modificados.\n"
            f"- `snapshots.jsonl`: estados interpretados na primeira leitura e a cada mudanca registrada.\n"
            f"- `markers.jsonl`: marcadores manuais e erros de leitura.\n"
            + ("- `raw-snapshots.bin`: blocos binarios completos, somente para RE local.\n" if self.raw_blocks else "")
            + "\n## Limites\n\nA leitura e periodica e nao atomica. Mudancas entre amostras podem ser perdidas; "
              "as labels indicam diferencas observadas e nao a causa das alteracoes. "
              "Indices de carta nao sao IDs de recurso. A captura nao contem a fila nativa de comandos.\n",
            encoding="utf-8",
        )
        return report
