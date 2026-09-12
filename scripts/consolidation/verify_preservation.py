"""Verify every archived original byte and, optionally, the local evidence vault."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile


def verify(repo, vault=None):
    manifest = json.loads((repo / 'docs/consolidation/preservation-manifest.json').read_text(encoding='utf-8'))
    issues = []
    verified = 0
    pending_local = 0
    seen = set()
    expected_archives = set()
    for source, archive in manifest['archives'].items():
        path = repo / archive['path']
        expected_archives.add(path.name)
        expected = {r['path']: r for r in manifest['files'] if r['source'] == source and r['preservation'] == 'repository-archive'}
        if not path.is_file():
            issues.append('MISSING ARCHIVE ' + source)
            continue
        if hashlib.sha256(path.read_bytes()).hexdigest() != archive['sha256']:
            issues.append('DIVERGENT ARCHIVE ' + source)
        with zipfile.ZipFile(path) as z:
            actual = z.namelist()
            if len(actual) != len(set(actual)):
                issues.append('DUPLICATE ZIP ENTRIES ' + source)
            for name in set(actual) - set(expected):
                issues.append('UNEXPECTED ENTRY ' + source + '/' + name)
            for name, row in expected.items():
                if name not in actual:
                    issues.append('MISSING ENTRY ' + source + '/' + name)
                    continue
                data = z.read(name)
                if len(data) != row['bytes'] or hashlib.sha256(data).hexdigest() != row['sha256']:
                    issues.append('DIVERGENT ENTRY ' + source + '/' + name)
                else:
                    verified += 1
    actual_archives = {p.name for p in (repo / 'archive/lineage').glob('*.zip')}
    for name in actual_archives - expected_archives:
        issues.append('UNEXPECTED ARCHIVE ' + name)
    for row in manifest['files']:
        key = (row['source'], row['path'])
        if key in seen:
            issues.append('DUPLICATE SOURCE PATH ' + str(key))
        seen.add(key)
        if row['preservation'] == 'repository-archive':
            if row['source'] not in manifest['archives']:
                issues.append('UNMAPPED ARCHIVE SOURCE ' + row['source'])
            continue
        if row['preservation'] != 'local-object':
            issues.append('UNMAPPED FILE ' + str(key))
            continue
        if vault is None:
            pending_local += 1
            continue
        path = vault / row['object']
        if not path.is_file():
            issues.append('MISSING LOCAL OBJECT ' + str(key))
            continue
        with path.open('rb') as stream:
            digest = hashlib.file_digest(stream, 'sha256').hexdigest()
        if path.stat().st_size != row['bytes'] or digest != row['sha256']:
            issues.append('DIVERGENT LOCAL OBJECT ' + str(key))
        else:
            verified += 1
    for source in manifest['sources']:
        population = [r for r in manifest['files'] if r['source'] == source['source']]
        if len(population) != source['files'] or sum(r['bytes'] for r in population) != source['bytes']:
            issues.append('SOURCE POPULATION MISMATCH ' + source['source'])
    result = {'expected': len(manifest['files']), 'verified': verified, 'localNotChecked': pending_local, 'issues': issues}
    print(json.dumps(result, indent=2))
    return 1 if issues else 0


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--repo', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--vault', type=Path)
    args = parser.parse_args()
    raise SystemExit(verify(args.repo.resolve(), args.vault.resolve() if args.vault else None))
