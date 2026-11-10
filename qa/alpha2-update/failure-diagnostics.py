"""Bounded startup diagnostics, no raw app log/database/credential export."""
from pathlib import Path
import sys,subprocess,json,re,os
private,evidence=map(Path,sys.argv[1:3]);api=int(sys.argv[3]);status=int(sys.argv[4]);assert api in[26,36]
def capture(args):
 try:
  r=subprocess.run(args,capture_output=True,text=True,timeout=8)
  return r.returncode,(r.stdout+r.stderr).splitlines()[-40:]
 except Exception:return None,['diagnostic_command_unavailable']
# Whitelist startup engine messages only. Never adb logcat, app data or arbitrary env.
log=private/'emulator.log';lines=[]
if log.exists():
 tail=log.read_text(errors='replace').splitlines()[-160:]
 for line in tail:
  if re.search(r'Boot completed|boot.*fail|ERROR|WARNING|KVM|hardware acceleration|emulator version|Emulator version|Vulkan|OpenGL|GPU|ADB|adb|snapshot',line,re.I):
   line=re.sub(r'/[^\s,;]+','[path]',line)
   line=re.sub(r'\b(?:\d{1,3}\.){3}\d{1,3}\b','[ip]',line)
   line=re.sub(r'(?i)(token|secret|password|jwt|key)[=: ]+[^\s,;]+',r'\1=[redacted]',line)
   lines.append(line[:500])
rc,devices=capture(['adb','devices','-l']);_,boot=capture(['adb','shell','getprop','sys.boot_completed']);_,uid=capture(['adb','shell','id','-u'])
# Provider/account text cannot enter these strictly parsed scalar diagnostics.
r={'api':api,'phase':'incomplete'if status else'runner_complete','exit_code':status,'qualification_complete':False,'attempt':os.environ.get('GITHUB_RUN_ATTEMPT'),'startup_tail':lines[-40:],'adb_return_code':rc,'device_state':('offline'if any('offline'in x for x in devices)else'device'if any(re.match(r'emulator-\d+\s+device\b',x)for x in devices)else'unavailable'),'boot_completed':any(x.strip()=='1'for x in boot),'uid_zero':any(x.strip()=='0'for x in uid),'phone_or_live_provider_proof':False}
(evidence/f'api{api}-startup-diagnostics.json').write_text(json.dumps(r,indent=2))
print(json.dumps({k:r[k]for k in ['api','phase','exit_code','qualification_complete','device_state','boot_completed','uid_zero']}))
