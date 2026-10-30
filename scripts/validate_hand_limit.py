"""Check the pinned native hand-limit constants and request calls, without execution."""
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
    # For this fingerprint the inspected code's file offsets equal VA - image base.
    checks = {0x5a58bc: '6893050000', 0x5a58e0: '83fa06',
              0x5a58e3: '0f8685fdffff', 0x5a58f0: '83e806', 0x5bcde7: '6a01'}
    for va, expected in checks.items():
        off = va - 0x400000
        if data[off:off + len(expected) // 2].hex() != expected:
            raise ValueError(f'Unexpected native hand-limit instruction at {va:#x}')
    calls = [(0x5a58c1, 0x5699c0), (0x5a58f7, 0x5bcdc0), (0x5bcdea, 0x5bcc70)]
    for site, target in calls:
        off = site - 0x400000
        if data[off] != 0xe8 or site + 5 + struct.unpack_from('<i', data, off + 1)[0] != target:
            raise ValueError(f'Unexpected call at {site:#x}')
    report = dict(sha256=digest, instructions={hex(va): value for va, value in checks.items()},
                  calls=[dict(site=hex(site), target=hex(target)) for site, target in calls],
                  limitation='Static bytes only; not execution of discard selection, triggers or turn scheduling.')
    (REPO / 'docs/evidence/hand-limit-validation.json').write_text(json.dumps(report, indent=2) + '\n')
    print('PASS: five hand-limit instructions and three native call targets')


if __name__ == '__main__':
    main()
