"""Bounded fresh offline own-app observation. Gestures only within measured scroll container."""
import pathlib,subprocess,sys,struct,json,xml.etree.ElementTree as E,re
from predicate import identify,bounds
P='com.frpboy.scrobblix';private=pathlib.Path(sys.argv[1]);out=pathlib.Path(sys.argv[2]);pid=sys.argv[3]
fields={'text','class','package','checkable','checked','enabled','clickable','bounds','scrollable','selected'}
metadata={'status':'VISUAL_AND_SWITCH_REVIEW_PENDING','not_a_pass_receipt':True,'fresh_synthetic_offline_guest':True,'scroll_count':0,'captures':{}}
def adb(*args):return subprocess.check_output(['adb',*args],timeout=30)
def fence():
    assert adb('shell','pidof',P).decode().strip()==pid,'process_changed'
    for tool in ('iptables','ip6tables'):adb('shell',tool,'-w','30','-C','OUTPUT','!','-o','lo','-j','REJECT')
    # Require foreground activity, not a setting, browser or external account surface.
    assert 'com.frpboy.scrobblix/dev.scrobblix.app.MainActivity' in adb('shell','dumpsys','activity','activities').decode().split('mResumedActivity:')[-1].split('\n')[0],'foreground_changed'
def capture(name):
    fence();adb('shell','uiautomator','dump','/sdcard/window.xml');adb('pull','/sdcard/window.xml',str(private/'window.xml'))
    raw=E.parse(private/'window.xml');root=E.Element('synthetic-app-accessibility',package=P)
    for n in raw.iter('node'):
        if n.get('package')==P:E.SubElement(root,'node',{k:v for k,v in n.attrib.items() if k in fields})
    texts={n.get('text') for n in root};assert not any('Last.fm connected:' in (t or '') for t in texts),'account_present'
    if name=='initial':assert {'Scrobblix','Tracking is paused','Media access is off'}<=texts,'initial_text_gate'
    png=adb('exec-out','screencap','-p');assert png[:8]==b'\x89PNG\r\n\x1a\n' and 100<len(png)<5_000_000
    w,h=struct.unpack('>II',png[16:24]);assert 240<=w<=2560 and 240<=h<=2560 and w*h<=8_000_000,'pixel_budget'
    # Preserve guarded diagnostic bytes even when geometry/predicate subsequently fails.
    (out/(name+'-app.png')).write_bytes(png);E.ElementTree(root).write(out/(name+'-app-only.xml'),encoding='utf-8',xml_declaration=True)
    metadata['captures'][name]={'width':w,'height':h,'wm_size':adb('shell','wm','size').decode().strip(),'wm_density':adb('shell','wm','density').decode().strip()}
    (out/'diagnostic.json').write_text(json.dumps(metadata,indent=2)+'\n')
    for n in root:
        b=bounds(n);assert b[0]>=0 and b[1]>=0 and b[2]<=w and b[3]<=h,'xml_png_geometry_mismatch'
    return root,w,h
try:
    root,w,h=capture('initial')
    while True:
        try:
            row=identify(root);metadata['row']=row;break
        except LookupError:
            assert metadata['scroll_count']<3,'tracking_label_missing_after_bounded_scroll'
            # Missing label alone permits a swipe. Never scroll past a known on/ambiguous row.
            containers=[n for n in root if n.get('scrollable')=='true']
            assert len(containers)==1,'ambiguous_scroll_container'
            b=bounds(containers[0]);x=w//2;y1=int(h*.80);y2=int(h*.25)
            assert b[0]<x<b[2] and b[1]<y2<y1<b[3],'unsafe_scroll_geometry'
            # Vertical gesture within the measured own-app content, no taps or settings navigation.
            fence();adb('shell','input','swipe',str(x),str(y1),str(x),str(y2),'700');metadata['scroll_count']+=1
            root,w,h=capture('final')
    root,w,h=capture('final');metadata['row']=identify(root);metadata['predicate']='ACTUAL_ROW_ASSERTIONS_PASS_VISUAL_REVIEW_PENDING'
    (out/'diagnostic.json').write_text(json.dumps(metadata,indent=2)+'\n')
except Exception as error:
    metadata['failure_type']=type(error).__name__;metadata['failure_reason']=str(error)[:180]
    # Keep last guarded capture; never promote failure diagnostics into a result receipt.
    if (out/'initial-app.png').exists() and not (out/'final-app.png').exists():
        for suffix in ('app.png','app-only.xml'):(out/('final-'+suffix)).write_bytes((out/('initial-'+suffix)).read_bytes())
        metadata['captures']['final']=metadata['captures'].get('initial',{})
    if (out/'initial-app.png').exists():(out/'diagnostic.json').write_text(json.dumps(metadata,indent=2)+'\n')
    raise
