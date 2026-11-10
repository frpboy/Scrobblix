"""One bounded acquisition, no gestures/PNG/permission/account interaction. Caller owns identity/install/fence/cleanup."""
import subprocess,os,pathlib,json,hashlib,sys,secrets
from sanitize import foreground,own_xml
out=pathlib.Path(sys.argv[1]);expected_pid=sys.argv[2];api=int(sys.argv[3]);P='com.frpboy.scrobblix'
def adb(*a):return subprocess.check_output(['adb',*a],timeout=30,stderr=subprocess.DEVNULL)
facts={'schema':'scrobblix-acquisition-v1','purpose':'DIAGNOSTIC_ONLY_NOT_QUALIFICATION','status':'DIAGNOSTIC_PENDING_REVIEW','api':api,'probe_ref':os.environ['GITHUB_SHA'],'run_id':os.environ['GITHUB_RUN_ID'],'run_attempt':1,'apk_sha256':'8b9770217774ad02123826940f9d8749160808564b81de78d6d6676bfdce50bc','stages':{'identity':'PASS','pid':'UNKNOWN','egress':'UNKNOWN','foreground':'UNKNOWN','xml':'SKIP','privacy':'UNKNOWN','png':'SKIP','gestures':'SKIP','qualification':'SKIP'},'png_reason':'exclusive_foreground_oracle_not_proven_no_png_in_this_route','foreground':{'oracle':'UNKNOWN','fields':[],'raw_retained':False},'xml_facts':None,'error_stage':None}
stage='pid'
try:
    assert adb('shell','pidof',P).decode().strip()==expected_pid
    facts['stages']['pid']='PASS';stage='egress'
    for tool in ('iptables','ip6tables'):adb('shell',tool,'-w','30','-C','OUTPUT','!','-o','lo','-j','REJECT')
    facts['stages']['egress']='PASS'
    stage='foreground'
    try:facts['foreground']=foreground(adb('shell','dumpsys','activity','activities').decode())
    except Exception:facts['error_stage']='foreground'
    # Foreground UNKNOWN does not authorize screenshots. It also must not erase own-app XML diagnostic facts.
    stage='xml'
    try:
        guest_xml='/sdcard/scrobblix-acquisition-'+secrets.token_hex(16)+'.xml'
        adb('shell','rm','-f',guest_xml)
        try:
            adb('shell','uiautomator','dump',guest_xml)
            raw=adb('shell','cat',guest_xml);safe,xf=own_xml(raw)
        finally:adb('shell','rm','-f',guest_xml)
        (out/'own-app.xml').write_bytes(safe);facts['xml_facts']=xf;facts['stages']['xml']='PASS';facts['stages']['privacy']=xf['privacy']
    except Exception:facts['stages']['xml']='FAIL';facts['error_stage']='xml'
except Exception:
    facts['stages'][stage]='FAIL';facts['error_stage']=stage
finally:
    # Fixed error stage only, never stringify external command/XML errors into an artifact.
    (out/'facts.json').write_text(json.dumps(facts,indent=2)+'\n')
    (out/'sha256.json').write_text(json.dumps({p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in out.iterdir() if p.is_file() and p.name!='sha256.json'},indent=2)+'\n')
# Diagnostic collection success is not app qualification. The workflow name/result must say acquisition only.
