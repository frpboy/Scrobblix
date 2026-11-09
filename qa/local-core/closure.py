import hashlib, json, pathlib, sys
root=pathlib.Path(__file__).resolve().parents[2]
base=root/'qa/local-core'
manifest=base/'closure-manifest.json'
files=[p for p in base.rglob('*') if p.is_file() and p!=manifest
       and not set(p.relative_to(base).parts)&{'__pycache__','bin','obj'}]
files.append(root/'.github/workflows/scrobblix-local-core-once.yml')
hashes={p.relative_to(root).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(files)}
if sys.argv[1:] == ['write']:
    manifest.write_text(json.dumps(hashes,sort_keys=True,indent=2)+'\n')
elif sys.argv[1:] == ['verify']:
    assert json.loads(manifest.read_text())==hashes, 'closure_file_or_hash_mismatch'
    print('exact local-core source closure verified')
else:
    raise SystemExit('use write or verify')
