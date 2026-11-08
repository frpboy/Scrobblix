"""Bounded synthetic About pixels only. No arbitrary file paths or recursive collection."""
import base64,hashlib,json,pathlib,re,sys,stat,os
NAMES=tuple(f'{prefix}-{scale}-{suffix}.png' for scale in ('1.0','2.0') for prefix,suffix in [('about','top'),('about','identity'),('about','failure'),('sponsor','')])
NAMES=tuple(x.replace('-.png','.png') for x in NAMES)
MAX_FILE=512*1024
MAX_TOTAL=2*1024*1024
CHUNK=768
MAX_LOG=8*1024*1024
TAG='SCROBBLIX_ABOUT_PIXEL_V1 '

def encode(directory):
    root=directory.lstat()
    if not stat.S_ISDIR(root.st_mode) or stat.S_ISLNK(root.st_mode):raise ValueError('Root must be nonsymlink directory')
    files=[]
    for name in NAMES:
        p=directory/name
        before=p.lstat()
        if not stat.S_ISREG(before.st_mode) or stat.S_ISLNK(before.st_mode):raise ValueError('Regular nonsymlink file required')
        # O_NONBLOCK prevents a replacement FIFO from hanging open; fstat rejects it.
        fd=os.open(p,os.O_RDONLY|os.O_NOFOLLOW|os.O_NONBLOCK)
        with os.fdopen(fd,'rb') as handle:
            opened=os.fstat(handle.fileno())
            if not stat.S_ISREG(opened.st_mode) or (opened.st_dev,opened.st_ino)!=(before.st_dev,before.st_ino):raise ValueError('File changed/type mismatch')
            b=handle.read(MAX_FILE+1)
        if not b.startswith(b'\x89PNG\r\n\x1a\n') or not 8<=len(b)<=MAX_FILE:raise ValueError('PNG header/size')
        files.append((name,b))
    if sum(len(b) for _,b in files)>MAX_TOTAL:raise ValueError('Total size')
    records=[]
    for name,b in files:
        text=base64.b64encode(b).decode('ascii');parts=[text[i:i+CHUNK] for i in range(0,len(text),CHUNK)]
        records.append(dict(kind='file',name=name,size=len(b),sha256=hashlib.sha256(b).hexdigest(),chunks=len(parts)))
        records.extend(dict(kind='chunk',name=name,index=i,data=p) for i,p in enumerate(parts))
    records.append(dict(kind='end',files=len(files),total=sum(len(b)for _,b in files)))
    return '\n'.join(TAG+json.dumps(r,separators=(',',':'))for r in records)+'\n'

def decode(text,directory):
    if len(text.encode("utf-8"))>MAX_LOG:raise ValueError("Raw log limit")
    metadata={};parts={};end=None
    for line in text.splitlines():
        if TAG not in line:continue
        raw=line.split(TAG,1)[1];r=json.loads(raw);kind=r.get('kind')
        if end is not None:raise ValueError('Records after end')
        if kind=='end':end=r;continue
        name=r.get('name')
        if name not in NAMES:raise ValueError('Unknown filename')
        if kind=='file':
            if name in metadata or not 8<=r['size']<=MAX_FILE or not 1<=r['chunks']<=1000:raise ValueError('Invalid file record')
            metadata[name]=r;parts[name]={}
        elif kind=='chunk':
            if name not in metadata or not isinstance(r['index'],int) or not 0<=r['index']<metadata[name]['chunks'] or r['index']in parts[name] or len(r['data'])>CHUNK:raise ValueError('Chunk inventory')
            parts[name][r['index']]=r['data']
        else:raise ValueError('Unknown record')
    if set(metadata)!=set(NAMES) or end is None or end.get('files')!=len(NAMES):raise ValueError('Incomplete files/end')
    recovered={}
    for name,m in metadata.items():
        if len(parts[name])!=m['chunks']:raise ValueError('Missing chunk')
        b=base64.b64decode(''.join(parts[name][i]for i in range(m['chunks'])),validate=True)
        if len(b)!=m['size'] or hashlib.sha256(b).hexdigest()!=m['sha256'] or not b.startswith(b'\x89PNG\r\n\x1a\n'):raise ValueError('Bytes/hash mismatch')
        recovered[name]=b
    total=sum(map(len,recovered.values()))
    if total>MAX_TOTAL or end.get('total')!=total:raise ValueError('Total mismatch')
    directory.mkdir(parents=True,exist_ok=False)
    for name,b in recovered.items():(directory/name).write_bytes(b)
    return {name:hashlib.sha256(b).hexdigest()for name,b in recovered.items()}

if __name__=='__main__':
    if len(sys.argv)!=3:raise SystemExit('encode DIRECTORY or decode RAW_LOG requires OUTPUT_DIRECTORY as fourth argument')
    # Decode uses stdin, ensuring no accidental arbitrary log path traversal.
    if sys.argv[1]=='encode':print(encode(pathlib.Path(sys.argv[2])),end='')
    elif sys.argv[1]=='decode':
        raw=sys.stdin.buffer.read(MAX_LOG+1)
        if len(raw)>MAX_LOG:raise ValueError('Raw log limit')
        print(json.dumps(decode(raw.decode('utf-8'),pathlib.Path(sys.argv[2])),sort_keys=True))
    else:raise SystemExit('Unsupported mode')
