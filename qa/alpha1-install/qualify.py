"""XML-only bounded qualification candidate. No PNG or exclusive-pixel claim."""
import pathlib,subprocess,sys,os,json,hashlib,secrets,xml.etree.ElementTree as E
from sanitize import foreground,own_xml
from predicate import identify,bounds
P='com.frpboy.scrobblix';out=pathlib.Path(sys.argv[1]);pid=sys.argv[2];api=int(sys.argv[3]);expected_field={26:'mResumedActivity',36:'Resumed'}[api]
facts={'schema':'scrobblix-xml-qualification-v1','status':'PENDING_NOT_RESULT','api':api,'probe_ref':os.environ['GITHUB_SHA'],'run_id':os.environ['GITHUB_RUN_ID'],'run_attempt':1,'apk_sha256':'8b9770217774ad02123826940f9d8749160808564b81de78d6d6676bfdce50bc','captures':[],'scroll_count':0,'admission_union':{},'row':None,'failure_stage':None,'png':'SKIP','pixel_exclusivity':'NOT_CLAIMED'}
seen=set();stage='initial'
def adb(*args):return subprocess.check_output(['adb',*args],timeout=30,stderr=subprocess.DEVNULL)
def fence():
    assert adb('shell','pidof',P).decode().strip()==pid,'pid_changed'
    for tool in ('iptables','ip6tables'):adb('shell',tool,'-w','30','-C','OUTPUT','!','-o','lo','-j','REJECT')
    f=foreground(adb('shell','dumpsys','activity','activities').decode())
    rows={r['field']:r for r in f['fields']};match=rows[expected_field]
    assert match['observed_count']==match['parsed_count']==match['own_package_count']==match['exact_component_count']==1,'active_component_not_exact'
    assert all(r['observed_count']==0 for k,r in rows.items() if k!=expected_field),'foreground_disagreement'
    return {'field':expected_field,'observed_count':1,'parsed_count':1,'own_package_count':1,'exact_component_count':1,'scope':'activity_component_only_not_exclusive_pixels'}
def capture(index):
    pre=fence();guest='/sdcard/scrobblix-qualify-'+secrets.token_hex(16)+'.xml';adb('shell','rm','-f',guest)
    try:
        adb('shell','uiautomator','dump',guest);raw=adb('shell','cat',guest)
    finally:adb('shell','rm','-f',guest)
    safe,xf=own_xml(raw);assert xf['privacy']=='PASS' and not xf['connected_account_marker'] and xf['unknown_text_redacted_count']==0,'privacy_gate'
    root=E.fromstring(safe);post=fence();name='capture-'+str(index)+'.xml';(out/name).write_bytes(safe)
    # XML coordinate space, not screenshot/physical-resolution proof. Entire tree bounded to root.
    rb=bounds(root[0]);assert rb[0]==rb[1]==0 and 240<=rb[2]<=2560 and 240<=rb[3]<=2560 and rb[2]*rb[3]<=8_000_000,'xml_geometry'
    for n in root:
        b=bounds(n);assert 0<=b[0]<b[2]<=rb[2] and 0<=b[1]<b[3]<=rb[3],'node_outside_root'
    texts={n.get('text') for n in root};assert 'Tracking is on' not in texts,'tracking_on'
    seen.update(texts);facts['captures'].append({'file':name,'sha256':hashlib.sha256(safe).hexdigest(),'xml_width':rb[2],'xml_height':rb[3],'pre_component':pre,'post_component':post,'privacy':'PASS','own_node_count':len(root)})
    return root,rb
try:
    root,rb=capture(0)
    while True:
        stage='row'
        try:facts['row']=identify(root);break
        except LookupError:
            assert facts['scroll_count']<3,'missing_row_after_three_scrolls'
            stage='scroll_safety';containers=[n for n in root if n.get('scrollable')=='true'];assert len(containers)==1,'ambiguous_scroll_container'
            b=bounds(containers[0]);x=(b[0]+b[2])//2;y1=b[1]+int((b[3]-b[1])*.80);y2=b[1]+int((b[3]-b[1])*.25)
            assert b[0]<x<b[2] and b[1]<y2<y1<b[3],'unsafe_swipe_region'
            # Actual own-app content scroll. Never tap; checked/ambiguous row failures do not enter this branch.
            fence();adb('shell','input','swipe',str(x),str(y1),str(x),str(y2),'700');fence();facts['scroll_count']+=1
            stage='capture';root,rb=capture(facts['scroll_count'])
    stage='admission';required={'Scrobblix','Tracking is paused','Media access is off'};facts['admission_union']={s:s in seen for s in sorted(required)};assert required<=seen,'admission_texts_not_observed'
    facts['status']='XML_ASSERTIONS_PASS_CLEANUP_PENDING'
except Exception:
    facts['failure_stage']=stage;facts['status']='XML_QUALIFICATION_FAILED'
    raise
finally:
    facts['admission_union']={s:s in seen for s in ('Scrobblix','Tracking is paused','Media access is off')}
    (out/'facts.json').write_text(json.dumps(facts,indent=2)+'\n')
    (out/'sha256.json').write_text(json.dumps({p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in out.iterdir() if p.is_file() and p.name!='sha256.json'},indent=2)+'\n')
