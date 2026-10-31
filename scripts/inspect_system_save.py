"""Read the pinned Joey system.dat layout without modifying the save or registry.

Native codec: VA 00439C00/00439D80/00439EF0; image: 005BE0C0.
See docs/27-save-and-collection.md for target fingerprint and limitations.
"""

import argparse
import hashlib
import json
from pathlib import Path
import struct


IMAGE_SIZE = 0x1188
HEADER_SIZE = 8
LANE_SEEDS = (0x7D, 0x13, 0x8A, 0xBB)


def decode_block(data, decoded_size):
    """Decode nine-bit modular symbols, checking the native checksum span."""
    expected_size = 4 + (decoded_size * 9 + 7) // 8
    if len(data) != expected_size:
        raise ValueError(f"Expected {expected_size} encoded bytes, got {len(data)}")
    # The native checksum ends at decoded_size, not at the encoded file size.
    for lane, seed in enumerate(LANE_SEEDS):
        if (data[lane] + seed + sum(data[4 + lane:decoded_size:4])) & 0xFF:
            raise ValueError("Encoded block checksum mismatch")
    result = bytearray()
    for offset in range(4, len(data), 9):
        packed = int.from_bytes(data[offset:offset + 9], "little")
        for slot in range(min(8, decoded_size - len(result))):
            value = pow((packed >> (slot * 9)) & 0x1FF, 53, 299)
            if value > 255:
                raise ValueError("Decoded symbol is outside the byte domain")
            result.append(value)
    return bytes(result)


def inspect_save(data):
    decoded = decode_block(data, IMAGE_SIZE + HEADER_SIZE)
    header, image = decoded[:HEADER_SIZE], decoded[HEADER_SIZE:]
    if header[0] != 1:
        raise ValueError("Unsupported save header version")
    if image[0x117A:0x1182] != b"YUGIOH01":
        raise ValueError("Save signature mismatch")
    if struct.unpack_from("<H", image, 0x1182)[0] != 0xFBA5:
        raise ValueError("Save image marker mismatch")
    if sum(struct.unpack("<2243H", image[:0x1186])) & 0xFFFF:
        raise ValueError("Save image checksum mismatch")
    entries = struct.unpack_from("<1115H", image, 12)
    owned = [
        {"card_index": index, "copies": word & 0xFF,
         "flags_hex": f"{word & 0xFF00:04X}"}
        for index, word in enumerate(entries) if index and word & 0xFF
    ]
    return {
        "sha256": hashlib.sha256(data).hexdigest(),
        "encoded_size": len(data),
        "decoded_size": len(decoded),
        "header_hex": header.hex(),
        "header_identifier_hex": header[4:8].hex(),
        "encoded_checksum_valid": True,
        "image_checksum_valid": True,
        "owned_types_without_pack_filter": len(owned),
        "owned_copies": sum(card["copies"] for card in owned),
        "stored_copy_counter": struct.unpack_from("<H", image, 10)[0],
        "owned_cards": owned,
        "limitations": [
            "Counts omit edition filtering; indices are not resource IDs.",
            "Registry flcrc was not supplied, so its header match is unverified.",
            "Options and other image fields have not been interpreted.",
        ],
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("save", type=Path)
    args = parser.parse_args()
    try:
        report = inspect_save(args.save.read_bytes())
    except (OSError, ValueError) as error:
        parser.exit(1, f"Cannot inspect save: {error}\n")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
