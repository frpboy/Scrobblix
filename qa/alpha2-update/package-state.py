import subprocess,re,json
package='com.frpboy.scrobblix'
def adb(*args):return subprocess.check_output(['adb',*args],text=True,timeout=20).replace('\r','')
rows=[x for x in adb('shell','pm','list','packages','-U',package).splitlines()if re.fullmatch(r'package:'+re.escape(package)+r' uid:\d+',x)]
assert len(rows)==1
uid=int(rows[0].split('uid:')[1]);assert uid>=10000
s=adb('shell','dumpsys','package',package)
versions=re.findall(r'^\s*versionCode=(\d+)\b',s,re.M);times=re.findall(r'^\s*firstInstallTime=([^\n]+)$',s,re.M)
assert len(versions)==1 and len(times)==1 and times[0].strip()
print(json.dumps({'uid':uid,'versionCode':int(versions[0]),'firstInstallTime':times[0].strip()},sort_keys=True))
