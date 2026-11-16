"""Offline visual resource research. No game code is executed; stdlib only."""
from __future__ import annotations
import argparse
from collections import Counter
from dataclasses import dataclass, asdict
import hashlib
import json
from pathlib import Path, PureWindowsPath
import struct
import zlib
import binascii
import csv
import re

LIMIT = 16 * 1024 * 1024


def lzss(data: bytes, size: int) -> bytes:
    """0x0054A000 family, zero ring, LSB flags, absolute 12-bit references."""
    if not 0 <= size <= LIMIT:
        raise ValueError('Invalid decoded size')
    ring = bytearray(4096)
    out = bytearray()
    source, pos, flags = 0, 0xfee, 0
    def take():
        nonlocal source
        if source >= len(data):
            raise ValueError('Truncated LZSS')
        value = data[source]
        source += 1
        return value
    while len(out) < size:
        flags >>= 1
        if not flags & 256:
            flags = take() | 0xff00
        if flags & 1:
            value = take()
            out.append(value)
            ring[pos] = value
            pos = (pos + 1) & 4095
        else:
            a, b = take(), take()
            start = a | ((b & 240) << 4)
            for i in range((b & 15) + 3):
                value = ring[(start + i) & 4095]
                out.append(value)
                ring[pos] = value
                pos = (pos + 1) & 4095
                if len(out) == size:
                    break
    if source != len(data):
        raise ValueError('Trailing compressed bytes')
    return bytes(out)


@dataclass(frozen=True)
class Entry:
    name: str
    offset: int
    size: int
    stored: int


class Archive:
    def __init__(self, path):
        self.path = Path(path)
        self.entries = []
        with self.path.open('rb') as stream:
            header = stream.read(12)
            if len(header) != 12 or header[:8] != b'KCEJYUGI':
                raise ValueError('Invalid DAT header')
            count, = struct.unpack_from('<I', header, 8)
            end = 12 + count * 268
            length = self.path.stat().st_size
            if end > length:
                raise ValueError('Truncated DAT index')
            seen = set()
            for _ in range(count):
                row = stream.read(268)
                name = bytes((v >> 4) | ((v & 15) << 4) for v in row[:256])
                if b'\0' not in name:
                    raise ValueError('Unterminated DAT name')
                name = name.split(b'\0')[0].decode('ascii')
                offset, size, stored = struct.unpack_from('<III', row, 256)
                if offset < end or offset + stored > length or name.lower() in seen:
                    raise ValueError('Invalid DAT entry bounds or duplicate name')
                seen.add(name.lower())
                self.entries.append(Entry(name, offset, size, stored))

    def read(self, entry):
        if isinstance(entry, str):
            entry = next(e for e in self.entries if e.name.lower() == entry.lower())
        if max(entry.size, entry.stored) > LIMIT:
            raise ValueError('Resource exceeds size limit')
        with self.path.open('rb') as stream:
            stream.seek(entry.offset)
            data = stream.read(entry.stored)
        if len(data) != entry.stored:
            raise ValueError('Truncated resource')
        return data if entry.size == entry.stored else lzss(data, entry.size)


def yga(data: bytes):
    if len(data) < 24 or data[:4] != b'yga\0':
        raise ValueError('Invalid YGA header')
    width, height, mode, size, stored = struct.unpack_from('<5I', data, 4)
    if not 0 < width <= 4096 or not 0 < height <= 4096 or size != width * height * 4:
        raise ValueError('Invalid YGA geometry')
    if mode not in (0, 1):
        raise ValueError('Unsupported YGA mode')
    if mode == 1 and stored != len(data) - 24:
        raise ValueError('Invalid YGA payload size')
    if mode == 0 and stored not in (0, size):
        raise ValueError('Invalid raw YGA size field')
    if size > LIMIT:
        raise ValueError('YGA exceeds size limit')
    pixels = lzss(data[24:], size) if mode else data[24:]
    if len(pixels) != size:
        raise ValueError('Invalid raw YGA size')
    return dict(width=width, height=height, mode=mode, decoded_size=size,
                compressed_size=stored, pixel_sha256=hashlib.sha256(pixels).hexdigest()), pixels


