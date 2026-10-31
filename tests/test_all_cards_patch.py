import importlib.util
from pathlib import Path
import shutil
import struct
import subprocess
import tempfile
import unittest


REPO = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("all_cards_patch", REPO / "scripts/patch_all_cards.py")
PATCH = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PATCH)


class AllCardsPatchTests(unittest.TestCase):
    def test_rejects_unrecognized_binary(self):
        with self.assertRaisesRegex(ValueError, "SHA256"):
            PATCH.patch_executable(b"Not the supported executable")

    def test_jump_destination(self):
        instruction = PATCH.relative_jump(0x5BEA80, 0xA5E000)
        self.assertEqual(instruction[0], 0xE9)
        self.assertEqual(0x5BEA85 + struct.unpack_from("<i", instruction, 1)[0], 0xA5E000)

    @unittest.skipUnless(shutil.which("as") and shutil.which("objcopy"), "GNU binutils unavailable")
    def test_embedded_code_matches_assembly(self):
        with tempfile.TemporaryDirectory() as directory:
            obj = Path(directory) / "stub.o"
            raw = Path(directory) / "stub.bin"
            subprocess.run(["as", "--32", str(REPO / "scripts/patches/all_cards_x86.s"),
                            "-o", str(obj)], check=True, capture_output=True)
            subprocess.run(["objcopy", "-O", "binary", "-j", ".text", str(obj), str(raw)],
                           check=True, capture_output=True)
            self.assertEqual(raw.read_bytes(), PATCH.STUB_PREFIX)

    @unittest.skipUnless((REPO.parent / "passion/joey_pc.exe").is_file(), "Original not available")
    def test_pinned_pe_patch_preserves_existing_sections(self):
        original = (REPO.parent / "passion/joey_pc.exe").read_bytes()
        patched, report = PATCH.patch_executable(original)
        pe = struct.unpack_from("<I", original, 0x3C)[0]
        self.assertEqual(struct.unpack_from("<H", patched, pe + 6)[0], 5)
        header = pe + 24 + struct.unpack_from("<H", original, pe + 20)[0] + 4 * 40
        name, size, rva, raw_size, raw = struct.unpack_from("<8sIIII", patched, header)
        self.assertEqual(name.rstrip(b"\0"), b".cards")
        self.assertEqual(rva + PATCH.IMAGE_BASE, int(report["stub_va"], 16))
        self.assertEqual(raw % 4096, 0)
        self.assertEqual(raw_size % 4096, 0)
        self.assertEqual(patched[raw:raw + len(PATCH.STUB_PREFIX)], PATCH.STUB_PREFIX)
        tail = raw + len(PATCH.STUB_PREFIX)
        self.assertEqual(patched[tail:tail + 5], PATCH.ORIGINAL_PROLOGUE)
        jump_va = rva + PATCH.IMAGE_BASE + size - 5
        self.assertEqual(jump_va + 5 + struct.unpack_from("<i", patched, raw + size - 4)[0],
                         PATCH.HOOK_VA + 5)
        offset = PATCH.HOOK_VA - PATCH.IMAGE_BASE
        # Every existing section byte survives except the five-byte hook.
        self.assertEqual(patched[4096:offset], original[4096:offset])
        self.assertEqual(patched[offset + 5:len(original)], original[offset + 5:])


if __name__ == "__main__":
    unittest.main()
