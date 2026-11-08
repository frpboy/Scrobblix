"""Six fixed synthetic effective-bar screenshots using the existing bounded transport."""
import importlib.util,pathlib,sys
s=importlib.util.spec_from_file_location('pixels',pathlib.Path(__file__).with_name('about-pixel-log.py'))
pixels=importlib.util.module_from_spec(s);s.loader.exec_module(pixels)
pixels.NAMES=tuple(f'bars-{h}-{p}.png'for h in ('fixture','main')for p in ('adb','ui-before','ui-after'))
pixels.MAX_TOTAL=3*1024*1024
encode=pixels.encode
decode=pixels.decode
if __name__=='__main__':
    import json
    if len(sys.argv)!=3:raise SystemExit('encode DIRECTORY or decode NEW_DIRECTORY via stdin')
    if sys.argv[1]=='encode':print(encode(pathlib.Path(sys.argv[2])),end='')
    elif sys.argv[1]=='decode':
        raw=sys.stdin.buffer.read(pixels.MAX_LOG+1)
        if len(raw)>pixels.MAX_LOG:raise SystemExit('Log limit')
        print(json.dumps(decode(raw.decode('utf-8'),pathlib.Path(sys.argv[2])),sort_keys=True))
    else:raise SystemExit('Unsupported mode')
