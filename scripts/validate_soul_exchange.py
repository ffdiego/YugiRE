"""Fingerprint and selected native checks for Soul Exchange; no game execution."""
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
    row = struct.unpack_from('<6I', data, 0x1eeda0)
    if row != (1220, 0x58e160, 0x579c40, 0x582080, 0, 0x5944d0):
        raise ValueError('Unexpected Soul Exchange callback record')
    sequences = {
        0x58e196: '6a0183e0016a12',  # value=1, actor side, bit 18
        0x58e1cd: '83ca03',          # metadata kind 3
        0x56da56: '83f803740a83f80a7e1383f80d7f0e',  # kinds 3 / 11..13
        0x5a0dfa: 'f7863064a50000000400',  # player bit 0x40000 test
        0x582087: '240e3c04',        # availability phase bits
    }
    for va, expected in sequences.items():
        raw = bytes.fromhex(expected)
        if data[va - 0x400000:va - 0x400000 + len(raw)] != raw:
            raise ValueError(f'Unexpected instructions at {va:#x}')
    calls = {}
    for site, expected in [(0x58e19e, 0x579930), (0x58e1e0, 0x5747e0), (0x56a1ac, 0x56da10), (0x481a4e, 0x5a0dd0), (0x5b0a81, 0x5a0dd0)]:
        offset = site - 0x400000
        if data[offset] != 0xe8 or site + 5 + struct.unpack_from('<i', data, offset + 1)[0] != expected:
            raise ValueError(f'Unexpected call at {site:#x}')
        calls[hex(site)] = hex(expected)
    report = dict(sha256=digest, resource_id=1220, callback_row=[hex(x) for x in row],
                  instruction_sites=[hex(x) for x in sequences], calls=calls,
                  limitation='Static checks only. Does not validate timing, expiration or every target eligibility condition.')
    (REPO / 'docs/evidence/soul-exchange-validation.json').write_text(json.dumps(report, indent=2) + '\n')
    print('PASS: Soul Exchange record, five instruction sequences and five call targets')

if __name__ == '__main__':
    main()
