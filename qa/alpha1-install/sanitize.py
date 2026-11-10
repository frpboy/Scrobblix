"""Acquisition-v1 privacy sanitizers. No foreground oracle and no qualification result."""
import re,json,xml.etree.ElementTree as E,pathlib
P='com.frpboy.scrobblix';COMP=P+'/dev.scrobblix.app.MainActivity'
STATIC=set(json.loads(pathlib.Path(__file__).with_name('static-texts.json').read_text()))
FIELDS=('mResumedActivity','Resumed','topResumedActivity')
# Source-derived ActivityRecord shape; NOT a sample from the failed runtime.
RECORD=re.compile(r'^ActivityRecord\{[0-9a-f]+ u[0-9]+ ([A-Za-z0-9_.]+/[A-Za-z0-9_.$]+)(?: t-?[0-9]+)?(?: f)?\}(?: isExiting)?$')
def foreground(raw):
    assert len(raw)<=1_000_000
    facts=[]
    for field in FIELDS:
        lines=re.findall(r'^\s*'+field+r':\s*(.*)$',raw,re.M)
        assert len(lines)<=16
        matches=[RECORD.fullmatch(x.strip()) for x in lines]
        facts.append({'field':field,'observed_count':len(lines),'parsed_count':sum(x is not None for x in matches),'own_package_count':sum(x is not None and x[1].split('/')[0]==P for x in matches),'exact_component_count':sum(x is not None and x[1]==COMP for x in matches)})
    return {'oracle':'UNKNOWN','oracle_reason':'exclusive_foreground_and_overlay_safety_not_proven','fields':facts,'raw_retained':False}
def own_xml(raw):
    assert len(raw)<=250_000 and b'<!DOCTYPE' not in raw and b'<!ENTITY' not in raw
    source=E.fromstring(raw);nodes=list(source.iter('node'));assert 0<len(nodes)<=1000
    assert nodes[0].get('package')==P and all(n.get('package')==P for n in nodes),'root_or_descendant_not_own'
    root=E.Element('synthetic-own-app-xml',schema='scrobblix-acquisition-v1',package=P)
    unknown=0;account=False
    classes={'android.view.View','android.widget.TextView','android.widget.CheckBox','android.widget.Switch','android.widget.Button','android.widget.ScrollView'}
    for n in nodes:
        if n.get('package')!=P:continue
        text=n.get('text','');account|='Last.fm connected:' in text
        safe={'package':P,'class':n.get('class') if n.get('class') in classes else 'OTHER','text':text if text in STATIC or text=='' else ''}
        unknown+=int(text not in STATIC and text!='')
        for k in ('checkable','checked','clickable','enabled','scrollable','selected'):
            if n.get(k) in ('true','false'):safe[k]=n.get(k)
        if re.fullmatch(r'\[\d+,\d+\]\[\d+,\d+\]',n.get('bounds','')):safe['bounds']=n.get('bounds')
        E.SubElement(root,'node',safe)
    assert len(root)>0
    texts={n.get('text') for n in root}
    facts={'own_node_count':len(root),'unknown_text_redacted_count':unknown,'connected_account_marker':bool(account),'privacy':'FAIL' if account else 'UNKNOWN' if unknown else 'PASS','visible_texts':{k: k in texts for k in ('Scrobblix','Tracking is paused','Media access is off','Track listening')},'xml_does_not_prove_foreground':True}
    return E.tostring(root,encoding='utf-8',xml_declaration=True),facts
