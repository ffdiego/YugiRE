"""Check selected scheduler anchors on the pinned executable without running it."""
from hashlib import sha256
from pathlib import Path
from struct import unpack_from

ROOT = Path(__file__).resolve().parent.parent
EXE_HASH = "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de"
BASE = 0x400000


def main():
    data = (ROOT.parent / "passion/joey_pc.exe").read_bytes()
    if sha256(data).hexdigest() != EXE_HASH:
        raise ValueError("Unexpected executable")
    pe, = unpack_from("<I", data, 0x3C)
    section_count, = unpack_from("<H", data, pe + 6)
    optional_bytes, = unpack_from("<H", data, pe + 20)
    header = pe + 24 + optional_bytes
    sections = [unpack_from("<IIII", data, header + index * 40 + 8)
                for index in range(section_count)]

    def offset(va):
        for _, rva, size, raw in sections:
            if rva <= va - BASE < rva + size:
                return raw + va - BASE - rva
        raise ValueError(f"VA {va:#x} has no file data")

    def expect(va, code):
        pos = offset(va)
        if data[pos:pos + len(code)] != code:
            raise ValueError(f"Unexpected instruction at {va:#x}")

    def transfer(site, target, opcode=0xE8):
        pos = offset(site)
        if data[pos] != opcode or site + 5 + unpack_from("<i", data, pos + 1)[0] != target:
            raise ValueError(f"Unexpected call/jump at {site:#x}")

    calls = {
        0x0044DD2F: 0x005C0000, 0x005C0000: 0x005BEEE0,
        0x0044DD42: 0x0045D040, 0x0044DD5D: 0x0045D170,
        0x005BC740: 0x005BDC40, 0x005BC749: 0x005B9CA0,
        0x005B9DB3: 0x005B9470, 0x005B92C4: 0x00449D50,
        0x005BC5A6: 0x005B9370, 0x00405AF6: 0x0040CDB0,
        0x0040D333: 0x0041D160, 0x00406DE6: 0x00450260,
    }
    for site, target in calls.items():
        transfer(site, target)
    transfer(0x005C0005, 0x005BC700, 0xE9)
    transfer(0x005BDC40, 0x0040CF20, 0xE9)

    expect(0x005DB1F8, bytes.fromhex("00 DD 44 00"))
    expect(0x005DA86C, bytes.fromhex("B0 88 41 00 60 8B 41 00"))
    expect(0x005B9CE6, bytes.fromhex("6A 08 68 A8 50 A5 00 68 A0 50 A5 00"))
    expect(0x005B9CFA, bytes.fromhex("66 FF 0D A8 58 A5 00"))
    expect(0x005B9D0E, bytes.fromhex("8D 46 08 6A 08 50 56"))
    expect(0x004188B0, bytes.fromhex("81 79 14 00 01 00 00 75 0A 8B 41 18 85 C0"))
    expect(0x005BC78A, bytes.fromhex("66 81 25 FE 77 A5 00 FF 00"))

    dispatch = {
        0x1E: 0x0040AA40, 0x30: 0x00570DD0, 0x31: 0x00407A20,
        0x3C: 0x00405020, 0x57: 0x00407A90, 0x72: 0x00406B20,
        0x79: 0x00404B40, 0x7C: 0x00405190, 0x80: 0x00407E60,
        0xCE: 0x004059C0, 0xCF: 0x00405CB0,
    }
    for opcode, target in dispatch.items():
        index = data[offset(0x005B9AEC + opcode - 1)]
        stub, = unpack_from("<I", data, offset(0x005B9838 + index * 4))
        transfer(stub, target, 0xE9)
    index = data[offset(0x005B9AEC + 0x43 - 1)]
    lp_stub, = unpack_from("<I", data, offset(0x005B9838 + index * 4))
    expect(lp_stub, b"\x6a\x00")
    transfer(lp_stub + 2, 0x00408470)

    print("PASS: scene calls, queue anchors, visual predicate and 12 opcode dispatches")


if __name__ == "__main__":
    main()
