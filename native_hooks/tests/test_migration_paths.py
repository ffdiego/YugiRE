"""Offline checks of the renamed backend and legacy entrypoints; no Frida needed."""
from pathlib import Path
import subprocess
import sys
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
HOOKS = ROOT / "native_hooks"
sys.path.insert(0, str(HOOKS))


class MigrationPathTests(unittest.TestCase):
    def test_legacy_module_and_assets_resolve_from_new_backend(self):
        import native_rules
        import patch_dual_rules
        import yugilab
        self.assertEqual(yugilab.BACKEND_DIR.resolve(), HOOKS)
        self.assertEqual(Path(native_rules.__file__).resolve(), HOOKS / "native_rules.py")
        self.assertEqual(patch_dual_rules.HERE, HOOKS)
        for file in ("yugire_dual_loader.js", "deck_trace.js", "yugire_rules.def", "decks/demo-A.json"):
            self.assertTrue((native_rules.HERE / file).is_file(), file)
        self.assertEqual(patch_dual_rules.DEFAULT_DLL, HOOKS / "build-vs2026/Release/yugire_rules.dll")

    def test_dotnet_deck_fixtures_have_real_sources(self):
        project = ROOT / "src/tests/Joey.Tests/Joey.Tests.csproj"
        node = next(n for n in ET.parse(project).iter("None") if "demo-?" in n.get("Include", ""))
        pattern = node.attrib["Include"].replace("\\", "/")
        matches = sorted((project.parent / pattern).parent.glob("demo-?.json"))
        self.assertEqual(len(matches), 3)
        self.assertTrue(all(p.resolve().parent == HOOKS / "decks" for p in matches))

    def test_help_entrypoints_from_another_directory(self):
        # A script launched by absolute path must locate sibling support modules.
        result = subprocess.run([sys.executable, str(HOOKS / "native_rules.py"), "--help"],
                                cwd=ROOT.parent, text=True, capture_output=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("--observe-only", result.stdout)
        result = subprocess.run([sys.executable, "-m", "yugilab", "--help"],
                                cwd=ROOT, text=True, capture_output=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("--workers", result.stdout)


if __name__ == "__main__":
    unittest.main()
