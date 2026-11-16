import struct
import zlib
import binascii
import sys
import tempfile
import unittest
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from joey_visual import Archive, lzss, yga, png_bgra, strip_frames, scene_records, local_export_dir, bitmap_info


def literal_stream(data):
    return b''.join(bytes([(1 << len(data[i:i+8])) - 1]) + data[i:i+8]
                    for i in range(0, len(data), 8))


class CodecTests(unittest.TestCase):
    def test_literals_and_overlap(self):
        self.assertEqual(lzss(literal_stream(b'0123456789'), 10), b'0123456789')
        # A written at FEE; absolute reference copies A and then its own output.
        self.assertEqual(lzss(b'\x01A\xee\xf2', 6), b'AAAAAA')
        self.assertEqual(lzss(b'\x00\x00\x00', 3), b'\0' * 3)

    def test_ring_wrap(self):
        data = bytes(range(256)) * 17
        self.assertEqual(lzss(literal_stream(data), len(data)), data)

    def test_truncation_and_trailing(self):
        for data, size in [(b'', 1), (b'\1', 1), (b'\0\0', 1), (b'\1AB', 1), (b'', -1), (b'', 2**30)]:
            with self.subTest(data=data, size=size), self.assertRaises(ValueError):
                lzss(data, size)

    def test_yga_geometry_and_payload(self):
        pixels = bytes([4, 5, 6, 128, 7, 8, 9, 255])
        payload = literal_stream(pixels)
        data = b'yga\0' + struct.pack('<5I', 2, 1, 1, 8, len(payload)) + payload
        info, actual = yga(data)
        self.assertEqual((info['width'], info['height'], actual), (2, 1, pixels))
        for bad in [data[:20], b'YGA\0' + data[4:], data[:-1], data + b'\0',
                    data[:4] + struct.pack('<I', 0) + data[8:],
                    data[:12] + struct.pack('<I', 2) + data[16:]]:
            with self.assertRaises(ValueError):
                yga(bad)

    def test_raw_yga(self):
        pixels = b'\x01\x02\x03\x04'
        data = b'yga\0' + struct.pack('<5I', 1, 1, 0, 4, 4) + pixels
        self.assertEqual(yga(data)[1], pixels)
        # derplayer ConvertToYGA emits mode 0 with CompressedSize == 0.
        self.assertEqual(yga(data[:20] + bytes(4) + pixels)[1], pixels)
        with self.assertRaises(ValueError):
            yga(b'yga\0' + struct.pack('<5I', 1, 1, 0, 4, 3) + pixels[:3])

    def test_png_channels_orientation_and_crc(self):
        pixels = bytes([1, 2, 3, 4, 5, 6, 7, 8])
        encoded = png_bgra(1, 2, pixels)
        at, chunks = 8, {}
        while at < len(encoded):
            length, = struct.unpack_from('>I', encoded, at)
            kind = encoded[at+4:at+8]
            payload = encoded[at+8:at+8+length]
            crc, = struct.unpack_from('>I', encoded, at+8+length)
            self.assertEqual(crc, binascii.crc32(kind + payload) & 0xffffffff)
            chunks[kind] = payload
            at += length + 12
        self.assertEqual(struct.unpack('>2I5B', chunks[b'IHDR']), (1, 2, 8, 6, 0, 0, 0))
        self.assertEqual(zlib.decompress(chunks[b'IDAT']), bytes([0, 3, 2, 1, 4, 0, 7, 6, 5, 8]))

    def test_strip_preserves_rows(self):
        pixels = bytes(range(32))
        self.assertEqual(list(strip_frames(4, 2, pixels, 2)),
                         [pixels[:8] + pixels[16:24], pixels[8:16] + pixels[24:]])
        for count in (0, 3, 5):
            with self.assertRaises(ValueError):
                list(strip_frames(4, 2, pixels, count))
        with self.assertRaises(ValueError):
            local_export_dir(Path('/tmp/joey-not-ignored'))

    def test_scene_raw_integer_not_coordinates(self):
        rows = scene_records(b'// ignored\r\nimage.yga\t5004 // comment\nother.bmp\nzero.bmp 0\nnegative.bmp -32\n')
        self.assertEqual([r['parameter'] for r in rows], [5004, None, 0, -32])
        self.assertEqual([r['line'] for r in rows], [2, 3, 4, 5])

    def test_bmp_truncated_pixels(self):
        data = bytearray(58)
        data[:2] = b'BM'
        struct.pack_into('<IIiiHHI', data, 10, 54, 40, 1, 1, 1, 24, 0)
        self.assertEqual(bitmap_info(data)['width'], 1)
        with self.assertRaises(ValueError):
            bitmap_info(data[:-1])

    def test_archive_bounds_and_decode(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / 'test.dat'
            name = b'test.yga'
            encoded = bytes((v >> 4) | ((v & 15) << 4) for v in name).ljust(256, b'\0')
            row = encoded + struct.pack('<III', 280, 4, 5)
            path.write_bytes(b'KCEJYUGI' + struct.pack('<I', 1) + row + literal_stream(b'test'))
            self.assertEqual(Archive(path).read('TEST.YGA'), b'test')
            path.write_bytes(path.read_bytes()[:-1])
            with self.assertRaises(ValueError):
                Archive(path)

if __name__ == '__main__':
    unittest.main()
