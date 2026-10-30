"""Check the fingerprinted 0x7C dispatch and producer calls; never execute game code."""
import hashlib
import json
from pathlib import Path
import struct

REPO = Path(__file__).resolve().parent.parent
EXPECTED = 'db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de'

def main():
    payload = (REPO.parent / 'passion/joey_pc.exe').read_bytes()
    fingerprint = hashlib.sha256(payload).hexdigest()
    if fingerprint != EXPECTED:
        raise ValueError('Unexpected executable; addresses apply only to the pinned PE')
    # This fingerprinted PE maps the queried .text/.data RVAs to equal raw offsets.
    index = payload[0x1b9aec + 0x7c - 1]
    stub = struct.unpack_from('<I', payload, 0x1b9838 + index * 4)[0]
    if index != 82 or stub != 0x5b967c or payload[stub - 0x400000] != 0xe9:
        raise ValueError('Unexpected 0x7C dispatch')
    consumer = stub + 5 + struct.unpack_from('<i', payload, stub - 0x400000 + 1)[0]
    if consumer != 0x405190:
        raise ValueError('Unexpected consumer')
    calls = {}
    for site, expected in [(0x5a3442, 0x577cf0), (0x5a34f8, 0x577cf0), (0x577d5f, 0x5b9170), (0x577da0, 0x5b9170)]:
        offset = site - 0x400000
        target = site + 5 + struct.unpack_from('<i', payload, offset + 1)[0]
        if payload[offset] != 0xe8 or target != expected:
            raise ValueError(f'Unexpected call at {site:#x}')
        calls[hex(site)] = hex(target)
    report = dict(sha256=fingerprint, opcode='0x7c', index=index, stub=hex(stub), consumer=hex(consumer), calls=calls,
                  limitation='Static byte and dispatch validation only; does not execute functions or validate battle formulas.')
    (REPO / 'docs/evidence/battle-command-7c-validation.json').write_text(json.dumps(report, indent=2) + '\n')
    print('PASS: fingerprint, 0x7C dispatch and four native call targets')

if __name__ == '__main__':
    main()
