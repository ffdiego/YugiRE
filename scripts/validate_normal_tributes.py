"""Validate native level-to-tribute tables on the pinned executable, without running it."""
import hashlib
import json
from pathlib import Path
import struct

REPO = Path(__file__).resolve().parent.parent


def main():
    data = (REPO.parent / 'passion/joey_pc.exe').read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if digest != 'db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de':
        raise ValueError('Unexpected executable')
    tables = {0x5aadf0: bytes([0, 0, 0, 0, 0, 1, 1]), 0x5aef08: bytes([0, 0, 0, 0, 0, 1, 1])}
    for va, expected in tables.items():
        off = va - 0x400000
        if data[off:off + len(expected)] != expected:
            raise ValueError(f'Unexpected level selector at {va:#x}')
    targets = {0x5aade8: (0x5aace7, 0x5aad1c), 0x5aef00: (0x5aee9a, 0x5aee9e)}
    for va, expected in targets.items():
        if struct.unpack_from('<2I', data, va - 0x400000) != expected:
            raise ValueError(f'Unexpected level jump table at {va:#x}')
    checks = {0x5aee86: '83f806', 0x5aee9a: '33ed', 0x5aee9e: 'bd01000000', 0x5aeea5: 'bd02000000'}
    for va, expected in checks.items():
        off = va - 0x400000
        if data[off:off + len(expected) // 2].hex() != expected:
            raise ValueError(f'Unexpected tribute count branch at {va:#x}')
    report = dict(sha256=digest, level_selectors={hex(va): list(value) for va, value in tables.items()},
                  jump_tables={hex(va): [hex(v) for v in value] for va, value in targets.items()},
                  branches={hex(va): value for va, value in checks.items()},
                  limitation='Static level counts, not complete summon legality, timing or effect interactions.')
    (REPO / 'docs/evidence/normal-tributes-validation.json').write_text(json.dumps(report, indent=2) + '\n')
    print('PASS: two level selectors, two jump tables and four tribute-count branches')


if __name__ == '__main__':
    main()
