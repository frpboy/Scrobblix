"""Not a standalone authorization. Called only by reviewed API36 isolated candidate runner.
Never persists or prints raw dumps/instrumentation. Fixed commands, caps and timeout.
"""
import importlib.util,json,pathlib,re,selectors,subprocess,sys,time
ROOT=pathlib.Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('fields',ROOT/'about-effective-fields.py');fields=importlib.util.module_from_spec(spec);spec.loader.exec_module(fields)
PACKAGE='com.frpboy.scrobblix'
OUT=pathlib.Path('android-about-effective-evidence')

def bounded(args,limit=262144,seconds=5):
    p=subprocess.Popen(args,stdout=subprocess.PIPE,stderr=subprocess.DEVNULL)
    sel=selectors.DefaultSelector();sel.register(p.stdout,selectors.EVENT_READ)
    data=bytearray();deadline=time.monotonic()+seconds
    try:
        while sel.get_map():
            remaining=deadline-time.monotonic()
            if remaining<=0:raise ValueError('command_timeout')
            for key,_ in sel.select(min(remaining,.2)):
                b=key.fileobj.read1(8192)
                if not b:sel.unregister(key.fileobj);continue
                data.extend(b)
                if len(data)>limit:raise ValueError('command_output_limit')
        if p.wait(timeout=max(.1,deadline-time.monotonic()))!=0:raise ValueError('command_failed')
        return bytes(data)
    finally:
        sel.close()
        if p.poll() is None:p.kill()
        p.wait();p.stdout.close()

def adb(*args,**kwargs):return bounded(['adb',*args],**kwargs)

def app_records(raw,host):
    lines=fields.lines(raw)
    pattern=(r'BARS_APP host='+host+r' phase=(before|after) resumed=true focused=true '
        r'decor=(-?\d{1,5},){3}-?\d{1,5} visible=(-?\d{1,5},){3}-?\d{1,5} '
        r'display=\d{1,5},\d{1,5} nav=(\d{1,5},){3}\d{1,5} '
        r'navVisible=(true|false) statusLight=(true|false) navLight=(true|false) contrast=(true|false)')
    found=[l.removeprefix('INSTRUMENTATION_STATUS: stream=') for l in lines if l.startswith(('BARS_APP ','INSTRUMENTATION_STATUS: stream=BARS_APP '))]
    if len(found)!=2 or not all(re.fullmatch(pattern,l) for l in found):raise ValueError('app_record_schema')
    if 'phase=before' not in found[0] or 'phase=after' not in found[1]:raise ValueError('app_phase_order')
    if found[0].replace('phase=before','phase=same')!=found[1].replace('phase=after','phase=same'):raise ValueError('app_state_changed')
    if not re.search(r'(?m)^OK \(1 test\)\s*$',raw.decode('utf-8')):raise ValueError('instrumentation_not_passed')
    return found

def run():
    if OUT.exists():raise ValueError('output_already_exists')
    OUT.mkdir()
    evidence={}
    for host,clazz in [('fixture','EffectiveBarFixtureTest'),('main','EffectiveBarMainTest')]:
        # Fresh disposable data for each host, not an owner installation.
        adb('shell','am','force-stop',PACKAGE)
        if adb('shell','pm','clear',PACKAGE).strip()!=b'Success':raise ValueError('fresh_state_failed')
        # Keep am-instrument output capped in memory while host observes ready handshake.
        p=subprocess.Popen(['adb','shell','am','instrument','-w','-r','-e','class','dev.scrobblix.app.'+clazz,PACKAGE+'.test/androidx.test.runner.AndroidJUnitRunner'],stdout=subprocess.PIPE,stderr=subprocess.DEVNULL)
        sel=selectors.DefaultSelector();sel.register(p.stdout,selectors.EVENT_READ)
        raw=bytearray();probed=False;deadline=time.monotonic()+90;next_poll=0
        try:
            while sel.get_map():
                if time.monotonic()>deadline:raise ValueError('host_timeout')
                for key,_ in sel.select(.1):
                    b=key.fileobj.read1(8192)
                    if not b:sel.unregister(key.fileobj);continue
                    raw.extend(b)
                    if len(raw)>131072:raise ValueError('instrumentation_output_limit')
                if not probed and time.monotonic()>next_poll and p.poll() is None:
                    next_poll=time.monotonic()+.5
                    exists=adb('shell',"run-as "+PACKAGE+" sh -c 'test -f cache/bars-"+host+"-ready && echo ready || true'").strip()
                    if exists==b'ready':
                        sysui=adb('shell','dumpsys','activity','service','com.android.systemui/.SystemUIService','LightBarControllerImpl')
                        wm=adb('shell','dumpsys','window','displays')
                        # Raw bytes exist only here, never in a log/file or error message.
                        # Explicit missing fields remain unknown; missing context fails parser.
                        evidence[host]={'systemui':fields.systemui(sysui),'policy':fields.policy(wm)}
                        del sysui,wm
                        png=adb('exec-out','screencap','-p',limit=524288)
                        if not png.startswith(b'\x89PNG\r\n\x1a\n'):raise ValueError('capture_format')
                        (OUT/f'bars-{host}-adb.png').write_bytes(png)
                        adb('shell',"run-as "+PACKAGE+" sh -c 'printf \"release\\n\" > cache/bars-"+host+"-release'")
                        probed=True
            if p.wait(timeout=3)!=0 or not probed:raise ValueError('host_probe_failed')
            evidence[host]['app']=app_records(bytes(raw),host)
            for phase in ('before','after'):
                name=f'bars-{host}-ui-{phase}.png'
                b=adb('exec-out','run-as',PACKAGE,'cat','cache/'+name,limit=524288)
                if not b.startswith(b'\x89PNG\r\n\x1a\n'):raise ValueError('capture_format')
                (OUT/name).write_bytes(b)
        finally:
            sel.close()
            if p.poll() is None:p.kill()
            p.wait();p.stdout.close()
    text=json.dumps(evidence,sort_keys=True)
    if len(text)>8192:raise ValueError('result_limit')
    (OUT/'fields.json').write_text(text+'\n')
    print('SCROBBLIX_EFFECTIVE_BAR_FIELDS_V1 '+text)
    # Reuse existing bounded byte transport, but six explicitly named originals.
    sp=importlib.util.spec_from_file_location('pixels',ROOT/'about-pixel-log.py');px=importlib.util.module_from_spec(sp);sp.loader.exec_module(px)
    px.NAMES=tuple(f'bars-{h}-{p}.png' for h in ('fixture','main')for p in ('adb','ui-before','ui-after'))
    px.MAX_TOTAL=3*1024*1024
    print(px.encode(OUT),end='')

if __name__=='__main__':
    try:run()
    except Exception:
        # No exception detail can launder raw platform content into public CI.
        print('Effective bar diagnostic failed closed; no raw dump/log emitted.',file=sys.stderr)
        raise SystemExit(1)
