"""Source-only build and guarded local install. No global input is generated."""
from __future__ import annotations

import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import platform
import shutil
import struct
import subprocess
import sys
import urllib.request
import uuid
import zipfile

ROOT = Path(__file__).resolve().parents[1]
LOCAL = ROOT / '.local'
PROFILES = {
    'bongo': {'directory': 'BongoCat', 'runtime': 'BongoLocalAutoTap.dll',
              'source': 'LocalAutoTap.cs',
              'known': {'25dbce06fdf0c16d302fed8287e3f3f1edad6f4c41747f37826a01f94049f927'}},
    'ttl': {'directory': 'TapTapLoot', 'runtime': 'TapLootLocalAutoTap.dll',
            'source': 'TapLootAutoTap.cs',
            'known': {'716d02bd8368b44c227fce80741b4f4d806cf9590125ea9855b59068d111c675'}},
}
CECIL_URL = 'https://api.nuget.org/v3-flatcontainer/mono.cecil/0.11.6/mono.cecil.0.11.6.nupkg'
PACKAGE_SHA = 'd2a23832aaa948ba9a01acc42b5726e34c5f995958f1b30d45c0e7c70b3a72d5'
CECIL_SHA = 'c41bdb9ffd3c5f6e17d2382c1012d73703e035e3f1100245fdd4e08c8dc6eb5b'
CHEST_FLAG = 'bongo_autoclaim.enabled'


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path: Path, data: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_suffix('.json.tmp')
    temp.write_text(json.dumps(data, indent=2), encoding='utf-8')
    temp.replace(path)


def windows_only() -> None:
    if os.name != 'nt' or struct.calcsize('P') != 8 or platform.machine().lower() not in ('amd64', 'x86_64'):
        raise RuntimeError('Use 64-bit Python on Windows x64. ARM64 and other systems are unverified.')


def require_closed(game: str) -> None:
    import psutil
    executable = PROFILES[game]['directory'].lower() + '.exe'
    if any((p.info['name'] or '').lower() == executable for p in psutil.process_iter(['name'])):
        raise RuntimeError('Close ' + executable + ' normally first; this tool never kills games.')


def fetch_deps() -> None:
    destination = LOCAL / 'deps' / 'Mono.Cecil.dll'
    if destination.exists() and sha(destination) == CECIL_SHA:
        print('Pinned Mono.Cecil already available.')
        return
    with urllib.request.urlopen(CECIL_URL, timeout=60) as response:
        package = response.read()
    if hashlib.sha256(package).hexdigest() != PACKAGE_SHA:
        raise RuntimeError('NuGet package checksum mismatch.')
    content = zipfile.ZipFile(io.BytesIO(package)).read('lib/net40/Mono.Cecil.dll')
    if hashlib.sha256(content).hexdigest() != CECIL_SHA:
        raise RuntimeError('Mono.Cecil checksum mismatch.')
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(content)
    print('Downloaded and verified Mono.Cecil 0.11.6 from NuGet.')


def managed_path(game: str, game_dir: Path) -> Path:
    name = PROFILES[game]['directory']
    root = game_dir.resolve()
    executable = root / (name + '.exe')
    data = executable.read_bytes()
    if data[:2] != b'MZ' or len(data) < 64:
        raise RuntimeError('Invalid game executable.')
    offset = struct.unpack_from('<I', data, 60)[0]
    if data[offset:offset+4] != b'PE\0\0' or struct.unpack_from('<H', data, offset+4)[0] != 0x8664:
        raise RuntimeError('Only the Windows x64 game executable is supported.')
    managed = root / (name + '_Data') / 'Managed'
    for filename in ('Assembly-CSharp.dll', 'mscorlib.dll', 'System.dll'):
        if not (managed / filename).is_file():
            raise RuntimeError('Unity Mono managed files missing: ' + filename)
    return managed


def run(arguments: list[str]) -> None:
    subprocess.run([str(x) for x in arguments], check=True, shell=False)


