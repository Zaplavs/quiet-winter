"""Audit the explicit public repository, never parent directories or user data."""
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
tracked = subprocess.check_output(['git', 'ls-files', '-z'], cwd=ROOT).decode('utf-8').split('\0')
files = [name for name in tracked if name]
assert files, 'No tracked project files'
for name in files:
    p = Path(name)
    assert not ({'artifacts', 'dist', '.venv', '.codex', '.agents', '.claude'} & set(p.parts)), f'Local directory staged: {name}'
    assert not p.name.startswith('.env'), f'Environment file staged: {name}'
    assert p.suffix.lower() not in {'.log', '.zip', '.exe', '.pdb', '.ttf'}, f'Local binary/log staged: {name}'
    if p.suffix.lower() in {'.png', '.gif', '.mp4'}:
        assert name in {'docs/media/screenshot.png', 'docs/media/settings.png', 'docs/media/demo.gif', 'docs/media/demo.mp4'}, f'Unreviewed media staged: {name}'
    if p.suffix.lower() in {'.cs', '.md', '.py', '.ps1', '.html', '.json', '.yml'}:
        text = (ROOT / p).read_text(encoding='utf-8-sig')
        assert not re.search(r'(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,}|-----BEGIN (?:RSA |OPENSSH |EC )?PRIVATE KEY-----)', text), f'Credential pattern: {name}'
        assert not re.search(r'(?i)(?:C:[\\/]Users[\\/]|D:[\\/]pars_val[\\/])', text), f'Machine-specific path: {name}'

readme = (ROOT / 'README.md').read_text(encoding='utf-8')
for required in ['https://t.me/+ay37cKnFWtg3MDJi', 'https://www.donationalerts.com/r/ruszaphack', 'docs/media/demo.mp4', 'docs/media/demo.gif', 'docs/media/screenshot.png']:
    assert required in readme, f'Missing README link: {required}'
for relative in ['docs/media/screenshot.png', 'docs/media/settings.png', 'docs/media/demo.gif', 'docs/media/demo.mp4']:
    assert (ROOT / relative).is_file() and (ROOT / relative).stat().st_size > 100, f'Missing media: {relative}'
exporter = (ROOT / 'src/DemoScene.cs').read_text(encoding='utf-8')
for forbidden in ['CopyFromScreen', 'BitBlt', 'ImageGrab', 'ReadWindows(', 'GetWindowText']:
    assert forbidden not in exporter, 'Demo exporter must not capture real user content'
print(json.dumps({'passed': True, 'public_files': len(files), 'safe_demo': True, 'readme_links': True}))
