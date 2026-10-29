"""Check request key packing against static bytes and the complete byte domain."""

import hashlib
import json
from pathlib import Path

from validate_parasite_creation import main as validate_creation


def main():
    validate_creation()
    repository = Path(__file__).resolve().parent.parent
    binary = (repository.parent / "passion/joey_pc.exe").read_bytes()
    checks = []
    for va, expected in [
        (0x005BA408, "8a442424"),
        (0x005BA423, "c1e00581e11fe000000bc166894604"),
        (0x005BA4B3, "e858feffff"),
        (0x005BB742, "8db02c5ba500"),
        (0x0057895D, "c1e918c1e80c83e17f83e0018d1448"),
    ]:
        offset = va - 0x00400000  # Documented .text: raw offset = RVA.
        actual = binary[offset:offset + len(bytes.fromhex(expected))].hex()
        if actual != expected:
            raise ValueError(f"Instruction mismatch at {va:#x}: {actual}")
        checks.append(dict(va=hex(va), file_offset=hex(offset), bytes=actual))

    cases = 0
    for key in range(256):
        word = 197 | ((key & 1) << 12) | ((key >> 1) << 24)
        lookup_key = ((word >> 12) & 1) + 2 * ((word >> 24) & 127)
        for upper in range(8):
            for lower in range(32):
                preserved = (upper << 13) | lower
                request_word = (key << 5) | preserved
                recovered = (request_word >> 5) & 255
                metadata = 13 | (recovered << 8)
                if recovered != lookup_key or metadata >> 8 != lookup_key:
                    raise ValueError("Request/lookup key mismatch")
                if (metadata & 15) != 13 or request_word & 0xE01F != preserved:
                    raise ValueError("Unrelated fields changed")
                cases += 1

    def packed_word_2(code):
        fields = (((code >> 24) & 1) << 2) | ((code >> 21) & 3)
        fields = (fields << 6) | ((code >> 25) & 63)
        fields = (fields << 5) | ((code >> 16) & 31)
        return ((fields << 1) | ((code >> 31) & 1)) & 0xFFFF

    branch_examples = []
    for side in range(2):
        code = 0x365F0000 | (side << 31) | 197
        flags = packed_word_2(code)
        if flags & 0x3000 != 0x2000:
            raise ValueError("Unexpected 0x365F callback branch")
        branch_examples.append(dict(side=side, code=hex(code), request_word_2=hex(flags),
                                    type_13_creation_branch=False))
    report = dict(
        target_sha256=hashlib.sha256(binary).hexdigest(), instruction_checks=checks,
        key_roundtrips=cases, key_domain=256,
        preserved_patterns_per_key=256, request_stride=20,
        pending_base="0xa558c0", active_base="0xa55b40",
        callback_request="0xa55b40 + (active_count - 1) * 20",
        branch_examples=branch_examples,
        method="Static bytes and exhaustive finite-domain arithmetic; no game execution.",
        limitation="Encoding equality proven; initial creation event and complete callback branches unresolved.",
    )
    (repository / "docs/evidence/parasite-request-validation.json").write_text(
        json.dumps(report, indent=2) + "\n"
    )
    print(f"PASS: request packing, {cases} key roundtrips and 0x365F branch distinction")


if __name__ == "__main__":
    main()
