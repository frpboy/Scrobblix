"""Bind exact Git blobs and accept only Git's LF-to-CRLF checkout transform."""
import hashlib
import json
import pathlib
import subprocess
import sys

root = pathlib.Path(__file__).resolve().parents[2]
base = root / 'qa/alpha1-install'
manifest = base / 'closure-manifest.json'
files = [p for p in base.rglob('*') if p.is_file() and p != manifest
         and not set(p.relative_to(base).parts) & {'__pycache__', 'bin', 'obj'}]
files.append(root / '.github/workflows/android-signed-update-rehearsal.yml')

def digest(data):
    return hashlib.sha256(data).hexdigest()

def blob(path):
    return subprocess.check_output(['git', '-C', str(root), 'show', 'HEAD:' + path],
                                   stderr=subprocess.DEVNULL)

if sys.argv[1:] == ['write']:
    # Local preparation only. Publication/verification uses committed blob identity.
    hashes = {p.relative_to(root).as_posix(): digest(p.read_bytes()) for p in sorted(files)}
    manifest.write_text(json.dumps(hashes, sort_keys=True, indent=2) + '\n')
elif sys.argv[1:] == ['verify']:
    expected = json.loads(manifest.read_text())
    paths = {p.relative_to(root).as_posix(): p for p in files}
    assert set(expected) == set(paths), 'closure_file_set_mismatch'
    committed_manifest = blob(manifest.relative_to(root).as_posix())
    assert manifest.read_bytes() in (committed_manifest,
                                     committed_manifest.replace(b'\n', b'\r\n')), 'manifest_checkout_mismatch'
    for name, path in paths.items():
        committed = blob(name)
        assert digest(committed) == expected[name], 'committed_blob_hash_mismatch'
        # Never hash normalized arbitrary text. Require byte equality to the exact
        # reviewed Git blob or its sole supported checkout transform. A lone CR,
        # mixed line endings, whitespace/content edits and extra files fail closed.
        actual = path.read_bytes()
        if name.endswith('.gpg'):
            assert actual == committed, 'binary_checkout_bytes_mismatch'
        else:
            assert b'\r' not in committed, 'reviewed_blob_must_be_lf_only'
            assert actual in (committed, committed.replace(b'\n', b'\r\n')), 'checkout_bytes_mismatch'
    print('exact reviewed Git blobs and LF/CRLF-only checkout bytes verified')
else:
    raise SystemExit('use write or verify')
