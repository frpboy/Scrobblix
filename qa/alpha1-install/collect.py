"""Finite XML-only assertion collector. Independent actual row/admission/hash verification."""
import io,zipfile,stat,json,hashlib,pathlib,xml.etree.ElementTree as E
from sanitize import own_xml
from predicate import identify,bounds
CAPTURES={'capture-'+str(i)+'.xml' for i in range(4)}
def collect(data,dest,api,probe,run):
    assert len(data)<1_200_000
    z=zipfile.ZipFile(io.BytesIO(data));entries=z.infolist();names=[x.filename for x in entries];assert len(names)==len(set(names)) and set(names)<=(CAPTURES|{'facts.json','sha256.json','result.json'}) and {'facts.json','sha256.json'}<=set(names)
    assert sum(x.file_size for x in entries)<1_200_000
    for x in entries:assert not x.is_dir() and not stat.S_ISLNK(x.external_attr>>16) and 0<x.file_size<=(250_000 if x.filename in CAPTURES else 40_000)
    b={x.filename:z.read(x) for x in entries};h=json.loads(b['sha256.json']);assert set(h)==set(b)-{'sha256.json'}
    for n,v in h.items():assert hashlib.sha256(b[n]).hexdigest()==v
    f=json.loads(b['facts.json']);assert set(f)=={'schema','status','api','probe_ref','run_id','run_attempt','apk_sha256','captures','scroll_count','admission_union','row','failure_stage','png','pixel_exclusivity'}
    assert f['schema']=='scrobblix-xml-qualification-v1' and (f['api'],f['probe_ref'],f['run_id'],f['run_attempt'])==(api,probe,str(run),1)
    assert f['apk_sha256']=='8b9770217774ad02123826940f9d8749160808564b81de78d6d6676bfdce50bc' and f['png']=='SKIP' and f['pixel_exclusivity']=='NOT_CLAIMED'
    assert f['status'] in ('XML_QUALIFICATION_FAILED','XML_ASSERTIONS_PASS_CLEANUP_PENDING') and type(f['scroll_count']) is int and 0<=f['scroll_count']<=3
    assert f['failure_stage'] in (None,'initial','row','scroll_safety','capture','admission')
    assert set(f['admission_union'])=={'Scrobblix','Tracking is paused','Media access is off'} and all(type(v) is bool for v in f['admission_union'].values())
    xmlnames=sorted(set(b)&CAPTURES);assert xmlnames==['capture-'+str(i)+'.xml' for i in range(len(xmlnames))]
    roots={};seen=set()
    for n in xmlnames:
        root=E.fromstring(b[n]);assert root.tag=='synthetic-own-app-xml' and root.attrib=={'schema':'scrobblix-acquisition-v1','package':'com.frpboy.scrobblix'}
        # Re-sanitize through the same privacy boundary, then independently inspect strict identity/booleans/text.
        raw=E.Element('hierarchy')
        for node in root:
            assert node.tag=='node' and not list(node) and set(node.attrib)<={'package','class','text','checkable','checked','clickable','enabled','scrollable','selected','bounds'} and node.get('package')=='com.frpboy.scrobblix'
            raw.append(node)
        safe,xf=own_xml(E.tostring(raw));assert xf['privacy']=='PASS' and not xf['connected_account_marker'] and xf['unknown_text_redacted_count']==0
        roots[n]=root;seen|={node.get('text') for node in root}
    assert len(f['captures'])<=len(xmlnames)
    for i,c in enumerate(f['captures']):
        assert set(c)=={'file','sha256','xml_width','xml_height','pre_component','post_component','privacy','own_node_count'} and c['file']=='capture-'+str(i)+'.xml' and c['sha256']==h[c['file']] and c['privacy']=='PASS'
        root=roots[c['file']];assert c['own_node_count']==len(root);rb=bounds(root[0]);assert (c['xml_width'],c['xml_height'])==(rb[2],rb[3]) and rb[0]==rb[1]==0
        assert 240<=rb[2]<=2560 and 240<=rb[3]<=2560 and rb[2]*rb[3]<=8_000_000
        for node in root:
            q=bounds(node);assert 0<=q[0]<q[2]<=rb[2] and 0<=q[1]<q[3]<=rb[3]
        expected={'field':{26:'mResumedActivity',36:'Resumed'}[api],'observed_count':1,'parsed_count':1,'own_package_count':1,'exact_component_count':1,'scope':'activity_component_only_not_exclusive_pixels'}
        assert c['pre_component']==c['post_component']==expected
    if 'result.json' in b:
        assert f['status']=='XML_ASSERTIONS_PASS_CLEANUP_PENDING' and f['failure_stage'] is None and len(xmlnames)==len(f['captures'])==f['scroll_count']+1
        assert all(f['admission_union'].values()) and all(t in seen for t in f['admission_union']) and 'Tracking is on' not in seen
        last=roots[xmlnames[-1]];actual=identify(last);assert json.loads(json.dumps(actual))==f['row']
        r=json.loads(b['result.json']);assert set(r)=={'schema','status','api','probe_ref','run_id','run_attempt','source_commit','apk_sha256','signer_sha256','package','version_code','fresh_install','cold_start_alive_seconds','tracking_enabled','media_access_enabled','account_connected','egress','cleanup','png','pixel_exclusivity','facts_sha256','not_proven'}
        assert r['schema']=='scrobblix-xml-qualification-v1' and r['source_commit']=='acf3d1bfc7c395bedff3a854b05e1b6be870b50e' and r['package']=='com.frpboy.scrobblix' and r['version_code']==6 and r['fresh_install'] is True and r['cold_start_alive_seconds']==10
        assert r['egress']=='guest IPv4 and IPv6 OUTPUT rejected before install and during acquisition' and r['cleanup']=='owned app force-stopped and emulator stopped; no reset/uninstall'
        assert r['not_proven']==['full visual qualification','owner-device update or data preservation','provider authorization or delivery','real player/background listening','full beta readiness']
        assert r['status']=='XML_ASSERTIONS_AND_OWNED_CLEANUP_PASS_VISUAL_REVIEW_PENDING' and (r['api'],r['probe_ref'],r['run_id'],r['run_attempt'])==(api,probe,str(run),1)
        assert r['apk_sha256']==f['apk_sha256'] and r['signer_sha256']=='a9b37328df9b9c166ceb41247cf9fd394427676832ab391c10f7494a711f7cde'
        assert r['tracking_enabled'] is False and r['media_access_enabled'] is False and r['account_connected'] is False and r['png']=='SKIP' and r['pixel_exclusivity']=='NOT_CLAIMED' and r['facts_sha256']==h['facts.json']
    else:assert f['status']=='XML_QUALIFICATION_FAILED'
    d=pathlib.Path(dest);d.mkdir(exist_ok=False)
    for n,v in b.items():(d/n).write_bytes(v)
    return {'status':f['status'],'authoritative_result_present':'result.json' in b,'visual_review_pending':True,'png':'SKIP'}
