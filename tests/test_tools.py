import importlib.util
import json
from pathlib import Path
import struct
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    obj = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(obj)
    return obj


tool = module('patch_tool', ROOT / 'tools/patch.py')
release = module('release_tool', ROOT / 'tools/package_release.py')


class InstallGuards(unittest.TestCase):
    def setUp(self):
        local = ROOT / '.local'
        local.mkdir(exist_ok=True)
        self.tmp = tempfile.TemporaryDirectory(prefix='test-', dir=local)
        self.root = Path(self.tmp.name).resolve()
        assert self.root.is_relative_to(local.resolve())
        self.local = self.root / 'state'
        self.job = self.local / 'jobs/bongo-test'
        self.job.mkdir(parents=True)
        self.game = self.root / 'game'
        self.managed = self.game / 'BongoCat_Data/Managed'
        self.managed.mkdir(parents=True)
        executable = bytearray(128)
        executable[:2] = b'MZ'
        struct.pack_into('<I', executable, 60, 64)
        executable[64:68] = b'PE\0\0'
        struct.pack_into('<H', executable, 68, 0x8664)
        (self.game / 'BongoCat.exe').write_bytes(executable)
        self.original = self.job / 'original.dll'
        self.original.write_bytes(b'original')
        self.assembly = self.managed / 'Assembly-CSharp.dll'
        self.assembly.write_bytes(b'original')
        for name in ('mscorlib.dll', 'System.dll'):
            (self.managed / name).write_bytes(b'fixture')
        (self.job / 'candidate').mkdir()
        (self.job / 'candidate/Assembly-CSharp.dll').write_bytes(b'candidate')
        (self.job / 'BongoLocalAutoTap.dll').write_bytes(b'runtime')
        self.runtime = self.managed / 'BongoLocalAutoTap.dll'
        self.data = {'schema': 1, 'game': 'bongo', 'job': 'bongo-test',
                     'game_dir': str(self.game), 'known_build': True,
                     'auto_chests': True, 'installed': False,
                     'original_sha256': tool.sha(self.original),
                     'candidate_sha256': tool.sha(self.job / 'candidate/Assembly-CSharp.dll'),
                     'runtime_sha256': tool.sha(self.job / 'BongoLocalAutoTap.dll')}
        self.patches = [patch.object(tool, 'LOCAL', self.local),
                        patch.object(tool, 'windows_only'), patch.object(tool, 'require_closed')]
        for p in self.patches:
            p.start()
        self.write_manifest()

    def write_manifest(self):
        tool.write_json(self.local / 'bongo.json', self.data)

    def tearDown(self):
        for p in reversed(self.patches):
            p.stop()
        assert self.root.is_relative_to((ROOT / '.local').resolve())
        self.tmp.cleanup()

    def test_install_restore_round_trip(self):
        tool.install('bongo')
        self.assertEqual(self.assembly.read_bytes(), b'candidate')
        self.assertTrue((self.managed / tool.CHEST_FLAG).exists())
        tool.restore('bongo')
        self.assertEqual(self.assembly.read_bytes(), b'original')
        self.assertFalse(self.runtime.exists())
        self.assertTrue(self.original.exists())

    def test_update_not_overwritten_by_install(self):
        self.assembly.write_bytes(b'new update')
        with self.assertRaises(RuntimeError):
            tool.install('bongo')
        self.assertEqual(self.assembly.read_bytes(), b'new update')

    def test_update_not_overwritten_by_restore(self):
        tool.install('bongo')
        self.assembly.write_bytes(b'new update')
        with self.assertRaises(RuntimeError):
            tool.restore('bongo')
        self.assertEqual(self.assembly.read_bytes(), b'new update')

    def test_unknown_build_requires_explicit_opt_in(self):
        self.data['known_build'] = False
        self.write_manifest()
        with self.assertRaises(RuntimeError):
            tool.install('bongo')
        self.assertFalse(self.runtime.exists())

    def test_existing_runtime_is_preserved(self):
        self.runtime.write_bytes(b'other mod')
        with self.assertRaises(RuntimeError):
            tool.install('bongo')
        self.assertEqual(self.runtime.read_bytes(), b'other mod')

    def test_corrupt_candidate_is_rejected(self):
        (self.job / 'candidate/Assembly-CSharp.dll').write_bytes(b'corrupt')
        with self.assertRaises(RuntimeError):
            tool.install('bongo')
        self.assertEqual(self.assembly.read_bytes(), b'original')

    def test_failed_copy_rolls_back_runtime_and_game(self):
        real_copy = tool.shutil.copyfile

        def fail_candidate(source, destination):
            if Path(source) == self.job / 'candidate/Assembly-CSharp.dll':
                raise OSError('Simulated write failure')
            return real_copy(source, destination)
        with patch.object(tool.shutil, 'copyfile', side_effect=fail_candidate):
            with self.assertRaises(OSError):
                tool.install('bongo')
        self.assertEqual(self.assembly.read_bytes(), b'original')
        self.assertFalse(self.runtime.exists())

    def test_manifest_cannot_escape_state_directory(self):
        self.data['job'] = '../outside'
        self.write_manifest()
        with self.assertRaises(RuntimeError):
            tool.load('bongo')

    def test_only_x64_game_executable_accepted(self):
        exe = self.game / 'BongoCat.exe'
        content = bytearray(exe.read_bytes())
        struct.pack_into('<H', content, 68, 0x14c)
        exe.write_bytes(content)
        with self.assertRaises(RuntimeError):
            tool.managed_path('bongo', self.game)


class ProcessAndReleaseGuards(unittest.TestCase):
    def test_running_game_is_blocked(self):
        fake = type('Process', (), {'info': {'name': 'TapTapLoot.exe'}})()
        with patch('psutil.process_iter', return_value=[fake]):
            with self.assertRaises(RuntimeError):
                tool.require_closed('ttl')

    def test_source_release_excludes_local_state(self):
        files = release.source_files(ROOT)
        self.assertTrue(files)
        self.assertFalse(any('.local' in p.relative_to(ROOT).parts for p in files))
        self.assertFalse(any(p.suffix.lower() in {'.dll', '.exe', '.log', '.png'} for p in files))

    def test_runtime_contains_no_global_input_injection(self):
        text = '\n'.join(p.read_text() for p in (ROOT / 'src/runtime').glob('*.cs'))
        for prohibited in ('SendInput(', 'SetCursorPos(', 'SetForegroundWindow(', 'mouse_event(', 'keybd_event('):
            self.assertNotIn(prohibited, text)


if __name__ == '__main__':
    unittest.main()
