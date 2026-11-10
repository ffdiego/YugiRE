"""Read the pinned EXE selection tables and validate their YDC resources in data.dat.

No target execution, save access, extraction of game payloads or Ghidra mutation.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct

EXE_SHA256 = "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de"
DAT_SHA256 = "7e8841ceb015861dacf898d4a62a909e399da14bb7d69c40de3afa3a4610e1ed"


def read_tables(exe):
    if hashlib.sha256(exe).hexdigest() != EXE_SHA256:
        raise ValueError("Unsupported executable fingerprint")
    def word(va):
        return struct.unpack_from("<I", exe, va - 0x400000)[0]
    def string(va):
        return exe[va - 0x400000:].split(b"\0", 1)[0].decode("ascii")
    assert string(0x5EBC70) == "data/J/file/CPU_"
    assert string(0x5EBC8C) == "data/J/file/FUL_"
    assert string(0x5EBC84) == ".ydc"
    rows = []
    for level in range(1, 8):
        numbers = [word(0x5DB158 + (level * 5 + slot) * 4) for slot in range(5)]
        rows.append({"pool": "CPU", "effective_level": level,
                     "resources": [f"j\\file\\cpu_{number:03d}.ydc" for number in numbers]})
    for level in range(3, 10):
        count = word(0x5DB080 + level * 4)
        if not 1 <= count <= 7:
            raise ValueError("Invalid FUL option count")
        suffixes = [string(word(0x5DB0A8 + ((level - 3) * 7 + slot) * 4))
                    for slot in range(count)]
        rows.append({"pool": "FUL", "effective_level": level,
                     "resources": [f"j\\file\\ful_{suffix.lower()}.ydc" for suffix in suffixes]})
    return rows


def deck_counts(data):
    if len(data) < 14:
        raise ValueError("Truncated YDC header")
    position, counts = 8, []
    for limit in (80, 15, 15):
        if position + 2 > len(data):
            raise ValueError("Truncated YDC group")
        count = struct.unpack_from("<H", data, position)[0]
        position += 2 + count * 2
        if count > limit or position > len(data):
            raise ValueError("Invalid YDC group")
        counts.append(count)
    if position != len(data):
        raise ValueError("Trailing YDC bytes")
    return counts


def inspect(exe_path, dat_path):
    exe = exe_path.read_bytes()
    rows = read_tables(exe)
    with dat_path.open("rb") as archive:
        if hashlib.file_digest(archive, "sha256").hexdigest() != DAT_SHA256:
            raise ValueError("Unsupported DAT fingerprint")
        archive.seek(0)
        if archive.read(8) != b"KCEJYUGI":
            raise ValueError("Invalid DAT signature")
        count = struct.unpack("<I", archive.read(4))[0]
        resources = {}
        for _ in range(count):
            record = archive.read(268)
            name = bytes((value >> 4) | ((value & 15) << 4) for value in record[:256])
            name = name.split(b"\0", 1)[0].decode("ascii").lower()
            resources[name] = struct.unpack_from("<III", record, 256)
        names = sorted({name for row in rows for name in row["resources"]})
        decks = {}
        for name in names:
            offset, size, stored = resources[name]
            if size != stored or size > 234:
                raise ValueError(f"Unexpected bounded YDC resource: {name}")
            archive.seek(offset)
            data = archive.read(size)
            counts = deck_counts(data)
            main = struct.unpack_from("<" + "H" * counts[0], data, 10)
            canonical = b"".join(value.to_bytes(2, "little") for value in sorted(main))
            decks[name] = {"offset": offset, "bytes": size, "group_counts": deck_counts(data),
                           "sha256": hashlib.sha256(data).hexdigest(),
                           "main_multiset_sha256": hashlib.sha256(canonical).hexdigest()}
    return {"exe_sha256": EXE_SHA256, "dat_sha256": DAT_SHA256,
            "method": "Static EXE tables, pinned DAT index and bounded YDC validation; no game execution",
            "tables": rows, "unique_resource_count": len(decks),
            "unique_main_multiset_count": len({deck["main_multiset_sha256"] for deck in decks.values()}),
            "progression_thresholds": {
                "win": {str(level): struct.unpack_from("<I", exe, 0x5DAF40 - 0x400000 + level * 4)[0]
                        for level in range(1, 9)},
                "non_win": {str(level): struct.unpack_from("<I", exe, 0x5DAF64 - 0x400000 + level * 4)[0]
                            for level in range(2, 10)}},
            "decks": decks}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("exe", type=Path)
    parser.add_argument("dat", type=Path)
    args = parser.parse_args()
    print(json.dumps(inspect(args.exe, args.dat), indent=2))


if __name__ == "__main__":
    main()
