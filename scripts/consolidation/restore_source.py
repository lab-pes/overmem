"""Restore one preserved source into a NEW directory; never overwrite an existing tree."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import zipfile

parser = argparse.ArgumentParser()
parser.add_argument('--source', required=True)
parser.add_argument('--output', required=True, type=Path)
parser.add_argument('--vault', type=Path)
parser.add_argument('--source-only', action='store_true', help='Restore repository archives; omit local evidence objects.')
args = parser.parse_args()
repo = Path(__file__).resolve().parents[2]
manifest = json.loads((repo / 'docs/consolidation/preservation-manifest.json').read_text(encoding='utf-8'))
rows = [r for r in manifest['files'] if r['source'] == args.source]
if not rows:
    raise SystemExit('Unknown source: ' + args.source)
if args.output.exists():
    raise SystemExit('Output must be a new directory.')
if args.vault is None and not args.source_only and any(r['preservation'] == 'local-object' for r in rows):
    raise SystemExit('Use --vault to include evidence, or --source-only for the archived source snapshot.')
output = args.output.resolve()
restored = 0
for row in rows:
    relative = PurePosixPath(row['path'])
    if relative.is_absolute() or '..' in relative.parts or ':' in row['path'] or '\\' in row['path']:
        raise SystemExit('Unsafe manifest path: ' + row['path'])
    if row['preservation'] == 'repository-archive':
        with zipfile.ZipFile(repo / row['archive']) as z:
            data = z.read(row['path'])
    elif args.source_only:
        continue
    else:
        data = (args.vault / row['object']).read_bytes()
    if len(data) != row['bytes'] or hashlib.sha256(data).hexdigest() != row['sha256']:
        raise SystemExit('Preservation hash mismatch: ' + row['path'])
    target = output.joinpath(*relative.parts)
    target.parent.mkdir(parents=True, exist_ok=True)
    with target.open('xb') as stream:
        stream.write(data)
    restored += 1
print(json.dumps({'source': args.source, 'restored': restored, 'output': str(output)}))