def png_bgra(width, height, pixels):
    """Export top-down Windows B,G,R,A pixels without altering their alpha."""
    if width <= 0 or height <= 0 or len(pixels) != width * height * 4 or len(pixels) > LIMIT:
        raise ValueError('Invalid PNG geometry')
    scanlines = bytearray()
    for y in range(height):
        scanlines.append(0)  # PNG filter: none
        for x in range(width):
            b, g, r, a = pixels[(y * width + x) * 4: (y * width + x + 1) * 4]
            scanlines.extend((r, g, b, a))
    def chunk(kind, data):
        return (struct.pack('>I', len(data)) + kind + data +
                struct.pack('>I', binascii.crc32(kind + data) & 0xffffffff))
    return (b'\x89PNG\r\n\x1a\n' +
            chunk(b'IHDR', struct.pack('>2I5B', width, height, 8, 6, 0, 0, 0)) +
            chunk(b'IDAT', zlib.compress(scanlines)) + chunk(b'IEND', b''))


def strip_frames(width, height, pixels, count):
    """Explicit caller-supplied horizontal divisions, not inferred YGA frames."""
    if count < 1 or count > width or width % count or len(pixels) != width * height * 4:
        raise ValueError('Invalid horizontal strip')
    frame_width = width // count
    for frame in range(count):
        yield b''.join(pixels[(y * width + frame * frame_width) * 4:
                             (y * width + (frame + 1) * frame_width) * 4]
                       for y in range(height))


def local_export_dir(path):
    # Extracted assets may only be written inside this checkout's ignored scratch.
    root = Path(__file__).resolve().parents[2] / 'scratch'
    path = path.resolve()
    if not path.is_relative_to(root.resolve()):
        raise ValueError('Image exports must be inside the ignored repository scratch/ directory')
    path.mkdir(parents=True, exist_ok=True)
    return path


