"""Verify selected continuation instructions without executing the target."""

import hashlib
import json
from pathlib import Path

from validate_parasite_request import main as validate_request


def main():
    validate_request()
    repository = Path(__file__).resolve().parent.parent
    binary = (repository.parent / "passion/joey_pc.exe").read_bytes()
    checks = []
    for va, expected in [
        (0x0058767A, "83e97e0f8412010000"),
        (0x005876A3, "e88814feff"),
        (0x005876E7, "66b87f00"),
        (0x0058771B, "e8f01bffff"),
        (0x0058778D, "66b87e00"),
        (0x005877A6, "83ca0d"),
        (0x005877C2, "68e8030000"),
        (0x005877CB, "e8100dffff"),
    ]:
        offset = va - 0x00400000  # Fingerprinted .text: file offset = RVA.
        actual = binary[offset:offset + len(bytes.fromhex(expected))].hex()
        if actual != expected:
            raise ValueError(f"Instruction mismatch at {va:#x}")
        checks.append(dict(va=hex(va), file_offset=hex(offset), bytes=actual))
    report = dict(
        target_sha256=hashlib.sha256(binary).hexdigest(), instruction_checks=checks,
        states={"0x80": "resolve key and choose path", "0x7f": "prepare placement",
                "0x7e": "append position metadata and emit command 0x43"},
        observed_value=1000,
        limitation="Command 0x43 consumer and placement completion not traced; no runtime execution.",
    )
    (repository / "docs/evidence/parasite-continuation-validation.json").write_text(
        json.dumps(report, indent=2) + "\n"
    )
    print("PASS: eight continuation instruction sequences")


if __name__ == "__main__":
    main()
