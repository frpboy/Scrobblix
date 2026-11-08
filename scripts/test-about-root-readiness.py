"""Actual bounded readiness helper under synthetic argv-only adb/clock stubs."""
import os,pathlib,subprocess,tempfile,json
with tempfile.TemporaryDirectory() as t:
 root=pathlib.Path(t)
 (root/'adb').write_text('''#!/usr/bin/env python3
import sys,os,pathlib,json
r=pathlib.Path(os.environ['FIXTURE_ROOT']);args=sys.argv[1:]
with (r/'argv').open('a') as f:f.write(json.dumps(args)+'\\n')
if args==['root']:
 n=int((r/'count').read_text())+1;(r/'count').write_text(str(n))
 if os.environ['CASE'] in ('closed','recover') and (os.environ['CASE']=='closed' or n==1):sys.exit(1)
elif args==['wait-for-device']:
 if os.environ['CASE']=='waitfail':sys.exit(1)
elif args==['shell','id','-u']:
 if os.environ['CASE']=='uidfail':sys.exit(1)
 print('2000' if os.environ['CASE']=='nonroot' else '0\\r')
else:raise AssertionError(args)
''')
 (root/'sleep').write_text('#!/bin/sh\nexit 0\n')
 for p in [root/'adb',root/'sleep']:p.chmod(0o755)
 for case,ok,roots in [('ready',True,1),('recover',True,2),('closed',False,3),('waitfail',False,3),('nonroot',False,3),('uidfail',False,3)]:
  (root/'count').write_text('0');(root/'argv').write_text('')
  p=subprocess.run(['bash','scripts/about-adb-root-ready.sh'],env=os.environ|dict(FIXTURE_ROOT=t,CASE=case,PATH=t+':'+os.environ['PATH']),capture_output=True,text=True,timeout=10)
  assert (p.returncode==0)==ok,(case,p.stdout,p.stderr)
  assert int((root/'count').read_text())==roots
  args=[json.loads(l)for l in(root/'argv').read_text().splitlines()]
  assert all(a in [['root'],['wait-for-device'],['shell','id','-u']]for a in args)
  print('PASS',case,'root_attempts='+str(roots),'success='+str(ok))
print('No emulator, network, install or instrumentation used.')
