import pathlib,importlib.util,tempfile,os,subprocess
from unittest.mock import patch
s=importlib.util.spec_from_file_location('prep','scripts/prepare-about-gradle.py');p=importlib.util.module_from_spec(s);s.loader.exec_module(p)
repo=pathlib.Path.cwd();original=(repo/'android/gradle/wrapper/gradle-wrapper.properties').read_bytes();jar=(repo/'android/gradle/wrapper/gradle-wrapper.jar').read_bytes()
with tempfile.TemporaryDirectory() as t:
 for failure in [None,subprocess.TimeoutExpired('java',240),subprocess.CalledProcessError(1,'java')]:
  def run(args,**kw):
   assert args[0]=='/synthetic-jdk/bin/java'and args[1]=='-cp'and args[3:]==['org.gradle.wrapper.GradleWrapperMain','--version']
   f=pathlib.Path(args[2]);assert f.read_bytes()==jar
   text=f.with_suffix('.properties').read_text()
   assert 'networkTimeout=60000\n'in text and'retries=2\n'in text and'retryBackOffMs=1000\n'in text
   expected=original.decode().replace('networkTimeout=10000\n','networkTimeout=60000\n').replace('retries=0\n','retries=2\n').replace('retryBackOffMs=500\n','retryBackOffMs=1000\n');assert text==expected
   assert kw==dict(cwd=repo,check=True,timeout=240)
   if failure:raise failure
  with patch.dict(os.environ,{'JAVA_HOME':'/synthetic-jdk'}),patch.object(p.subprocess,'run',run):
   try:p.prepare(repo,t)
   except (subprocess.TimeoutExpired,subprocess.CalledProcessError):assert failure is not None
   else:assert failure is None
  assert list(pathlib.Path(t).iterdir())==[]and(repo/'android/gradle/wrapper/gradle-wrapper.properties').read_bytes()==original
  print('PASS prep mocked process',type(failure).__name__,'exact jar+URL+SHA preserved, cleanup, unchanged source')
print('No java/Gradle/download/emulator/build/runtime executed by fixtures.')
