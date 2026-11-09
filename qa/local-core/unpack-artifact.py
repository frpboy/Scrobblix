import hashlib, pathlib, sys, zipfile
def digest(path):
    h=hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda:f.read(1024*1024),b''): h.update(block)
    return h.hexdigest()
source, target = map(pathlib.Path, sys.argv[1:])
assert source.stat().st_size == 135357479
assert digest(source) == '4eee8fad91d38cb992db6b1eb81e6ec10b83c80d9542a37bae5ae3097334d16f'
assert not target.exists()
with zipfile.ZipFile(source) as z:
    matching = [i for i in z.infolist() if i.filename == 'Scrobblix-Windows-TestBundle.zip']
    assert len(matching) == 1 and matching[0].file_size == 136124791
    with z.open(matching[0]) as src, target.open('xb') as dest:
        import shutil
        shutil.copyfileobj(src,dest,1024*1024)
assert target.stat().st_size == 136124791
assert digest(target) == '03af74a8686a2620d125c9d302da0ccbd11a1393c17fcc2075620eea09259245'
print('exact existing delivered ZIP verified')
