"""Validate native tables and scheduler call order on the pinned PE, without execution."""
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
    tables = {
        0x5efb78: (0x5a59b0, 0x5a41c0, 0x5a8090, 0x5b7b50, 0x5a5120, 0x5a8a50, 0),
        0x5efb94: (0x5a59b0, 0x5a41c0, 0x5a8090, 0x5a5a40, 0x5a5120, 0x5a8a50, 0),
        0x5efbb0: (0x5b9f50, 0x5bc620, 0x5bc3c0, 0),
    }
    for va, expected in tables.items():
        if struct.unpack_from('<' + 'I' * len(expected), data, va - 0x400000) != expected:
            raise ValueError(f'Unexpected scheduling table {va:#x}')
    calls = [(0x5bc669, 0x5bf9c0), (0x5bc672, 0x5bc9b0), (0x5bc67b, 0x5ab820),
             (0x5bc684, 0x5b8c00), (0x5bc68d, 0x5bbe40), (0x5bc696, 0x5ba060),
             (0x5bc69f, 0x5bc050), (0x5bc6c6, 0x5b7bd0), (0x5bc6ea, 0x5bc5d0),
             (0x5bc749, 0x5b9ca0)]
    for site, expected in calls:
        off = site - 0x400000
        if data[off] != 0xe8 or site + 5 + struct.unpack_from('<i', data, off + 1)[0] != expected:
            raise ValueError(f'Unexpected call at {site:#x}')
    report = dict(sha256=digest, tables={hex(k): [hex(v) for v in values] for k, values in tables.items()},
                  calls=[dict(site=hex(site), target=hex(target)) for site, target in calls],
                  limitation='Static tables and call sites; branch conditions and return semantics are documented from listings, not executed.')
    (REPO / 'docs/evidence/turn-scheduling-validation.json').write_text(json.dumps(report, indent=2) + '\n')
    print('PASS: three native scheduling tables and ten call targets')

if __name__ == '__main__':
    main()
