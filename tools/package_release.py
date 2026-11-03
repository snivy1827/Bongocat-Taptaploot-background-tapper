"""Create a source-only ZIP from an explicit directory and extension allowlist."""
from pathlib import Path
import hashlib
import zipfile

ROOT = Path(__file__).resolve().parents[1]
ROOT_FILES = {'README.md', 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'CONTRIBUTING.md',
              'SECURITY.md', 'requirements.txt', 'VERSION', '.gitignore'}
SOURCE_DIRS = ('src', 'tools', 'tests', 'docs', '.github')
EXTENSIONS = {'.cs', '.py', '.md', '.txt', '.yml', '.yaml'}


def source_files(root: Path) -> list[Path]:
    result = [root / name for name in ROOT_FILES if (root / name).is_file()]
    for directory in SOURCE_DIRS:
        if not (root / directory).exists():
            continue
        for path in (root / directory).rglob('*'):
            if '__pycache__' in path.parts or not path.is_file():
                continue
            if path.is_symlink() or not path.resolve().is_relative_to(root.resolve()):
                raise RuntimeError('Symlink/outside source: ' + str(path))
            if path.suffix.lower() not in EXTENSIONS:
                raise RuntimeError('Non-source file in release tree: ' + str(path))
            result.append(path)
    for path in result:
        if path.is_symlink() or not path.resolve().is_relative_to(root.resolve()):
            raise RuntimeError('Unsafe source path: ' + str(path))
        text = path.read_text(encoding='utf-8')
        private_markers = ['C:' + '\\Users\\', 'sk-' + 'proj-', 'gh' + 'p_']
        if any(marker in text for marker in private_markers):
            raise RuntimeError('Potential private path or credential in ' + str(path))
    return sorted(result)


def main() -> None:
    version = (ROOT / 'VERSION').read_text().strip()
    if not version or any(c not in '0123456789.-' for c in version):
        raise RuntimeError('Invalid VERSION')
    files = source_files(ROOT)
    destination = ROOT / 'dist' / ('game-background-tapper-source-' + version + '.zip')
    destination.parent.mkdir(exist_ok=True)
    with zipfile.ZipFile(destination, 'w', zipfile.ZIP_DEFLATED) as archive:
        for path in files:
            archive.write(path, 'game-background-tapper/' + path.relative_to(ROOT).as_posix())
    checksum = hashlib.sha256(destination.read_bytes()).hexdigest()
    destination.with_suffix('.zip.sha256').write_text(checksum + '  ' + destination.name + '\n', encoding='ascii')
    print(str(destination))
    print(str(len(files)) + ' source files; no .local, game binaries or personal logs.')
    print('SHA256 ' + checksum)


if __name__ == '__main__':
    main()
