from pathlib import Path
import subprocess,re,sys,json,struct,hashlib,xml.etree.ElementTree as E
p=Path(sys.argv[1]);api=int(sys.argv[2]);assert api in[26,36]
def adb(*args):return subprocess.check_output(['adb',*args],timeout=25)
def foreground():
 s=adb('shell','dumpsys','activity','activities').decode(errors='replace')
 rows=[x for x in s.splitlines()if 'mResumedActivity:'in x or 'topResumedActivity='in x or 'ResumedActivity:'in x]
 assert rows and any('com.frpboy.scrobblix/dev.scrobblix.app.MainActivity'in x for x in rows)
 pid=adb('shell','pidof','com.frpboy.scrobblix').decode().strip();assert re.fullmatch(r'\d+',pid)
 return pid
pid=foreground()
# UIAutomator is a read/dump operation; no taps, permission changes or toggles.
adb('shell','uiautomator','dump','/sdcard/alpha2-exact.xml')
raw=adb('shell','cat','/sdcard/alpha2-exact.xml');root=E.fromstring(raw)
png=adb('exec-out','screencap','-p');assert png[:8]==b'\x89PNG\r\n\x1a\n';width,height=struct.unpack('>II',png[16:24]);assert 200<=width<=4096 and 200<=height<=4096
assert foreground()==pid
own=[n for n in root.iter('node')if n.get('package')=='com.frpboy.scrobblix'];assert own
for n in own:
 b=re.fullmatch(r'\[(\d+),(\d+)\]\[(\d+),(\d+)\]',n.get('bounds',''));assert b
 x1,y1,x2,y2=map(int,b.groups());assert 0<=x1<=x2<=width and 0<=y1<=y2<=height
texts='\n'.join(n.get('text','')for n in own);assert 'Scrobblix'in texts and 'Tracking is paused'in texts
# Retain only own synthetic app nodes in XML. Pixels are from the clean disposable guest.
san=E.Element('synthetic-app',{'api':str(api),'width':str(width),'height':str(height)})
for n in own:E.SubElement(san,'node',{k:n.get(k,'')for k in ['text','content-desc','class','resource-id','bounds','checked','checkable','enabled']})
(p/f'api{api}-candidate.png').write_bytes(png);E.ElementTree(san).write(p/f'api{api}-candidate.xml',encoding='utf-8',xml_declaration=True)
(p/f'api{api}-capture.json').write_text(json.dumps({'api':api,'component':'com.frpboy.scrobblix/dev.scrobblix.app.MainActivity','same_pid_adjacent':True,'width':width,'height':height,'tracking_paused_text':True,'png_sha256':hashlib.sha256(png).hexdigest(),'pixel_review_pending':True},indent=2))
print('PASS adjacent candidate Activity/native pixel/XML capture; actual pixel review still pending')
