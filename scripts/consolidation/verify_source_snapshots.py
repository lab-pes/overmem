"""Fail when a duplicate source changed after its preservation inventory."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main(manifest_path, canonical_root):
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    rows_by_source = {}
    for row in manifest['files']:
        rows_by_source.setdefault(row['source'], {})[row['path']] = row
    issues = []
    checked = 0
    for source in manifest['sources']:
        name = source['source']
        root = Path(source['root'])
        if root.resolve() == canonical_root.resolve():
            continue
        expected = rows_by_source.get(name, {})
        excluded = {Path(item).as_posix().rstrip('/') for item in source['excluded_directories']}
        actual = {}
        if not root.is_dir():
            issues.append({'source': name, 'kind': 'SOURCE_MISSING', 'path': str(root)})
            continue
        for directory, dirs, files in os.walk(root):
            relative_dir = Path(directory).relative_to(root).as_posix()
            kept = []
            for item in dirs:
                relative = (Path(relative_dir) / item).as_posix() if relative_dir != '.' else item
                if relative == '.git' or relative in excluded:
                    continue
                kept.append(item)
            dirs[:] = kept
            for filename in files:
                path = Path(directory) / filename
                relative = path.relative_to(root).as_posix()
                if relative == '.git':
                    continue
                actual[relative] = path
        for relative in sorted(set(expected) - set(actual)):
            issues.append({'source': name, 'kind': 'MISSING', 'path': relative})
        for relative in sorted(set(actual) - set(expected)):
            issues.append({'source': name, 'kind': 'UNEXPECTED', 'path': relative})
        for relative in sorted(set(expected) & set(actual)):
            row = expected[relative]
            path = actual[relative]
            if path.stat().st_size != row['bytes'] or digest(path) != row['sha256']:
                issues.append({'source': name, 'kind': 'DIVERGENT', 'path': relative})
            else:
                checked += 1

    canonical_rows = rows_by_source['overmem']
    result = subprocess.run(['git', '-C', str(canonical_root), 'status', '--porcelain=v1', '-z', '--untracked-files=all'],
                            capture_output=True, check=True)
    untracked = []
    for entry in result.stdout.decode('utf-8', errors='strict').split('\0'):
        if entry.startswith('?? '):
            untracked.append(entry[3:])
    for relative in untracked:
        path = canonical_root / relative
        row = canonical_rows.get(relative)
        if row is None:
            issues.append({'source': 'overmem', 'kind': 'UNPRESERVED_CANONICAL_UNTRACKED', 'path': relative})
        elif path.stat().st_size != row['bytes'] or digest(path) != row['sha256']:
            issues.append({'source': 'overmem', 'kind': 'DIVERGENT_CANONICAL_UNTRACKED', 'path': relative})
        else:
            checked += 1
    output = {'status': 'PASS' if not issues else 'FAIL', 'verifiedFiles': checked,
              'canonicalUntrackedVerified': len(untracked), 'issues': issues}
    print(json.dumps(output, indent=2))
    return 0 if not issues else 1


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--manifest', type=Path, required=True)
    parser.add_argument('--canonical-root', type=Path, required=True)
    arguments = parser.parse_args()
    raise SystemExit(main(arguments.manifest.resolve(), arguments.canonical_root.resolve()))
