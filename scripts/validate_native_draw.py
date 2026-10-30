"""Validate selected draw/deal paths and callback constants without native execution."""
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
    records = {0x5ed9a8: (539, 0x5887c0, 0, 0, 0, 0x593ca0),
               0x5ee110: (1010, 0x5887c0, 0, 0x580780, 0, 0x593ca0),
               0x5ef5b0: (1447, 0x5887c0, 0, 0x580780, 0, 0x593ca0)}
    for va, expected in records.items():
        if struct.unpack_from('<6I', data, va - 0x400000) != expected:
            raise ValueError(f'Unexpected draw callback record at {va:#x}')
    index = data[0x1b9aec + 0x61 - 1]
    stub = struct.unpack_from('<I', data, 0x1b9838 + index * 4)[0]
    if index != 57 or stub != 0x5b95e6:
        raise ValueError('Unexpected 0x61 dispatch')
    transfers = [(stub, 0xe9, 0x571e30), (0x571eb7, 0xe8, 0x5691d0),
                 (0x5691dd, 0xe8, 0x569010), (0x5691ef, 0xe8, 0x56cef0),
                 (0x58881a, 0xe8, 0x578b60), (0x578b6c, 0xe8, 0x578860),
                 (0x578921, 0xe8, 0x5b9170)]
    for site, opcode, target in transfers:
        off = site - 0x400000
        if data[off] != opcode or site + 5 + struct.unpack_from('<i', data, off + 1)[0] != target:
            raise ValueError(f'Unexpected transfer at {site:#x}')
    checks = {0x5b9ff3: '6a05', 0x5ba011: '6a05', 0x588813: 'be02000000',
              0x571fa9: '80886d5da50001', 0x571e61: '66ff0da450a500'}
    for va, expected in checks.items():
        raw = bytes.fromhex(expected)
        if data[va - 0x400000:va - 0x400000 + len(raw)] != raw:
            raise ValueError(f'Unexpected draw instruction at {va:#x}')
    exodia_calls = (0x5bbf78, 0x5bbf89, 0x5bbf9a, 0x5bbfab, 0x5bbfbc)
    terminal_calls = [(site, 0x56d2c0) for site in exodia_calls]
    terminal_calls += [(0x5bbfdb, 0x5699a0), (0x5bbfef, 0x569720),
                       (0x5bc005, 0x569720), (0x5bc01b, 0x569720), (0x5bc031, 0x569720)]
    for expected_id, site in enumerate(exodia_calls, 16):
        if data[site - 0x400000 - 3:site - 0x400000 - 1] != bytes((0x6a, expected_id)):
            raise ValueError('Unexpected Exodia hand predicate ID')
    for site, target in terminal_calls:
        off = site - 0x400000
        if data[off] != 0xe8 or site + 5 + struct.unpack_from('<i', data, off + 1)[0] != target:
            raise ValueError('Unexpected terminal predicate call')
    registered = {struct.unpack_from('<I', data, 0x1ed0a8 + row * 24)[0] for row in range(443)}
    if registered.intersection(range(16, 21)):
        raise ValueError('Expected Exodia parts to have no record in this callback table')
    report = dict(sha256=digest, opcode='0x61', normalized_index=index, stub=hex(stub), consumer='0x571e30',
                  records={hex(k): [hex(v) for v in row] for k, row in records.items()},
                  transfers=[dict(site=hex(site), target=hex(target)) for site, _, target in transfers],
                  instruction_sites=[hex(x) for x in checks],
                  terminal_calls=[dict(site=hex(site), target=hex(target)) for site, target in terminal_calls],
                  exodia_ids_without_callback_record=list(range(16, 21)),
                  limitation='Static checks only; no native drawing, shuffling, animations, terminal scenarios or chain scenarios executed.')
    (REPO / 'docs/evidence/native-draw-validation.json').write_text(json.dumps(report, indent=2) + '\n')
    print('PASS: draw dispatch, three card records, seven transfers, five constants and ten terminal calls; Exodia IDs absent from callback table')

if __name__ == '__main__':
    main()
