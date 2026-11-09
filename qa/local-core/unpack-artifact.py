import hashlib, pathlib, sys, zipfile
def digest(path):
    h=hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda:f.read(1024*1024),b''): h.update(block)
    return h.hexdigest()
source, target = map(pathlib.Path, sys.argv[1:])
assert source.stat().st_size == 135360417
assert digest(source) == 'c9407c3f7c26ad5a3a39dbdaa85ccb3655e49cb3ccc0e5f9d4004301ed82ea0b'
assert not target.exists()
with zipfile.ZipFile(source) as z:
    matching = [i for i in z.infolist() if i.filename == 'Scrobblix-Windows-TestBundle.zip']
    assert len(matching) == 1 and matching[0].file_size == 136124914
    with z.open(matching[0]) as src, target.open('xb') as dest:
        import shutil
        shutil.copyfileobj(src,dest,1024*1024)
assert target.stat().st_size == 136124914
assert digest(target) == '5191988d22cd2c9c6ee8c6942dec43dd85d1882115790ef4123d8f06de604afc'
print('exact existing delivered ZIP verified')