def prepare(game: str, game_dir: Path, auto_chests: bool = False,
            allow_unknown: bool = False, csc: Path | None = None) -> dict:
    windows_only()
    if auto_chests and game != 'bongo':
        raise RuntimeError('Auto-chests apply only to Bongo Cat.')
    managed = managed_path(game, game_dir)
    source = managed / 'Assembly-CSharp.dll'
    original_hash = sha(source)
    if any(name in source.read_bytes() for name in (b'BongoLocalAutoTap', b'TapLootLocalAutoTap')):
        raise RuntimeError('Already patched. Restore with the tool that installed it before preparing again.')
    known = original_hash in PROFILES[game]['known']
    if not known and not allow_unknown:
        raise RuntimeError('Unknown game build. Refusing by default; see docs/COMPATIBILITY.md.')
    compiler = csc or Path(os.environ.get('WINDIR', r'C:\Windows')) / 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    if not compiler.is_file():
        raise RuntimeError('C# compiler missing. Pass --csc with a compatible .NET Framework compiler.')
    cecil = LOCAL / 'deps/Mono.Cecil.dll'
    if not cecil.exists() or sha(cecil) != CECIL_SHA:
        raise RuntimeError('Run: python tools/patch.py fetch-deps')
    job_name = game + '-' + uuid.uuid4().hex
    job = LOCAL / 'jobs' / job_name
    job.mkdir(parents=True)
    original = job / 'original.dll'
    shutil.copyfile(source, original)
    runtime = job / PROFILES[game]['runtime']
    runtime_dir = ROOT / 'src/runtime'
    run([compiler, '/nologo', '/noconfig', '/nostdlib+', '/target:library',
         '/r:' + str(managed / 'mscorlib.dll'), '/r:' + str(managed / 'System.dll'),
         '/out:' + str(runtime), runtime_dir / PROFILES[game]['source'],
         runtime_dir / 'SharedF7.cs', runtime_dir / 'UnityCompilerCompatibility.cs'])
    shutil.copyfile(cecil, job / 'Mono.Cecil.dll')
    patchers = ROOT / 'src/patchers'
    for name in ('AddAutoTap', 'PatchTapLoot', 'PreparePatch'):
        run([compiler, '/nologo', '/r:' + str(cecil), '/out:' + str(job / (name + '.exe')),
             patchers / (name + '.cs')])
    candidate = job / 'candidate'
    if game == 'bongo':
        input_file = original
        if auto_chests:
            chest = job / 'chests'
            run([job / 'PreparePatch.exe', original, chest, managed / CHEST_FLAG])
            input_file = chest / 'Assembly-CSharp.dll'
        run([job / 'AddAutoTap.exe', input_file, runtime, candidate])
    else:
        run([job / 'PatchTapLoot.exe', original, runtime, candidate])
    if sha(source) != original_hash:
        raise RuntimeError('Game changed during prepare; candidate not activated.')
    manifest = {'schema': 1, 'game': game, 'game_dir': str(game_dir.resolve()),
                'job': job_name, 'original_sha256': original_hash,
                'candidate_sha256': sha(candidate / 'Assembly-CSharp.dll'),
                'runtime_sha256': sha(runtime), 'auto_chests': auto_chests,
                'known_build': known, 'target_cps': 30, 'installed': False}
    write_json(LOCAL / (game + '.json'), manifest)
    print('Prepared locally; game files were not modified. known_build=' + str(known))
    return manifest


def load(game: str) -> tuple[dict, Path, Path]:
    manifest = json.loads((LOCAL / (game + '.json')).read_text(encoding='utf-8'))
    job_name = manifest['job']
    if manifest.get('schema') != 1 or manifest['game'] != game or Path(job_name).name != job_name or not job_name.startswith(game + '-'):
        raise RuntimeError('Invalid local manifest.')
    jobs = (LOCAL / 'jobs').resolve()
    job = (jobs / job_name).resolve()
    if job.parent != jobs:
        raise RuntimeError('Local job escaped state directory.')
    managed = managed_path(game, Path(manifest['game_dir']))
    return manifest, job, managed


def validate_job(game: str, data: dict, job: Path) -> None:
    files = [(job / 'original.dll', data['original_sha256']),
             (job / 'candidate/Assembly-CSharp.dll', data['candidate_sha256']),
             (job / PROFILES[game]['runtime'], data['runtime_sha256'])]
    if any(not p.exists() or sha(p) != expected for p, expected in files):
        raise RuntimeError('Backup/candidate integrity check failed.')


