import importlib.util,os,subprocess,tempfile,pathlib,json
spec=importlib.util.spec_from_file_location("guard","scripts/guard-about-qualification.py");g=importlib.util.module_from_spec(spec);spec.loader.exec_module(g)
sha="a"*40
base=dict(EXPECTED_SHA=sha,GITHUB_SHA=sha,GITHUB_EVENT_NAME="workflow_dispatch",GITHUB_RUN_ATTEMPT="1",GITHUB_REPOSITORY="frpboy/Scrobblix")
g.validate(base,sha)
for key,value in [("EXPECTED_SHA","main"),("GITHUB_SHA","b"*40),("GITHUB_EVENT_NAME","push"),("GITHUB_RUN_ATTEMPT","2"),("GITHUB_REPOSITORY","other/repo")]:
 bad=base|{key:value}
 try:g.validate(bad,sha)
 except ValueError:pass
 else:raise AssertionError(key)
try:g.validate(base,"b"*40)
except ValueError:pass
else:raise AssertionError("checkout")
with tempfile.TemporaryDirectory() as t:
 for tool in ["sdkmanager","avdmanager"]:
  p=pathlib.Path(t)/tool;p.write_text('#!/usr/bin/env python3\nimport sys,json\nprint(json.dumps(sys.argv))\n');p.chmod(0o755)
 for api in ["26","36"]:
  lines=subprocess.check_output(["bash","scripts/qualify-about-android.sh"],env=os.environ|dict(ABOUT_API=api,ABOUT_ARGV_FIXTURE="1",PATH=t+":"+os.environ["PATH"]),text=True).splitlines()
  args=[json.loads(l) for l in lines];target=f"system-images;android-{api};default;x86_64"
  assert args[0][1:]==["emulator",target]
  assert args[1][1:]==["create","avd","-n","scrobblix-test","-k",target]
  print("PASS exact SDK/AVD argv",api,target)
print("PASS guard: one valid + six denied cases; no SDK/emulator calls")
