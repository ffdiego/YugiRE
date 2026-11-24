#!/usr/bin/env python3
"""Read-only fingerprint/registration/branch checks for P045; emits metadata only."""
import argparse
import hashlib
import json
import struct
from pathlib import Path

EXE_SHA256 = 'db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de'
# resource, internal index, callback row, branch VA, damage
CARDS = [(342, 755, 66, 0x584f1b, 200), (343, 769, 67, 0x584f42, 500),
         (344, 738, 68, 0x584f5e, 600), (345, 557, 69, 0x584fa7, 800),
         (1007, 1049, 172, 0x585076, 300)]


def inspect(path):
    data = Path(path).read_bytes()
    if hashlib.sha256(data).hexdigest() != EXE_SHA256:
        raise ValueError('EXE fingerprint mismatch')
    pe = struct.unpack_from('<I', data, 0x3c)[0]
    count, optional_size = struct.unpack_from('<H', data, pe+6)[0], struct.unpack_from('<H', data, pe+20)[0]
    optional = pe+24
    image_base = struct.unpack_from('<I', data, optional+28)[0]
    sections = [struct.unpack_from('<IIII', data, optional+optional_size+40*i+8) for i in range(count)]
    def read(va, size):
        rva = va-image_base
        for virtual_size, address, raw_size, offset in sections:
            if address <= rva and rva+size <= address+raw_size:
                return data[offset+rva-address:offset+rva-address+size]
        raise ValueError('VA outside initialized PE sections')
    rows = []
    for resource, index, row, va, damage in CARDS:
        words = struct.unpack('<6I', read(0x5ed0a8+24*row, 24))
        if words != (resource, 0x584e60, 0, 0, 0, 0x593cc0):
            raise ValueError('P045 registration mismatch')
        expected = bytes.fromhex('8a5f06be')+struct.pack('<I', damage)+bytes.fromhex('83e3015653e8')
        actual = read(va, len(expected)+4+9)
        if not actual.startswith(expected):
            raise ValueError('Branch immediate/target mismatch')
        if va+18+struct.unpack_from('<i', actual, 14)[0] != 0x5784e0:
            raise ValueError('Unexpected damage helper')
        if actual[18:25] != bytes.fromhex('83c4086633c05f'):
            raise ValueError('Unexpected low-word return')
        rows.append(dict(resourceId=resource, internalIndex=index, callbackRow=row,
                         branchVa=f'0x{va:08x}', damage=damage, branchBytes=actual.hex()))
    checks = [(0x584e76, '8a4704a8040f8599020000'),
              (0x593cc0, '8b442404ba010000008a480283e1012bd15250e878edffff83c408b801000000c3'),
              (0x5784fd, '6a01f7d81bc06a002500800000570c4350')]
    for va, expected in checks:
        if read(va, len(bytes.fromhex(expected))) != bytes.fromhex(expected):
            raise ValueError(f'Control sequence mismatch at {va:x}')
    return dict(exeSha256=EXE_SHA256, imageBase=f'0x{image_base:08x}', kind='StaticAnalysis',
                method='PE section mapping, exact bytes and relative-call checks; no game execution', cards=rows,
                limitations=['Not a native observation or differential validation.', 'Caller legality, timing, suppression semantics and animation remain outside this check.'])


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--exe', required=True)
    args = p.parse_args()
    print(json.dumps(inspect(args.exe), ensure_ascii=False, sort_keys=True, indent=2))
