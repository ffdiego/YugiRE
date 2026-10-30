"""Validate static Ritual mode dispatch and command 0x70 on the pinned executable."""
import hashlib
import json
from pathlib import Path
import struct

REPO = Path(__file__).resolve().parent.parent

def main():
    data = (REPO.parent / 'passion/joey_pc.exe').read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if digest != 'db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de':
        raise ValueError('Unexpected executable fingerprint')
    if data[0x1adbc5:0x1adbc7] != bytes.fromhex('0c14'):
        raise ValueError('Ritual placement mode changed')
    targets = list(struct.unpack_from('<6I', data, 0x1abca8))
    if targets != [0x5ab87f, 0x5ab893, 0x5ab8a7, 0x5ab8bb, 0x5ab8cf, 0x5ab8e3]:
        raise ValueError('Placement mode jump table changed')
    calls = {}
    for site, target in [(0x5adbe8, 0x5ad8c0), (0x5ab8cf, 0x5ad210), (0x5ad26e, 0x5acf10), (0x5ad05d, 0x5b9170)]:
        offset = site - 0x400000
        if data[offset] != 0xe8 or site + 5 + struct.unpack_from('<i', data, offset + 1)[0] != target:
            raise ValueError(f'Unexpected call at {site:#x}')
        calls[hex(site)] = hex(target)
    if data[0x1ad05b:0x1ad05d] != bytes.fromhex('6a70'):
        raise ValueError('Expected command 0x70')
    index = data[0x1b9aec + 0x70 - 1]
    stub = struct.unpack_from('<I', data, 0x1b9838 + index * 4)[0]
    if index != 70 or stub != 0x5b962c or data[stub - 0x400000] != 0xe9:
        raise ValueError('Command dispatch changed')
    consumer = stub + 5 + struct.unpack_from('<i', data, stub - 0x400000 + 1)[0]
    if consumer != 0x4063e0:
        raise ValueError('Command consumer changed')
    report = dict(sha256=digest, mode=5, table_index=4, dispatch_targets=[hex(x) for x in targets], calls=calls,
                  command='0x70', normalized_index=index, stub=hex(stub), consumer=hex(consumer),
                  limitation='Static byte checks only; no native execution or timing validation.')
    (REPO / 'docs/evidence/ritual-placement-validation.json').write_text(json.dumps(report, indent=2) + '\n')
    print('PASS: fingerprint, Ritual mode table, four calls and command 0x70 dispatch')

if __name__ == '__main__':
    main()
