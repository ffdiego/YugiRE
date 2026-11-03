"""Offline tests for registry-aware Joey replay discovery."""
from pathlib import Path
import os
import sys
import tempfile
import unittest
import struct
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
import joeytrace_replay as discovery
import joeytrace
from joeytrace_core import TraceSession


class FakeKey:
    def __init__(self, data):
        self.data = data
    def __enter__(self):
        return self
    def __exit__(self, *args):
        pass


class FakeRegistry:
    HKEY_LOCAL_MACHINE = "HKLM"
    HKEY_CURRENT_USER = "HKCU"
    KEY_READ = 0x1
    KEY_WOW64_32KEY = 0x200
    KEY_WOW64_64KEY = 0x100
    REG_SZ = 1
    REG_EXPAND_SZ = 2

    def __init__(self, db):
        self.db = db
        self.accesses = []

    def OpenKey(self, hive, key, unused, access):
        self.accesses.append((hive, key, access))
        try:
            return FakeKey(self.db[(hive, key, access)])
        except KeyError:
            raise FileNotFoundError("key not present")

    def QueryValueEx(self, handle, name):
        try:
            return handle.data[name]
        except KeyError:
            raise FileNotFoundError("value not present")


class DiscoveryTests(unittest.TestCase):
    def test_32bit_registry_view_and_virtualstore_without_double_wow6432node(self):
        with tempfile.TemporaryDirectory() as root:
            k = discovery._GAME_SYSTEM_KEY
            reg = FakeRegistry({
                ("HKLM", k, 0x201): {"CommonDir": (root, 1)},
                ("HKCU", discovery._VIRTUAL_BASE + r"\WOW6432Node\KONAMI\Yu-Gi-Oh! Power Of Chaos\system", 0x1):
                    {"ReplayDir": (root, 1)},
            })
            locations = discovery.discover_registry_locations(reg)
            self.assertEqual(len(locations), 2)
            self.assertEqual(locations[0].value_name, "CommonDir")
            self.assertEqual(locations[1].value_name, "ReplayDir")
            self.assertEqual(reg.accesses[0], ("HKLM", k, 0x201))
            self.assertNotIn("WOW6432Node", reg.accesses[0][1])
            self.assertIn("WOW6432Node", reg.accesses[1][1])

    def test_latest_file_chosen_by_mtime_and_duplicate_folders_deduplicated(self):
        with tempfile.TemporaryDirectory() as root:
            folder = Path(root)
            a = folder / "one.YDR"
            b = folder / "two.ydr"
            a.write_bytes(b"YGR\x00")
            b.write_bytes(b"YGR\x00")
            os.utime(a, ns=(10_000_000_000, 10_000_000_000))
            os.utime(b, ns=(20_000_000_000, 20_000_000_000))
            locations = [discovery.ReplayLocation(folder, "HKLM32", "CommonDir", True),
                         discovery.ReplayLocation(folder, "HKLM64", "CommonDir", True)]
            found = discovery.find_replays(locations)
            self.assertEqual([x.path for x in found], [b, a])

    def test_missing_locations_are_ignored(self):
        reg = FakeRegistry({})
        self.assertEqual(discovery.discover_registry_locations(reg), [])
        self.assertEqual(discovery.find_replays([]), [])

    def test_select_suggested_or_custom_file(self):
        with tempfile.TemporaryDirectory() as tmp:
            file = Path(tmp) / "replay.ydr"
            file.write_bytes(b"YGR\x00")
            loc = [discovery.ReplayLocation(Path(tmp), "HKLM32", "CommonDir", True)]
            with patch.object(discovery, "discover_registry_locations", return_value=loc), \
                 patch.object(discovery.sys.stdin, "isatty", return_value=True):
                self.assertEqual(discovery.choose_replay(input_fn=lambda _: ""), file)
                self.assertEqual(discovery.choose_replay(input_fn=lambda _: str(file)), file)
                self.assertIsNone(discovery.choose_replay(input_fn=lambda _: "0"))

    def test_import_replay_interactively_with_no_file_argument(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            trace = TraceSession(folder / "session", {})
            trace.finish("test_complete", 0)
            replay = folder / "sample.ydr"
            data = bytearray(struct.pack("<4s8s4I", b"YGR\0", b"ABCDEFGH", 1300, 1, 1, 12345))
            for group in ((730,), (), (), (1123,), (), ()):
                data += struct.pack("<H", len(group))
                for card in group:
                    data += struct.pack("<H", card)
            data += struct.pack("<I", 1) + struct.pack("<4H", 0x8001, 0, 0, 0)
            replay.write_bytes(data)
            with patch.object(joeytrace, "choose_replay", return_value=replay):
                self.assertEqual(joeytrace.main([
                    "import-replay", "--session", str(trace.folder)]), 0)
            self.assertEqual((trace.folder / "replay.ydr").read_bytes(), data)
            self.assertTrue((trace.folder / "replay-summary.json").is_file())

    def test_list_replays_command(self):
        with patch.object(joeytrace, "list_replays", return_value=[]) as listed:
            self.assertEqual(joeytrace.main(["list-replays", "--limit", "3"]), 0)
            listed.assert_called_once_with(3)

    def test_noninteractive_requires_explicit_file(self):
        with patch.object(discovery.sys.stdin, "isatty", return_value=False):
            with self.assertRaisesRegex(ValueError, "--file"):
                discovery.choose_replay()


if __name__ == "__main__":
    unittest.main()
