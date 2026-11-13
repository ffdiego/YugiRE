"""Run offline: python -m unittest native_hooks/tests/test_esi_fixture_harness.py"""
import importlib.util
from pathlib import Path
import tempfile
import unittest

HERE = Path(__file__).resolve().parents[1] / 'experiments' / 'esi_stack_probe'
SPEC = importlib.util.spec_from_file_location('esi_fixture_runner', HERE / 'run_fixture.py')
runner = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(runner)


class FixtureGuardTests(unittest.TestCase):
    def write_pe(self, root, machine=0x14c, name='esi_fixture_x86.exe', include_magic=True):
        exe = Path(root) / name
        contents = bytearray(512)
        contents[:2] = b'MZ'
        contents[0x3c:0x40] = (0x80).to_bytes(4, 'little')
        contents[0x80:0x84] = b'PE\0\0'
        contents[0x84:0x86] = machine.to_bytes(2, 'little')
        if include_magic:
            contents[0xc0:0xc0 + len(runner.MAGIC)] = runner.MAGIC.encode()
        exe.write_bytes(contents)
        return exe

    def test_accepted_x86_fixture_has_expected_name_magic_and_machine(self):
        with tempfile.TemporaryDirectory() as directory:
            exe = self.write_pe(directory)
            self.assertEqual(runner.check_fixture_exe(exe), exe.resolve())
            self.assertEqual(len(runner.sha256_file(exe)), 64)

    def test_refuses_wrong_name_even_if_pe_matches(self):
        with tempfile.TemporaryDirectory() as directory:
            exe = self.write_pe(directory, name='joey_pc.exe')
            with self.assertRaisesRegex(ValueError, 'Refusing any executable'):
                runner.check_fixture_exe(exe)

    def test_refuses_64bit_and_unmarked_exe(self):
        with tempfile.TemporaryDirectory() as directory:
            exe = self.write_pe(directory, machine=0x8664)
            with self.assertRaisesRegex(ValueError, 'x86 PE'):
                runner.check_fixture_exe(exe)
            exe = self.write_pe(directory, include_magic=False)
            with self.assertRaisesRegex(ValueError, 'Missing embedded synthetic fixture'):
                runner.check_fixture_exe(exe)

    def test_refuses_corrupt_pe(self):
        with tempfile.TemporaryDirectory() as directory:
            exe = self.write_pe(directory)
            exe.write_bytes(b'MZ')
            with self.assertRaisesRegex(ValueError, 'Expected an MZ'):
                runner.check_fixture_exe(exe)

    def test_ready_requires_pid_and_selected_al(self):
        self.assertEqual(runner.parse_ready(f'{runner.MAGIC} READY pid=123 probe=0x005A1234 al=2',123,2),'0x005A1234')
        with self.assertRaisesRegex(ValueError,'PID/AL mismatch'):
            runner.parse_ready(f'{runner.MAGIC} READY pid=123 probe=0x005A1234 al=0',123,2)

    def test_result_both_branches_and_esi_corruption(self):
        for al, route in [(0, 1), (2, 0)]:
            good = runner.parse_result(f'{runner.MAGIC} RESULT al={al} esi=0x00000D44 route={route} esp=0x00123456 healthy=1',al)
            self.assertEqual(runner.classify('probe',0,good),'pass')
            bad = runner.parse_result(f'{runner.MAGIC} RESULT al={al} esi=0x0871120F route={route} esp=0x00123456 healthy=0',al)
            self.assertEqual(runner.classify('object',20,bad),'object_overwrote_saved_esi')
            self.assertEqual(runner.classify('probe',20,bad),'fail')
            with self.assertRaisesRegex(ValueError, 'health verdict'):
                runner.parse_result(f'{runner.MAGIC} RESULT al={al} esi=0x00000D44 route={route} esp=0x00123456 healthy=0',al)

    def test_no_result_is_inconclusive(self):
        self.assertEqual(runner.classify('object',-1,None),'inconclusive_crash_or_missing_result')

    def test_source_keeps_separate_overloads_and_exact_window(self):
        agent = (HERE / 'agent.js').read_text()
        fixture = (HERE / 'esi_fixture_x86.c').read_text()
        self.assertIn('Interceptor.attach(target, observeInstruction)', agent)
        self.assertIn('Interceptor.attach(target, {onEnter: observeInstruction})', agent)
        self.assertIn('{0xA8, 0x02, 0x75, 0x1B, 0x0C, 0x02}', fixture)
        self.assertEqual(fixture.count('_emit 090h'), 20)


if __name__ == '__main__':
    unittest.main()
