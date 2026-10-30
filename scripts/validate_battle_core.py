"""Check the pinned battle comparison, snapshot transfer and damage call chain."""
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
    sequence = struct.unpack_from('<4I', data, 0x1efa84)
    if sequence != (0x5a2740, 0x5a2990, 0x5a2d60, 0x5a31a0):
        raise ValueError('Unexpected battle sequence table')
    dispatch = {}
    transfers = []
    for op, index, stub, target in [(0x31, 29, 0x5b952f, 0x407a20), (0x3c, 39, 0x5b9561, 0x405020)]:
        actual_index = data[0x1b9aec + op - 1]
        if actual_index != index or struct.unpack_from('<I', data, 0x1b9838 + index * 4)[0] != stub:
            raise ValueError('Unexpected battle command dispatch')
        dispatch[hex(op)] = dict(index=index, stub=hex(stub), consumer=hex(target))
        transfers.append((stub, 0xe9, target))
    transfers += [(0x5a2780, 0xe8, 0x5b7c50), (0x5a2800, 0xe8, 0x5b8210),
                  (0x5a2b50, 0xe8, 0x578540), (0x5a2b67, 0xe8, 0x578540),
                  (0x57862a, 0xe8, 0x5b9170), (0x40509d, 0xe8, 0x5bde10),
                  (0x4050a8, 0xe8, 0x5bddf0)]
    for site, opcode, target in transfers:
        off = site - 0x400000
        if data[off] != opcode or site + 5 + struct.unpack_from('<i', data, off + 1)[0] != target:
            raise ValueError(f'Unexpected battle control transfer at {site:#x}')
    checks = {0x5b82ec: '85c90f8e1c010000', 0x5b8416: '3dffff0000',
              0x5b842b: '3dffff0000', 0x405072: '6890000000', 0x578627: '0c43'}
    for va, expected in checks.items():
        raw = bytes.fromhex(expected)
        if data[va - 0x400000:va - 0x400000 + len(raw)] != raw:
            raise ValueError(f'Unexpected battle instruction at {va:#x}')
    report = dict(sha256=digest, sequence=[hex(x) for x in sequence], dispatch=dispatch,
                  transfers=[dict(site=hex(site), target=hex(target)) for site, _, target in transfers],
                  instruction_sites=[hex(x) for x in checks],
                  limitation='Static checks only. Covers the selected core and transfer chain, not all card overrides or frame timing.')
    (REPO / 'docs/evidence/battle-core-validation.json').write_text(json.dumps(report, indent=2) + '\n')
    print('PASS: battle sequence, two dispatch entries, nine transfers and five instruction checks')

if __name__ == '__main__':
    main()
