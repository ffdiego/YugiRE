"""Create a separate Joey EXE granting at least three copies of playable cards.

Only the documented original SHA256 is supported. Never overwrites a file.
Running the result can persist the collection through the game's normal save.
"""

import argparse
import hashlib
import json
from pathlib import Path
import struct


ORIGINAL_SHA256 = "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de"
IMAGE_BASE = 0x400000
HOOK_VA = 0x5BEA80
ORIGINAL_PROLOGUE = bytes.fromhex("51535533db")
# Auditable x86 source in patches/all_cards_x86.s. No assembler needed to use.
STUB_PREFIX = bytes.fromhex(
    "9c6031edbe010000008d3c75cc3ca50056b8e0244000ffd083c404a8077408"
    "803f037303c607030fb60701c54681fe5b0400007cd466892dca3ca500"
    "c605424fa50001619d"
)


def align(value, alignment):
    return (value + alignment - 1) // alignment * alignment


def relative_jump(source, destination):
    return b"\xe9" + struct.pack("<i", destination - source - 5)


def patch_executable(original):
    digest = hashlib.sha256(original).hexdigest()
    if digest != ORIGINAL_SHA256:
        raise ValueError("Unsupported EXE SHA256; provide the unmodified documented joey_pc.exe")
    pe = struct.unpack_from("<I", original, 0x3C)[0]
    if original[pe:pe + 4] != b"PE\0\0":
        raise ValueError("Invalid PE signature")
    optional = pe + 24
    sections = struct.unpack_from("<H", original, pe + 6)[0]
    optional_size = struct.unpack_from("<H", original, pe + 20)[0]
    section_table = optional + optional_size
    new_header = section_table + sections * 40
    file_alignment = struct.unpack_from("<I", original, optional + 36)[0]
    section_alignment = struct.unpack_from("<I", original, optional + 32)[0]
    image_size = struct.unpack_from("<I", original, optional + 56)[0]
    header_size = struct.unpack_from("<I", original, optional + 60)[0]
    if new_header + 40 > header_size or any(original[new_header:new_header + 40]):
        raise ValueError("No unused PE section-header slot")
    hook_offset = HOOK_VA - IMAGE_BASE  # Verified mapping for this fingerprint.
    if original[hook_offset:hook_offset + 5] != ORIGINAL_PROLOGUE:
        raise ValueError("Unexpected hook instructions")
    rva = align(image_size, section_alignment)
    stub_va = IMAGE_BASE + rva
    stub = STUB_PREFIX + ORIGINAL_PROLOGUE
    stub += relative_jump(stub_va + len(stub), HOOK_VA + 5)
    raw_offset = align(len(original), file_alignment)
    raw_size = align(len(stub), file_alignment)
    result = bytearray(original)
    result.extend(bytes(raw_offset + raw_size - len(result)))
    result[raw_offset:raw_offset + len(stub)] = stub
    struct.pack_into("<8sIIIIIIHHI", result, new_header,
                     b".cards\0\0", len(stub), rva, raw_size, raw_offset,
                     0, 0, 0, 0, 0x60000020)  # Code, read, execute.
    struct.pack_into("<H", result, pe + 6, sections + 1)
    old_code_size = struct.unpack_from("<I", result, optional + 4)[0]
    struct.pack_into("<I", result, optional + 4, old_code_size + raw_size)
    struct.pack_into("<I", result, optional + 56, align(rva + len(stub), section_alignment))
    struct.pack_into("<I", result, optional + 64, 0)  # PE checksum unused by this target.
    result[hook_offset:hook_offset + 5] = relative_jump(HOOK_VA, stub_va)
    report = {
        "original_sha256": digest,
        "patched_sha256": hashlib.sha256(result).hexdigest(),
        "hook_va": hex(HOOK_VA), "hook_file_offset": hex(hook_offset),
        "stub_va": hex(stub_va), "stub_file_offset": hex(raw_offset),
        "stub_size": len(stub),
        "behavior": "At deck reconciliation: minimum 3 copies for card_pack & 7; preserve flags and larger quantities; recount copies; mark collection dirty.",
        "runtime_validation": "Pending Windows test. Normal saving may persist granted cards.",
    }
    return bytes(result), report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("original", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    try:
        if args.original.resolve() == args.output.resolve():
            raise ValueError("Input and output must be different files")
        patched, report = patch_executable(args.original.read_bytes())
        with args.output.open("xb") as destination:
            destination.write(patched)
    except (OSError, ValueError) as error:
        parser.exit(1, f"Cannot patch EXE: {error}\n")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
