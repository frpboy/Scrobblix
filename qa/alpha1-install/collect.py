"""Independent finite acquisition-v1 collector. No PNG/result/qualification receipt accepted."""
import zipfile,io,stat,json,hashlib,pathlib,xml.etree.ElementTree as E
P='com.frpboy.scrobblix'
def collect(data,dest,api,probe,run):
    assert len(data)<=300_000
    z=zipfile.ZipFile(io.BytesIO(data));entries=z.infolist();names=[i.filename for i in entries];assert len(set(names))==len(names)
    assert set(names) in ({'facts.json','sha256.json'},{'facts.json','own-app.xml','sha256.json'})
    assert sum(i.file_size for i in entries)<=300_000
    for i in entries:
        assert not i.is_dir() and not stat.S_ISLNK(i.external_attr>>16)
        assert 0<i.file_size<=(250_000 if i.filename=='own-app.xml' else 30_000)
    b={i.filename:z.read(i) for i in entries};hashes=json.loads(b['sha256.json']);assert set(hashes)==set(b)-{'sha256.json'}
    for n,v in hashes.items():assert hashlib.sha256(b[n]).hexdigest()==v
    f=json.loads(b['facts.json']);assert set(f)=={'schema','purpose','status','api','probe_ref','run_id','run_attempt','apk_sha256','stages','png_reason','foreground','xml_facts','error_stage'}
    assert f['schema']=='scrobblix-acquisition-v1' and f['purpose']=='DIAGNOSTIC_ONLY_NOT_QUALIFICATION' and f['status']=='DIAGNOSTIC_PENDING_REVIEW'
    assert (f['api'],f['probe_ref'],f['run_id'],f['run_attempt'])==(api,probe,str(run),1)
    assert f['apk_sha256']=='8b9770217774ad02123826940f9d8749160808564b81de78d6d6676bfdce50bc'
    assert set(f['stages'])=={'identity','pid','egress','foreground','xml','privacy','png','gestures','qualification'} and set(f['stages'].values())<={'PASS','FAIL','UNKNOWN','SKIP'}
    assert f['stages']['identity']=='PASS' and f['stages']['foreground']=='UNKNOWN' and all(f['stages'][k]=='SKIP' for k in ('png','gestures','qualification'))
    assert f['png_reason']=='exclusive_foreground_oracle_not_proven_no_png_in_this_route' and f['error_stage'] in (None,'pid','egress','foreground','xml')
    fg=f['foreground'];assert set(fg)<={'oracle','oracle_reason','fields','raw_retained'} and fg['oracle']=='UNKNOWN' and fg['raw_retained'] is False
    assert fg.get('oracle_reason') in (None,'exclusive_foreground_and_overlay_safety_not_proven')
    assert len(fg['fields']) in (0,3)
    assert not fg['fields'] or {r['field'] for r in fg['fields']}=={'mResumedActivity','Resumed','topResumedActivity'}
    for row in fg['fields']:
        assert set(row)=={'field','observed_count','parsed_count','own_package_count','exact_component_count'} and row['field'] in ('mResumedActivity','Resumed','topResumedActivity')
        assert all(type(row[k]) is int and 0<=row[k]<=16 for k in row if k!='field')
    if 'own-app.xml' in b:
        assert f['stages']['pid']==f['stages']['egress']==f['stages']['xml']=='PASS'
        root=E.fromstring(b['own-app.xml']);assert root.tag=='synthetic-own-app-xml' and root.attrib=={'schema':'scrobblix-acquisition-v1','package':P} and 0<len(root)<=1000
        static=set(json.loads(pathlib.Path(__file__).with_name('static-texts.json').read_text()))|{''}
        for n in root:
            assert n.tag=='node' and not list(n) and n.get('package')==P and n.get('text') in static
            assert set(n.attrib)<={'package','class','text','checkable','checked','clickable','enabled','scrollable','selected','bounds'}
            assert n.get('class') in ('android.view.View','android.widget.TextView','android.widget.CheckBox','android.widget.Switch','android.widget.Button','android.widget.ScrollView','OTHER')
            for k in ('checkable','checked','clickable','enabled','scrollable','selected'):
                assert n.get(k) in (None,'true','false')
            if 'bounds' in n.attrib:assert __import__('re').fullmatch(r'\[\d+,\d+\]\[\d+,\d+\]',n.get('bounds'))
        xf=f['xml_facts'];assert set(xf)=={'own_node_count','unknown_text_redacted_count','connected_account_marker','privacy','visible_texts','xml_does_not_prove_foreground'} and xf['own_node_count']==len(root)
        assert type(xf['unknown_text_redacted_count']) is int and 0<=xf['unknown_text_redacted_count']<=1000 and type(xf['connected_account_marker']) is bool
        assert xf['privacy'] in ('PASS','FAIL','UNKNOWN') and xf['privacy']==f['stages']['privacy'] and xf['xml_does_not_prove_foreground'] is True
        assert set(xf['visible_texts'])=={'Scrobblix','Tracking is paused','Media access is off','Track listening'} and all(type(v) is bool for v in xf['visible_texts'].values())
    else:assert f['xml_facts'] is None and f['stages']['xml'] in ('FAIL','SKIP')
    p=pathlib.Path(dest);p.mkdir(exist_ok=False)
    for n,v in b.items():(p/n).write_bytes(v)
    return f