def install(game: str, allow_unverified: bool = False) -> None:
    windows_only()
    require_closed(game)
    data, job, managed = load(game)
    validate_job(game, data, job)
    if not data['known_build'] and not allow_unverified:
        raise RuntimeError('Unverified build: install refused. Maintainer testing requires --allow-unverified.')
    assembly = managed / 'Assembly-CSharp.dll'
    runtime = managed / PROFILES[game]['runtime']
    if sha(assembly) != data['original_sha256']:
        raise RuntimeError('Game no longer matches the prepared original; refusing overwrite.')
    if runtime.exists():
        raise RuntimeError('Runtime file already exists; restore the previous installation first.')
    flag = managed / CHEST_FLAG
    if data['auto_chests'] and flag.exists():
        raise RuntimeError('An unknown chest flag already exists; inspect the previous installation.')
    try:
        shutil.copyfile(job / PROFILES[game]['runtime'], runtime)
        shutil.copyfile(job / 'candidate/Assembly-CSharp.dll', assembly)
        if sha(runtime) != data['runtime_sha256'] or sha(assembly) != data['candidate_sha256']:
            raise RuntimeError('Installed file checksum mismatch.')
        if data['auto_chests']:
            flag.write_text('enabled\n', encoding='ascii')
        data['installed'] = True
        write_json(LOCAL / (game + '.json'), data)
    except Exception:
        shutil.copyfile(job / 'original.dll', assembly)
        if runtime.exists() and sha(runtime) == data['runtime_sha256']:
            runtime.unlink()
        if data['auto_chests'] and flag.exists():
            flag.unlink()
        raise
    print('Installed. Start the game; F7 controls auto-tap. Verify before relying on it.')


def restore(game: str) -> None:
    windows_only()
    require_closed(game)
    data, job, managed = load(game)
    validate_job(game, data, job)
    assembly = managed / 'Assembly-CSharp.dll'
    runtime = managed / PROFILES[game]['runtime']
    if sha(assembly) != data['candidate_sha256']:
        raise RuntimeError('Game changed (possibly an update); old backup will NOT overwrite it.')
    if not runtime.exists() or sha(runtime) != data['runtime_sha256']:
        raise RuntimeError('Runtime changed or missing; inspect manually before restoring.')
    shutil.copyfile(job / 'original.dll', assembly)
    if sha(assembly) != data['original_sha256']:
        raise RuntimeError('Restore verification failed; runtime retained.')
    runtime.unlink()
    if data['auto_chests']:
        (managed / CHEST_FLAG).unlink(missing_ok=True)
    data['installed'] = False
    write_json(LOCAL / (game + '.json'), data)
    print('Original game assembly restored. Local backups retained.')


def status(game: str) -> None:
    data, job, managed = load(game)
    assembly = sha(managed / 'Assembly-CSharp.dll')
    runtime = managed / PROFILES[game]['runtime']
    state = ('patched' if assembly == data['candidate_sha256'] else
             'original' if assembly == data['original_sha256'] else 'changed')
    print(json.dumps({'game': game, 'assembly_state': state,
                      'runtime_matches': runtime.exists() and sha(runtime) == data['runtime_sha256'],
                      'known_build': data['known_build'], 'target_cps': 30,
                      'auto_chests_enabled': data['auto_chests'] and (managed / CHEST_FLAG).exists()}, indent=2))


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='action', required=True)
    sub.add_parser('fetch-deps')
    build = sub.add_parser('prepare')
    build.add_argument('--game', required=True, choices=PROFILES)
    build.add_argument('--game-dir', required=True, type=Path)
    build.add_argument('--auto-chests', action='store_true')
    build.add_argument('--allow-unknown-build', action='store_true')
    build.add_argument('--csc', type=Path)
    for command in ('install', 'restore', 'status', 'chests'):
        cmd = sub.add_parser(command)
        cmd.add_argument('--game', required=True, choices=PROFILES)
        if command == 'install':
            cmd.add_argument('--allow-unverified', action='store_true')
        if command == 'chests':
            cmd.add_argument('--enabled', choices=('on', 'off'), required=True)
    args = parser.parse_args()
    if args.action == 'fetch-deps':
        fetch_deps()
    elif args.action == 'prepare':
        prepare(args.game, args.game_dir, args.auto_chests, args.allow_unknown_build, args.csc)
    elif args.action == 'install':
        install(args.game, args.allow_unverified)
    elif args.action == 'restore':
        restore(args.game)
    elif args.action == 'status':
        status(args.game)
    else:
        if args.game != 'bongo':
            raise RuntimeError('TapTapLoot has no chest module.')
        data, _, managed = load(args.game)
        if not data['auto_chests'] or sha(managed / 'Assembly-CSharp.dll') != data['candidate_sha256']:
            raise RuntimeError('A matching auto-chest patch is not installed.')
        flag = managed / CHEST_FLAG
        if args.enabled == 'on':
            flag.write_text('enabled\n', encoding='ascii')
        else:
            flag.unlink(missing_ok=True)
        print('Chest auto-claim ' + args.enabled)


if __name__ == '__main__':
    try:
        main()
    except (OSError, RuntimeError, ValueError, KeyError, subprocess.CalledProcessError) as error:
        print('ERROR: ' + str(error), file=sys.stderr)
        sys.exit(1)
