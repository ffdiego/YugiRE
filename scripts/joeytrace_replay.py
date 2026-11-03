"""Read-only discovery of Joey replay directories via Windows registry views.

The confirmed value is 'CommonDir' under HKLM\SOFTWARE\KONAMI\Yu-Gi-Oh!
Power Of Chaos\system. 32-bit and 64-bit registry views are queried using
WinReg flags instead of manually inserting WOW6432Node into that key.
"""
from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime
import os
from pathlib import Path
import sys

_GAME_SYSTEM_KEY = r"SOFTWARE\KONAMI\Yu-Gi-Oh! Power Of Chaos\system"
_VIRTUAL_BASE = r"Software\Classes\VirtualStore\MACHINE\SOFTWARE"


@dataclass(frozen=True)
class ReplayLocation:
    path: Path
    registry_source: str
    value_name: str
    exists: bool


@dataclass(frozen=True)
class ReplayCandidate:
    path: Path
    modified_ns: int
    source: str


def discover_registry_locations(registry=None) -> list[ReplayLocation]:
    """Enumerate recorded directories; never write to HKLM/HKCU.

    VirtualStore is tested explicitly because old unmanifested 32-bit apps
    could have HKLM writes redirected to per-user storage. A 64-bit view is
    also inspected, but this does not prove Joey itself uses that view.
    """
    if registry is None:
        if sys.platform != "win32":
            return []
        import winreg as registry

    hklm = registry.HKEY_LOCAL_MACHINE
    hkcu = registry.HKEY_CURRENT_USER
    read = registry.KEY_READ
    view32 = getattr(registry, "KEY_WOW64_32KEY", 0)
    view64 = getattr(registry, "KEY_WOW64_64KEY", 0)
    roots = [
        (hklm, _GAME_SYSTEM_KEY, read | view32, "HKLM (visao 32 bits)"),
        (hkcu, _VIRTUAL_BASE + r"\WOW6432Node\KONAMI\Yu-Gi-Oh! Power Of Chaos\system",
         read, "HKCU VirtualStore/WOW6432Node"),
        (hkcu, _VIRTUAL_BASE + r"\KONAMI\Yu-Gi-Oh! Power Of Chaos\system",
         read, "HKCU VirtualStore"),
        (hklm, _GAME_SYSTEM_KEY, read | view64, "HKLM (visao 64 bits)"),
    ]
    locations: list[ReplayLocation] = []
    for hive, key, access, label in roots:
        try:
            with registry.OpenKey(hive, key, 0, access) as handle:
                # CommonDir is proved by docs/27-save-and-collection.md.
                # ReplayDir is a conservative optional extension for installs
                # with an explicitly configured replay directory.
                for value_name in ("CommonDir", "ReplayDir"):
                    try:
                        value, kind = registry.QueryValueEx(handle, value_name)
                    except OSError:
                        continue
                    supported = (registry.REG_SZ, registry.REG_EXPAND_SZ)
                    if kind not in supported or not isinstance(value, str) or not value.strip():
                        continue
                    value = os.path.expandvars(value.strip().strip('"'))
                    folder = Path(value).expanduser()
                    if not folder.is_absolute():
                        continue
                    locations.append(ReplayLocation(
                        path=folder, registry_source=f"{label}: {key}",
                        value_name=value_name, exists=folder.is_dir(),
                    ))
        except OSError:
            continue
    return locations


def find_replays(locations: list[ReplayLocation]) -> list[ReplayCandidate]:
    """Read immediate .ydr files from the discovered directories, newest first."""
    discovered: dict[str, ReplayCandidate] = {}
    for location in locations:
        if not location.exists:
            continue
        try:
            entries = location.path.iterdir()
            for file in entries:
                if file.suffix.lower() != ".ydr" or not file.is_file():
                    continue
                try:
                    mtime = file.stat().st_mtime_ns
                except OSError:
                    continue
                normalized = os.path.normcase(str(file.resolve()))
                item = ReplayCandidate(file, mtime, f"{location.registry_source} [{location.value_name}]")
                discovered.setdefault(normalized, item)
        except OSError:
            continue
    return sorted(discovered.values(), key=lambda item: (-item.modified_ns, str(item.path).lower()))


def describe_locations(locations: list[ReplayLocation]) -> None:
    print("[joeytrace] Busca no Registro: HKLM 32/64 bits e HKCU VirtualStore.")
    if not locations:
        print("[joeytrace] Nenhum valor CommonDir/ReplayDir encontrado nessas visoes.")
        return
    for location in locations:
        status = "OK" if location.exists else "PASTA INEXISTENTE"
        print(f"[joeytrace] {status} {location.registry_source}\\{location.value_name}")
        print(f"            {location.path}")


def list_replays(limit: int = 10, *, registry=None) -> list[ReplayCandidate]:
    locations = discover_registry_locations(registry)
    describe_locations(locations)
    candidates = find_replays(locations)
    if candidates:
        print(f"[joeytrace] Replays encontrados: {len(candidates)}; mais recentes:")
        for i, item in enumerate(candidates[:limit], 1):
            timestamp = datetime.fromtimestamp(item.modified_ns / 1e9).astimezone().strftime("%Y-%m-%d %H:%M:%S")
            print(f"  {i}. {item.path.name}  ({timestamp}, {item.source})")
    else:
        print("[joeytrace] Nenhum arquivo .ydr encontrado nos diretorios registrados.")
    return candidates


def choose_replay(*, registry=None, input_fn=input) -> Path | None:
    """Suggest latest replay, let user choose index or enter another file path.

    No silent import: modification time does not establish the replay belongs
    to the captured duel. Explicit confirmation is always required.
    """
    if not sys.stdin.isatty():
        raise ValueError("Modo interativo indisponivel; informe o replay com --file caminho.ydr")
    items = list_replays(limit=10, registry=registry)
    if items:
        print("[joeytrace] Enter aceita o mais recente; numero 1-10 seleciona outro; ")
        print("            caminho completo escolhe arquivo manual; 0 cancela.")
        response = input_fn("[joeytrace] Replay [1]: ").strip().strip('"')
        if not response:
            chosen = items[0].path
        elif response == "0":
            return None
        elif response.isdecimal() and 1 <= int(response) <= min(10, len(items)):
            chosen = items[int(response)-1].path
        else:
            chosen = Path(response).expanduser()
    else:
        response = input_fn("[joeytrace] Caminho completo do .ydr (Enter cancela): ").strip().strip('"')
        if not response:
            return None
        chosen = Path(response).expanduser()
    if not chosen.is_file() or chosen.suffix.lower() != ".ydr":
        raise ValueError(f"Replay .ydr nao encontrado: {chosen}")
    return chosen
