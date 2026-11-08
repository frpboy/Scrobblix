"""Warm only the checksum-pinned Wrapper distribution with bounded download retries.
No build task, SDK, emulator or runtime starts here; no source property is edited.
"""
import os,pathlib,subprocess,tempfile,re

def prepare(repo,runner_temp):
    repo=pathlib.Path(repo)
    original=repo/'android/gradle/wrapper/gradle-wrapper.properties'
    text=original.read_text()
    # Do not let this CI helper widen the owner-pinned distribution/version/hash.
    assert re.search(r'^distributionUrl=https\\://services\.gradle\.org/distributions/gradle-9\.6\.0-bin\.zip$',text,re.M)
    assert re.search(r'^distributionSha256Sum=bbaeb2fef8710818cf0e261201dab964c572f92b942812df0c3620d62a529a01$',text,re.M)
    for key,value in [('networkTimeout','60000'),('retries','2'),('retryBackOffMs','1000')]:
        text,n=re.subn(r'^'+key+r'=.*$',key+'='+value,text,flags=re.M);assert n==1
    with tempfile.TemporaryDirectory(prefix='about-gradle-prep-',dir=runner_temp) as directory:
        root=pathlib.Path(directory)
        # Wrapper locates .properties beside its JAR. Copy exact JAR bytes, no new binary.
        jar=root/'gradle-wrapper.jar'
        jar.write_bytes((repo/'android/gradle/wrapper/gradle-wrapper.jar').read_bytes())
        (root/'gradle-wrapper.properties').write_text(text)
        subprocess.run([str(pathlib.Path(os.environ['JAVA_HOME'])/'bin/java'),'-cp',str(jar),'org.gradle.wrapper.GradleWrapperMain','--version'],cwd=repo,check=True,timeout=240)
    print('ABOUT_GRADLE_PREP pinned distribution prepared, no build/runtime tasks')

if __name__=='__main__':prepare(pathlib.Path(__file__).resolve().parent.parent,os.environ['RUNNER_TEMP'])