def sha256_file(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def bitmap_info(data):
    if len(data) < 54 or data[:2] != b'BM':
        raise ValueError('Invalid BMP header')
    offset, header = struct.unpack_from('<II', data, 10)
    width, height, planes, bits, compression = struct.unpack_from('<iiHHI', data, 18)
    if (header < 40 or 14 + header > len(data) or offset < 14 + header or
            not 0 < width <= 4096 or not 0 < abs(height) <= 4096 or
            planes != 1 or bits not in (1, 8, 24) or compression != 0):
        raise ValueError('Unsupported BMP geometry/layout')
    stride = ((width * bits + 31) // 32) * 4
    if offset + stride * abs(height) > len(data):
        raise ValueError('Truncated BMP pixels')
    return dict(width=width, height=height, planes=planes, bits=bits, compression=compression)


def scene_records(data):
    """First token and optional signed integer; interpretation belongs to consumer.

    cp932 is supported by observed Japanese comments, not asserted for every DAT text.
    Never feed binary tables or card description files to this helper.
    """
    rows = []
    for line_number, line in enumerate(data.decode('cp932').splitlines(), 1):
        text = line.split('//', 1)[0].strip()
        if not text:
            continue
        tokens = text.split()
        parameter = None
        if len(tokens) > 1:
            match = re.match(r'[+-]?[0-9]+', tokens[1])
            if match:
                parameter = int(match[0])
                if not -(2**31) <= parameter < 2**31:
                    raise ValueError('Scene integer outside signed 32-bit range')
        rows.append(dict(line=line_number, resource=tokens[0], parameter=parameter))
    return rows


def purpose(entry):
    path = PureWindowsPath(entry.name.lower())
    ext = path.suffix
    if ext == '.sue':
        return 'SUECAT thumbnail catalog candidate; runtime use unknown', 'low'
    if ext == '.ydc':
        return 'deck data', 'high'
    if ext == '.bin':
        return 'binary tables; see existing file-format research', 'medium'
    if ext == '.txt':
        return 'resource/scene manifest candidate', 'medium'
    if path.parts[0] in ('card', 'mini'):
        return 'card artwork' if path.parts[0] == 'card' else 'card miniature', 'high'
    if path.parts[0] == 'cursor':
        return 'cursor/interface sprite', 'high'
    parent = str(path.parent).replace('\\', '/')
    labels = {'effect': 'duel effect sprite', 'field': 'duel field/background',
              'phase_name': 'phase/turn transition sprite', 'help': 'duel help/interface',
              'dialog': 'dialog interface', 'get': 'card reward interface',
              'exodia': 'Exodia presentation', 'chain': 'chain presentation',
              'bust_up': 'character presentation', 'title': 'title/menu interface',
              'life': 'life point presentation', 'match': 'match result presentation'}
    label = next((label for part, label in labels.items() if part in path.parts), 'visual asset')
    return f'{label} ({parent})', 'medium'


def write_csv(result, path):
    fields = ['name', 'extension', 'offset', 'size', 'stored', 'signature_hex',
              'width', 'height', 'format', 'probable_purpose', 'purpose_confidence']
    with Path(path).open('w', newline='') as stream:
        writer = csv.DictWriter(stream, fieldnames=fields, lineterminator='\n')
        writer.writeheader()
        for row in result['resources']:
            item = {key: row[key] for key in fields if key in row}
            info = row.get('bitmap', row.get('yga', {}))
            item.update(width=info.get('width'), height=info.get('height'))
            item['format'] = (f"BMP {info['bits']}bpp BI_RGB" if 'bitmap' in row else
                              ('YGA 32bpp LZSS' if info['mode'] else 'YGA 32bpp raw') if 'yga' in row else row['extension'][1:])
            writer.writerow(item)


def inventory(archive, decode_yga=False):
    rows = []
    for entry in archive.entries:
        row = asdict(entry)
        ext = PureWindowsPath(entry.name).suffix.lower()
        row['extension'] = ext
        row['probable_purpose'], row['purpose_confidence'] = purpose(entry)
        data = archive.read(entry)
        row['signature_hex'] = data[:4].hex()
        if ext == '.yga':
            if decode_yga:
                row['yga'], _ = yga(data)
            else:
                row['header_words'] = list(struct.unpack_from('<5I', data, 4))
        elif ext == '.bmp':
            row['bitmap'] = bitmap_info(data)
        rows.append(row)
    return dict(dat_sha256=sha256_file(archive.path), count=len(rows),
                extensions=dict(Counter(r['extension'] for r in rows)), resources=rows)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('dat', type=Path)
    sub = parser.add_subparsers(dest='command', required=True)
    inv = sub.add_parser('inventory')
    inv.add_argument('--csv', type=Path, help='Metadata only; no payloads')
    inv.add_argument('--decode-yga', action='store_true')
    inv.add_argument('--output', type=Path, required=True)
    scene = sub.add_parser('scene')
    scene.add_argument('resource')
    scene.add_argument('--output', type=Path, required=True)
    export = sub.add_parser('export-yga')
    export.add_argument('resource', help='Exact DAT resource name')
    export.add_argument('--output-dir', type=Path, required=True)
    export.add_argument('--frames', type=int, default=1, help='Explicit horizontal division count')
    args = parser.parse_args()
    if args.command == 'inventory' and args.csv and not args.decode_yga:
        parser.error('--csv requires --decode-yga for validated YGA dimensions')
    archive = Archive(args.dat)
    if args.command == 'scene':
        args.output.write_text(json.dumps(scene_records(archive.read(args.resource)), indent=2) + '\n')
        return
    if args.command == 'export-yga':
        info, pixels = yga(archive.read(args.resource))
        width, height = info['width'], info['height']
        frames = list(strip_frames(width, height, pixels, args.frames))
        output = local_export_dir(args.output_dir)
        for index, frame in enumerate(frames):
            path = output / f'frame-{index:03}.png'
            # Refuse overwrites, including links to paths outside scratch.
            with path.open('xb') as stream:
                stream.write(png_bgra(width // args.frames, height, frame))
        print(json.dumps(dict(resource=args.resource, frames=args.frames, **info)))
        return
    result = inventory(archive, args.decode_yga)
    args.output.write_text(json.dumps(result, indent=2) + '\n')
    if args.csv:
        write_csv(result, args.csv)
    print(json.dumps({k:v for k,v in result.items() if k != 'resources'}))

if __name__ == '__main__':
    main()
