import json,sys
from pathlib import Path
a,b=[json.loads(Path(x).read_text())for x in sys.argv[1:3]]
assert a['versionCode']==6 and b['versionCode']==7
assert a['uid']==b['uid'] and a['firstInstallTime']==b['firstInstallTime']
print('PASS exact release-pair version increase, unchanged application UID and firstInstallTime')
