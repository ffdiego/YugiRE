"""Check the pinned command-2 dispatch and shared temporary-state cleanup calls."""
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
    index = data[0x1b9aec + 1]
    stub = struct.unpack_from('<I', data, 0x1b9838 + index * 4)[0]
    if index != 1 or stub != 0x5b94a3:
        raise ValueError('Unexpected opcode-2 dispatch')
    targets = {}
    for site, opcode, target in [(stub, 0xe9, 0x574230), (0x5743d2, 0xe8, 0x56aea0), (0x56af3a, 0xe8, 0x5695e0)]:
        off = site - 0x400000
        if data[off] != opcode or site + 5 + struct.unpack_from('<i', data, off + 1)[0] != target:
            raise ValueError(f'Unexpected control transfer at {site:#x}')
        targets[hex(site)] = hex(target)
    checks = {0x574265: 'b90c000000', 0x574271: '81e6ffff0000', 0x56af0d: '3c03', 0x56af11: '3c0c'}
    for va, expected in checks.items():
        raw = bytes.fromhex(expected)
        if data[va - 0x400000:va - 0x400000 + len(raw)] != raw:
            raise ValueError(f'Unexpected cleanup instruction at {va:#x}')
    report = dict(sha256=digest, opcode='0x02', normalized_index=index, stub=hex(stub), transfers=targets,
                  instruction_sites=[hex(x) for x in checks], virtual_slot_flag_address=hex(0xa55d74 + 11 * 0x90 + 0x8c),
                  limitation='Static bytes only; does not identify the producer/event scheduling command 2.')
    (REPO / 'docs/evidence/temporary-cleanup-validation.json').write_text(json.dumps(report, indent=2) + '\n')
    print('PASS: command-2 dispatch, cleanup transfers, count, flag mask and association kinds')

if __name__ == '__main__':
    main()
